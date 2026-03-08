import * as vscode from "vscode";
import * as path from "path";
import * as fs from "fs";
import { ProfilerPanelProvider } from "./views/profiler-panel-provider";
import { ProfilerClient } from "./services/profiler-client";

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
  outputChannel: vscode.OutputChannel | undefined;
}

/**
 * Global extension state
 */
const state: ExtensionState = {
  profilerClient: undefined,
  profilerPanelProvider: undefined,
  outputChannel: undefined,
};

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

  try {
    // Verify prerequisites
    await verifyPrerequisites(log);

    // Get server DLL path
    const serverDllPath = getServerDllPath(context, log);
    if (!serverDllPath) {
      const message =
        "Light Query Profiler server not found. Please run the setup script or check TROUBLESHOOTING.md";
      log.error(message);
      await vscode.window
        .showErrorMessage(message, "Open Troubleshooting")
        .then((selection) => {
          if (selection === "Open Troubleshooting") {
            void vscode.commands.executeCommand(
              "markdown.showPreview",
              vscode.Uri.file(
                path.join(context.extensionPath, "TROUBLESHOOTING.md"),
              ),
            );
          }
        });
      return;
    }

    log.info(`Server DLL path: ${serverDllPath}`);

    // Get dotnet path
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

    // Register commands
    const showProfilerCommand = vscode.commands.registerCommand(
      "lightQueryProfiler.showProfiler",
      () => {
        log.info("Show SQL Profiler command executed");
        if (state.profilerPanelProvider) {
          state.profilerPanelProvider.showPanel();
        } else {
          log.error("Profiler panel provider not initialized");
          void vscode.window.showErrorMessage(
            "Failed to open SQL Profiler. Please reload the window.",
          );
        }
      },
    );

    // Register disposables
    context.subscriptions.push(
      showProfilerCommand,
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
          if (state.profilerClient) {
            log.info("Disposing profiler client...");
            state.profilerClient.dispose();
          }
        },
      },
    );

    log.info("Light Query Profiler extension activated successfully");

    // Show welcome message
    await vscode.window.showInformationMessage(
      "Light Query Profiler is ready! Run 'Show SQL Profiler' command to open the profiler.",
    );
  } catch (error) {
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
 * Verifies that prerequisites are installed
 * @param log - Logger instance for diagnostic output
 * @throws Error if prerequisites are not met
 * @remarks Currently only checks for .NET runtime availability
 */
async function verifyPrerequisites(log: Logger): Promise<void> {
  // Check if .NET is available
  try {
    const dotnetPath = await getDotnetPath(log);
    if (!dotnetPath) {
      throw new Error(".NET runtime not found in PATH");
    }
  } catch (error) {
    throw new Error(
      ".NET 10 SDK or runtime is required. Please install from https://dotnet.microsoft.com/download",
    );
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
