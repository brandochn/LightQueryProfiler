import * as vscode from "vscode";
import * as path from "path";
import * as fs from "fs";
import { ProfilerPanelProvider } from "./views/profiler-panel-provider";
import { ConnectionsPanelProvider } from "./views/connections-panel-provider";
import { ProfilerClient } from "./services/profiler-client";
import { MssqlConnectionReader } from "./services/mssql-connection-reader";
import { ConnectionProfile } from "./models/connection-profile";

/**
 * Logger interface for structured logging
 */
interface Logger {
  readonly info: (message: string) => void;
  readonly warn: (message: string) => void;
  readonly error: (message: string) => void;
}

/**
 * Extension state container
 * @remarks This container holds the lifecycle-managed resources for the extension
 */
interface ExtensionState {
  profilerClient: ProfilerClient | undefined;
  profilerPanelProvider: ProfilerPanelProvider | undefined;
  connectionsPanelProvider: ConnectionsPanelProvider | undefined;
  outputChannel: vscode.OutputChannel | undefined;
}

/**
 * Global extension state
 */
const state: ExtensionState = {
  profilerClient: undefined,
  profilerPanelProvider: undefined,
  connectionsPanelProvider: undefined,
  outputChannel: undefined,
};

// ── Module-level concurrency guard for MSSQL import ────────────────────
let isImportingFromMssql = false;

/**
 * Imports MSSQL connection profiles into Light Query Profiler's connection store.
 * @param connections - Array of ConnectionProfile objects to import
 * @param client - ProfilerClient instance for backend communication
 * @param context - Extension context for globalState
 * @param log - Logger instance
 * @remarks This function is guarded against concurrent execution. Only one import
 * can run at a time. The `mssqlImportCompleted` flag is only set when at least
 * one connection is successfully imported.
 */
async function importMssqlConnections(
  connections: ConnectionProfile[],
  client: ProfilerClient,
  context: vscode.ExtensionContext,
  log: Logger,
): Promise<void> {
  // -- Concurrency guard -------------------------------------------
  if (isImportingFromMssql) {
    log.warn("MSSQL import already in progress, skipping duplicate request.");
    return;
  }
  isImportingFromMssql = true;

  try {
    // Ensure the JSON-RPC server is running before attempting saves.
    // The one-time detection fires early in activation before any panel
    // has triggered server start; the manual button path also benefits
    // from this guard should the server have stopped unexpectedly.
    if (!client.isRunning()) {
      log.info("Starting server before MSSQL import...");
      try {
        await client.start();
      } catch (error) {
        const errorMessage =
          error instanceof Error ? error.message : String(error);
        log.error(
          `Failed to start profiler server for MSSQL import: ${errorMessage}`,
        );
        await vscode.window.showErrorMessage(
          `Failed to start the profiler server. Cannot import connections: ${errorMessage}`,
        );
        return;
      }
    }

    let importedCount = 0;
    let errorCount = 0;

    for (const conn of connections) {
      try {
        await client.saveConnection(conn);
        importedCount++;
      } catch (error) {
        errorCount++;
        const errorMessage =
          error instanceof Error ? error.message : String(error);
        log.error(
          `Failed to import MSSQL connection "${conn.profileName || conn.dataSource}": ${errorMessage}`,
        );
      }
    }

    if (importedCount > 0) {
      // Fire-and-forget: do NOT await the dialog so the caller can refresh
      // the connections table immediately while the message is still visible.
      void vscode.window.showInformationMessage(
        `Successfully imported ${importedCount} connection(s) from the MSSQL extension.`,
      );
      // Only mark as completed if at least one import succeeded (Issue 1 fix)
      await context.globalState.update("mssqlImportCompleted", true);
    }

    if (errorCount > 0) {
      // Fire-and-forget: same reason as above — let the UI refresh without
      // waiting for the user to dismiss this warning.
      void vscode.window.showWarningMessage(
        `${errorCount} connection(s) could not be imported. Check the output log for details.`,
      );
    }
  } finally {
    isImportingFromMssql = false;
  }
}

/**
 * Activates the extension
 * @param context - Extension context provided by VS Code
 * @remarks This is called when the extension is first activated
 */
export async function activate(
  context: vscode.ExtensionContext,
): Promise<void> {
  // Create output channel first for logging
  state.outputChannel = vscode.window.createOutputChannel(
    "Light Query Profiler",
  );
  const log = createLogger(state.outputChannel);

  log.info("Activating Light Query Profiler extension...");

  // IMPORTANT: Register the command IMMEDIATELY — before any awaits.
  // VS Code may dispatch the command while activate() is still running its
  // async initialization (getDotnetPath does execAsync ~200ms).  If the
  // command handler is not yet registered at that point the invocation is
  // silently swallowed, which is why the panel sometimes does not open.
  // The handler checks whether the provider is ready and either shows the
  // panel or queues a retry once initialization completes.
  let activationReady = false;
  const exportEventsCommand = vscode.commands.registerCommand(
    "lightQueryProfiler.exportEvents",
    () => {
      log.info("Export Events command executed");
      if (state.profilerPanelProvider) {
        void state.profilerPanelProvider.exportEvents();
      } else {
        void vscode.window.showErrorMessage(
          "Light Query Profiler: Extension is not initialized.",
        );
      }
    },
  );
  context.subscriptions.push(exportEventsCommand);

  const importEventsCommand = vscode.commands.registerCommand(
    "lightQueryProfiler.importEvents",
    () => {
      log.info("Import Events command executed");
      if (state.profilerPanelProvider) {
        void state.profilerPanelProvider.importEvents();
      } else {
        void vscode.window.showErrorMessage(
          "Light Query Profiler: Extension is not initialized.",
        );
      }
    },
  );
  context.subscriptions.push(importEventsCommand);

  const showConnectionsCommand = vscode.commands.registerCommand(
    "lightQueryProfiler.showConnections",
    () => {
      log.info("Show Connections command executed");
      if (state.connectionsPanelProvider) {
        void state.connectionsPanelProvider.show();
      } else {
        void vscode.window.showErrorMessage(
          "Light Query Profiler: Extension is not initialized.",
        );
      }
    },
  );
  context.subscriptions.push(showConnectionsCommand);

  const showProfilerCommand = vscode.commands.registerCommand(
    "lightQueryProfiler.showProfiler",
    () => {
      log.info("Show SQL Profiler command executed");
      if (state.profilerPanelProvider) {
        state.profilerPanelProvider.showPanel();
      } else if (!activationReady) {
        // Extension is still initializing — wait for it then open the panel
        log.info("Provider not ready yet, deferring panel open...");
        const deferredInterval = setInterval(() => {
          if (state.profilerPanelProvider) {
            clearInterval(deferredInterval);
            clearTimeout(deferredTimeout);
            log.info("Provider ready, opening deferred panel");
            state.profilerPanelProvider.showPanel();
          }
        }, 50);
        // Safety: stop polling after 10 s regardless
        // eslint-disable-next-line prefer-const
        const deferredTimeout = setTimeout(
          () => clearInterval(deferredInterval),
          10_000,
        );
        // Register both handles so they are cancelled if the extension is
        // deactivated within the 10-second initialization window.
        context.subscriptions.push({
          dispose: () => {
            clearInterval(deferredInterval);
            clearTimeout(deferredTimeout);
          },
        });
      } else {
        log.error("Profiler panel provider not initialized");
        void vscode.window.showErrorMessage(
          "Failed to open SQL Profiler. Please reload the window.",
        );
      }
    },
  );
  context.subscriptions.push(showProfilerCommand);

  try {
    // Get server DLL path and dotnet path in parallel (no duplicate dotnet check)
    const serverDllPath = getServerDllPath(context, log);
    if (!serverDllPath) {
      const message = "Light Query Profiler server not found.";
      log.error(message);
      activationReady = true;
      await vscode.window.showErrorMessage(message, "Error");
      return;
    }

    log.info(`Server DLL path: ${serverDllPath}`);

    // Get dotnet path (single check — no duplicate exec)
    const dotnetPath = await getDotnetPath(log);
    log.info(`dotnet path: ${dotnetPath}`);

    // Create profiler client
    state.profilerClient = new ProfilerClient(
      dotnetPath,
      serverDllPath,
      state.outputChannel,
    );

    // Create panel provider
    state.profilerPanelProvider = new ProfilerPanelProvider(
      context.extensionUri,
      state.profilerClient,
      state.outputChannel,
    );

    // Create connections provider
    state.connectionsPanelProvider = new ConnectionsPanelProvider(
      context.extensionUri,
      state.profilerClient,
      state.outputChannel,
      (connection: ConnectionProfile) => {
        // Double-click / Enter: fill connection fields only (no auto-start).
        // NOTE: method is showPanel(), not show()
        state.profilerPanelProvider?.showPanel();
        state.profilerPanelProvider?.fillConnectionFields(connection);
      },
      (connection: ConnectionProfile) => {
        // "Start Profiling" button: fill connection fields and start profiling automatically.
        void state.profilerPanelProvider?.startProfilingWithConnection(
          connection,
        );
      },
    );

    // Wire the "Connections" toolbar button inside the profiler webview
    state.profilerPanelProvider.setOnShowConnections(() => {
      state.connectionsPanelProvider?.show();
    });

    // -- One-time MSSQL connection import detection --------------------
    void (async () => {
      const mssqlImportCompleted = context.globalState.get<boolean>(
        "mssqlImportCompleted",
        false,
      );
      if (!mssqlImportCompleted) {
        const reader = new MssqlConnectionReader();
        const mssqlConnections = reader.getImportableConnections();
        if (mssqlConnections.length > 0 && state.profilerClient) {
          const choice = await vscode.window.showInformationMessage(
            `Light Query Profiler detected ${mssqlConnections.length} connection(s) in the MSSQL extension. Would you like to import them?`,
            "Import All",
            "Not Now",
          );
          if (choice === "Import All") {
            // Per Issue 5: we intentionally skip isMssqlExtensionAvailable()
            // here because the MSSQL extension uses lazy activation and
            // extension.isActive may return false even though it is installed
            // and has connections.  getImportableConnections() already reads
            // from vscode.workspace.getConfiguration('mssql') which works
            // regardless of activation state.
            await importMssqlConnections(
              mssqlConnections,
              state.profilerClient,
              context,
              log,
            );
          }
        }
      }
    })();

    // -- Register import-from-mssql command ---------------------------
    const importFromMssqlCommand = vscode.commands.registerCommand(
      "lightQueryProfiler.importFromMssql",
      async () => {
        if (!state.profilerClient) {
          await vscode.window.showErrorMessage(
            "Light Query Profiler is not initialized.",
          );
          return;
        }
        const reader = new MssqlConnectionReader();
        // Per Issue 5: we intentionally skip isMssqlExtensionAvailable()
        // because the MSSQL extension uses lazy activation and
        // extension.isActive may return false even though it is installed
        // and has connections.  getImportableConnections() reads from
        // vscode.workspace.getConfiguration('mssql') which works regardless
        // of activation state.
        const connections = reader.getImportableConnections();
        if (connections.length === 0) {
          // Distinguish between "extension not installed" (no mssql config
          // section at all) and "installed but no connections configured".
          const extension = vscode.extensions.getExtension("ms-mssql.mssql");
          if (extension) {
            await vscode.window.showInformationMessage(
              "No connections found in the MSSQL extension.",
            );
          } else {
            await vscode.window.showWarningMessage(
              "The MSSQL extension (ms-mssql.mssql) is not installed.",
            );
          }
          return;
        }
        const choice = await vscode.window.showInformationMessage(
          `Found ${connections.length} connection(s) in the MSSQL extension. Import them?`,
          "Import All",
          "Cancel",
        );
        if (choice === "Import All") {
          await importMssqlConnections(
            connections,
            state.profilerClient,
            context,
            log,
          );
        }
      },
    );
    context.subscriptions.push(importFromMssqlCommand);

    // Register remaining disposables
    context.subscriptions.push(
      state.outputChannel,
      {
        dispose: async () => {
          if (state.profilerPanelProvider) {
            log.info("Disposing profiler panel provider...");
            await state.profilerPanelProvider.dispose();
          }
        },
      },
      {
        dispose: () => {
          if (state.connectionsPanelProvider) {
            log.info("Disposing connections panel provider...");
            state.connectionsPanelProvider.dispose();
          }
        },
      },
      {
        dispose: () => {
          if (state.profilerClient) {
            log.info("Disposing profiler client...");
            state.profilerClient.dispose();
          }
        },
      },
    );

    activationReady = true;
    log.info("Light Query Profiler extension activated successfully");

    // Show welcome message only on first activation
    const hasShownWelcomeMessage = context.globalState.get<boolean>(
      "hasShownWelcomeMessage",
      false,
    );
    if (!hasShownWelcomeMessage) {
      void vscode.window
        .showInformationMessage(
          "Light Query Profiler is ready! Run 'Show SQL Profiler' command to open the profiler.",
        )
        .then(() => {
          // Mark as shown after user dismisses or acknowledges the message
          void context.globalState.update("hasShownWelcomeMessage", true);
        });
    }
  } catch (error) {
    activationReady = true; // Stop the deferred-panel polling
    const errorMessage = error instanceof Error ? error.message : String(error);
    const stackTrace = error instanceof Error ? error.stack : undefined;

    log.error(`Activation failed: ${errorMessage}`);
    if (stackTrace) {
      log.error(`Stack trace: ${stackTrace}`);
    }

    await vscode.window
      .showErrorMessage(
        `Failed to activate Light Query Profiler: ${errorMessage}`,
        "View Logs",
      )
      .then((selection) => {
        if (selection === "View Logs" && state.outputChannel) {
          state.outputChannel.show();
        }
      });

    throw error;
  }
}

/**
 * Deactivates the extension
 * @remarks Called when VS Code is shutting down or the extension is being disabled
 */
export async function deactivate(): Promise<void> {
  const log: Logger = state.outputChannel
    ? createLogger(state.outputChannel)
    : {
        info: (_message: string) => {
          /* No-op: extension is shutting down */
        },
        warn: (_message: string) => {
          /* No-op: extension is shutting down */
        },
        error: (_message: string) => {
          /* No-op: extension is shutting down */
        },
      };

  log.info("Deactivating Light Query Profiler extension...");

  // Cleanup is primarily handled by context.subscriptions dispose
  // But we ensure proper cleanup order here
  try {
    if (state.profilerPanelProvider) {
      await state.profilerPanelProvider.dispose();
      state.profilerPanelProvider = undefined;
    }

    if (state.profilerClient) {
      state.profilerClient.dispose();
      state.profilerClient = undefined;
    }
  } catch (error) {
    log.error(`Error during deactivation: ${String(error)}`);
  }

  if (state.outputChannel) {
    log.info("Light Query Profiler extension deactivated");
    state.outputChannel.dispose();
    state.outputChannel = undefined;
  }
}

/**
 * Gets the path to the JSON-RPC server DLL
 * @param context - Extension context providing the extension path
 * @param log - Logger instance for diagnostic output
 * @returns Path to the server DLL or undefined if not found
 * @remarks Searches multiple possible locations in order of preference
 */
function getServerDllPath(
  context: vscode.ExtensionContext,
  log: Logger,
): string | undefined {
  const possiblePaths: ReadonlyArray<string> = [
    path.join(context.extensionPath, "bin", "LightQueryProfiler.JsonRpc.dll"),
    path.join(
      context.extensionPath,
      "server",
      "LightQueryProfiler.JsonRpc.dll",
    ),
    path.join(
      context.extensionPath,
      "dist",
      "server",
      "LightQueryProfiler.JsonRpc.dll",
    ),
  ];

  log.info("Searching for server DLL in the following paths:");
  for (const dllPath of possiblePaths) {
    log.info(`  - ${dllPath}`);
    try {
      if (fs.existsSync(dllPath)) {
        log.info(`  ✓ Found at: ${dllPath}`);
        return dllPath;
      }
    } catch (error) {
      log.warn(`  ✗ Error checking path: ${String(error)}`);
    }
  }

  log.error("Server DLL not found in any expected location");
  return undefined;
}

/**
 * Gets the path to the dotnet executable
 * @param log - Logger instance for diagnostic output
 * @returns Path to dotnet executable (typically just "dotnet")
 * @remarks Falls back to "dotnet" if verification fails, letting the OS resolve the path
 */
async function getDotnetPath(log: Logger): Promise<string> {
  // Try to find dotnet in PATH
  const dotnetPath = await findDotnetInPath(log);
  if (dotnetPath) {
    return dotnetPath;
  }

  // Default to 'dotnet' and let the OS resolve it
  log.warn("Could not verify dotnet installation, using 'dotnet' as default");
  return "dotnet";
}

/**
 * Finds dotnet executable in PATH
 * @param log - Logger instance for diagnostic output
 * @returns Path to dotnet or undefined if not found
 * @remarks Attempts to execute 'dotnet --version' to verify availability
 */
async function findDotnetInPath(log: Logger): Promise<string | undefined> {
  try {
    const { exec } = await import("child_process");
    const { promisify } = await import("util");
    const execAsync = promisify(exec);

    log.info("Checking for dotnet installation...");
    const { stdout } = await execAsync("dotnet --version");
    const version = stdout.trim();
    log.info(`Found dotnet version: ${version}`);
    return "dotnet";
  } catch (error) {
    log.warn(`dotnet not found in PATH: ${String(error)}`);
    return undefined;
  }
}

/**
 * Creates a logger wrapper around the output channel
 * @param channel - VS Code output channel for logging
 * @returns Logger object with info, warn, and error methods
 * @remarks All log entries include ISO 8601 timestamps for debugging
 */
function createLogger(channel: vscode.OutputChannel): Logger {
  return {
    info: (message: string) => {
      const timestamp = new Date().toISOString();
      channel.appendLine(`[${timestamp}] [INFO] ${message}`);
    },
    warn: (message: string) => {
      const timestamp = new Date().toISOString();
      channel.appendLine(`[${timestamp}] [WARN] ${message}`);
    },
    error: (message: string) => {
      const timestamp = new Date().toISOString();
      channel.appendLine(`[${timestamp}] [ERROR] ${message}`);
    },
  };
}
