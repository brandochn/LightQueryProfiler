import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import { ProfilerPanelProvider } from './views/profiler-panel-provider';
import { ProfilerClient } from './services/profiler-client';

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
    'Light Query Profiler',
  );
  const log = createLogger(state.outputChannel);

  log.info('Activating Light Query Profiler extension...');

  // IMPORTANT: Register the command IMMEDIATELY — before any awaits.
  // VS Code may dispatch the command while activate() is still running its
  // async initialization (getDotnetPath does execAsync ~200ms).  If the
  // command handler is not yet registered at that point the invocation is
  // silently swallowed, which is why the panel sometimes does not open.
  // The handler checks whether the provider is ready and either shows the
  // panel or queues a retry once initialization completes.
  let activationReady = false;
  const showProfilerCommand = vscode.commands.registerCommand(
    'lightQueryProfiler.showProfiler',
    () => {
      log.info('Show SQL Profiler command executed');
      if (state.profilerPanelProvider) {
        state.profilerPanelProvider.showPanel();
      } else if (!activationReady) {
        // Extension is still initializing — wait for it then open the panel
        log.info('Provider not ready yet, deferring panel open...');
        const deferredInterval = setInterval(() => {
          if (state.profilerPanelProvider) {
            clearInterval(deferredInterval);
            clearTimeout(deferredTimeout);
            log.info('Provider ready, opening deferred panel');
            state.profilerPanelProvider.showPanel();
          }
        }, 50);
        // Safety: stop polling after 10 s regardless
        // eslint-disable-next-line prefer-const
        const deferredTimeout = setTimeout(() => clearInterval(deferredInterval), 10_000);
        // Register both handles so they are cancelled if the extension is
        // deactivated within the 10-second initialization window.
        context.subscriptions.push({
          dispose: () => {
            clearInterval(deferredInterval);
            clearTimeout(deferredTimeout);
          },
        });
      } else {
        log.error('Profiler panel provider not initialized');
        void vscode.window.showErrorMessage(
          'Failed to open SQL Profiler. Please reload the window.',
        );
      }
    },
  );
  context.subscriptions.push(showProfilerCommand);

  try {
    // Get server DLL path and dotnet path in parallel (no duplicate dotnet check)
    const serverDllPath = getServerDllPath(context, log);
    if (!serverDllPath) {
      const message = 'Light Query Profiler server not found.';
      log.error(message);
      activationReady = true;
      await vscode.window.showErrorMessage(message, 'Error');
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

    // Register remaining disposables
    context.subscriptions.push(
      state.outputChannel,
      {
        dispose: async () => {
          if (state.profilerPanelProvider) {
            log.info('Disposing profiler panel provider...');
            await state.profilerPanelProvider.dispose();
          }
        },
      },
      {
        dispose: () => {
          if (state.profilerClient) {
            log.info('Disposing profiler client...');
            state.profilerClient.dispose();
          }
        },
      },
    );

    activationReady = true;
    log.info('Light Query Profiler extension activated successfully');

    // Show welcome message (fire-and-forget — do not await so activate() returns immediately)
    void vscode.window.showInformationMessage(
      "Light Query Profiler is ready! Run 'Show SQL Profiler' command to open the profiler.",
    );
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
        'View Logs',
      )
      .then((selection) => {
        if (selection === 'View Logs' && state.outputChannel) {
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

  log.info('Deactivating Light Query Profiler extension...');

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
    log.info('Light Query Profiler extension deactivated');
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
    path.join(context.extensionPath, 'bin', 'LightQueryProfiler.JsonRpc.dll'),
    path.join(
      context.extensionPath,
      'server',
      'LightQueryProfiler.JsonRpc.dll',
    ),
    path.join(
      context.extensionPath,
      'dist',
      'server',
      'LightQueryProfiler.JsonRpc.dll',
    ),
  ];

  log.info('Searching for server DLL in the following paths:');
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

  log.error('Server DLL not found in any expected location');
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
  return 'dotnet';
}

/**
 * Finds dotnet executable in PATH
 * @param log - Logger instance for diagnostic output
 * @returns Path to dotnet or undefined if not found
 * @remarks Attempts to execute 'dotnet --version' to verify availability
 */
async function findDotnetInPath(log: Logger): Promise<string | undefined> {
  try {
    const { exec } = await import('child_process');
    const { promisify } = await import('util');
    const execAsync = promisify(exec);

    log.info('Checking for dotnet installation...');
    const { stdout } = await execAsync('dotnet --version');
    const version = stdout.trim();
    log.info(`Found dotnet version: ${version}`);
    return 'dotnet';
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
