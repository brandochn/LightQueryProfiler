import * as vscode from 'vscode';
import { spawn, ChildProcess } from 'child_process';
import {
  StreamMessageReader,
  StreamMessageWriter,
  createMessageConnection,
  MessageConnection,
  RequestType,
} from 'vscode-jsonrpc/node';
import { ProfilerEvent } from '../models/profiler-event';
import {
  ConnectionSettings,
  toConnectionString,
  getEngineType,
} from '../models/connection-settings';

/**
 * Request parameters for starting a profiling session
 * @remarks Sent to the JSON-RPC server to initiate profiling
 */
interface StartProfilingRequest {
  readonly sessionName: string;
  readonly engineType: number;
  readonly connectionString: string;
}

/**
 * Request parameters for stopping a profiling session
 * @remarks Sent to the JSON-RPC server to terminate an active session
 */
interface StopProfilingRequest {
  readonly sessionName: string;
}

/**
 * Request parameters for getting events
 * @remarks Sent to retrieve captured events from an active session
 */
interface GetEventsRequest {
  readonly sessionName: string;
}

/**
 * JSON-RPC request types
 * @remarks These define the contract between the client and the JSON-RPC server
 */
const startProfilingRequestType = new RequestType<
  StartProfilingRequest,
  void,
  void
>('StartProfilingAsync');
const stopProfilingRequestType = new RequestType<
  StopProfilingRequest,
  void,
  void
>('StopProfilingAsync');
const getEventsRequestType = new RequestType<
  GetEventsRequest,
  ProfilerEvent[],
  void
>('GetLastEventsAsync');

/**
 * Client state enum for tracking lifecycle
 * @remarks Forms a discriminated union for state machine implementation
 */
enum ClientState {
  Idle = 'idle',
  Starting = 'starting',
  Running = 'running',
  Stopping = 'stopping',
  Disposed = 'disposed',
}

/**
 * Client for communicating with the Light Query Profiler JSON-RPC server
 * @remarks This class manages the lifecycle of the .NET server process and JSON-RPC communication
 * @example
 * ```typescript
 * const client = new ProfilerClient(dotnetPath, serverDllPath, outputChannel);
 * await client.start();
 * await client.startProfiling('MySession', connectionSettings);
 * const events = await client.getLastEvents('MySession');
 * await client.stopProfiling('MySession');
 * client.dispose();
 * ```
 */
export class ProfilerClient {
  private serverProcess: ChildProcess | null = null;
  private connection: MessageConnection | null = null;
  private readonly outputChannel: vscode.OutputChannel;
  private readonly dotnetPath: string;
  private readonly serverDllPath: string;
  private state: ClientState = ClientState.Idle;
  private readonly activeSessions = new Set<string>();
  private onServerStoppedCallback: (() => void) | null = null;

  constructor(
    dotnetPath: string,
    serverDllPath: string,
    outputChannel: vscode.OutputChannel,
  ) {
    this.dotnetPath = dotnetPath;
    this.serverDllPath = serverDllPath;
    this.outputChannel = outputChannel;
  }

  /**
   * Starts the JSON-RPC server process
   * @throws Error if server is already running or fails to start
   * @remarks Spawns the .NET process and establishes JSON-RPC communication over stdin/stdout
   */
  public async start(): Promise<void> {
    if (this.state === ClientState.Disposed) {
      throw new Error('Client has been disposed and cannot be restarted');
    }

    if (this.state === ClientState.Running) {
      this.log('Server is already running');
      return;
    }

    if (this.state === ClientState.Starting) {
      throw new Error('Server is already starting');
    }

    this.state = ClientState.Starting;
    this.log('Starting Light Query Profiler server...');

    try {
      // Spawn the .NET process
      this.serverProcess = spawn(this.dotnetPath, [this.serverDllPath], {
        stdio: ['pipe', 'pipe', 'pipe'],
      });

      if (!this.serverProcess.stdout || !this.serverProcess.stdin) {
        throw new Error('Failed to create server process streams');
      }

      // Set up error handlers before anything else
      this.serverProcess.on('error', (error: Error) => {
        this.logError(`Server process error: ${error.message}`);
        void this.handleServerFailure(error);
      });

      // Log server stderr output and detect the READY signal
      // NOTE: waitForServerReady() registers its own one-time listener that
      // resolves on "READY" and then removes itself.  This persistent handler
      // runs in parallel and logs every stderr line for diagnostics.
      this.serverProcess.stderr?.on('data', (data: Buffer) => {
        const message = data.toString().trim();
        if (message) {
          this.log(`[Server stderr] ${message}`);
        }
      });

      // Handle server exit
      this.serverProcess.on(
        'exit',
        (code: number | null, signal: string | null) => {
          const exitInfo = signal
            ? `signal ${signal}`
            : `code ${code ?? 'null'}`;
          this.log(`Server process exited with ${exitInfo}`);
          void this.handleServerExit(code, signal);
        },
      );

      // Wait for the server to emit the READY signal on stderr before
      // attempting any JSON-RPC communication.  This prevents the
      // "Pending response rejected since connection got disposed" error
      // caused by calling startProfiling() before the .NET runtime has
      // finished JIT-compiling and reached jsonRpc.StartListening().
      await this.waitForServerReady(8000);

      // Create JSON-RPC connection
      this.connection = createMessageConnection(
        new StreamMessageReader(this.serverProcess.stdout),
        new StreamMessageWriter(this.serverProcess.stdin),
      );

      // Set up connection error handlers
      this.connection.onError((error: [Error, unknown, number | undefined]) => {
        this.logError(`Connection error: ${error[0].message}`);
        void this.handleConnectionError(error[0]);
      });

      this.connection.onClose(() => {
        this.log('JSON-RPC connection closed');
        void this.handleConnectionClose();
      });

      // Start listening for messages
      this.connection.listen();

      this.state = ClientState.Running;
      this.log('Server started successfully');
    } catch (error) {
      this.state = ClientState.Idle;
      await this.cleanup();
      const message = error instanceof Error ? error.message : String(error);
      this.logError(`Failed to start server: ${message}`);
      throw new Error(`Failed to start profiler server: ${message}`);
    }
  }

  /**
   * Starts a profiling session
   * @param sessionName - Unique name for the session
   * @param settings - Connection settings for the target database
   * @throws Error if connection is not established or request fails
   * @remarks The connection string is not logged to prevent password exposure
   */
  public async startProfiling(
    sessionName: string,
    settings: ConnectionSettings,
  ): Promise<void> {
    this.ensureRunning();
    this.validateSessionName(sessionName);

    this.log(`Starting profiling session: ${sessionName}`);

    try {
      const connectionString = toConnectionString(settings);
      const engineType = getEngineType(settings.authenticationMode);

      // Don't log the full connection string as it may contain passwords
      this.log(
        `Session: ${sessionName}, Server: ${settings.server}, Database: ${settings.database}`,
      );

      await this.connection!.sendRequest(startProfilingRequestType, {
        sessionName,
        engineType,
        connectionString,
      });

      this.activeSessions.add(sessionName);
      this.log(`Session started successfully: ${sessionName}`);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      this.logError(`Failed to start session '${sessionName}': ${message}`);
      throw new Error(`Failed to start profiling: ${message}`);
    }
  }

  /**
   * Retrieves the latest profiling events
   * @param sessionName - Session name to retrieve events from
   * @returns Array of profiler events captured since last retrieval
   * @throws Error if connection is not established or request fails
   * @remarks Events are returned in the order they were captured
   */
  public async getLastEvents(sessionName: string): Promise<ProfilerEvent[]> {
    this.ensureRunning();
    this.validateSessionName(sessionName);

    try {
      const events = await this.connection!.sendRequest(getEventsRequestType, {
        sessionName,
      });

      if (events.length > 0) {
        this.log(
          `Retrieved ${events.length} events from session: ${sessionName}`,
        );
      }

      return events;
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      this.logError(`Failed to get events from '${sessionName}': ${message}`);
      throw new Error(`Failed to get events: ${message}`);
    }
  }

  /**
   * Stops a profiling session
   * @param sessionName - Session name to stop
   * @throws Error if connection is not established or request fails
   * @remarks Removes session from active sessions even if server-side stop fails
   */
  public async stopProfiling(sessionName: string): Promise<void> {
    this.ensureRunning();
    this.validateSessionName(sessionName);

    this.log(`Stopping profiling session: ${sessionName}`);

    try {
      await this.connection!.sendRequest(stopProfilingRequestType, {
        sessionName,
      });

      this.activeSessions.delete(sessionName);
      this.log(`Session stopped successfully: ${sessionName}`);
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      this.logError(`Failed to stop session '${sessionName}': ${message}`);
      // Still remove from active sessions even if stop fails
      this.activeSessions.delete(sessionName);
      throw new Error(`Failed to stop profiling: ${message}`);
    }
  }

  /**
   * Checks if the server is running
   * @returns True if server is running
   */
  public isRunning(): boolean {
    return this.state === ClientState.Running && this.connection !== null;
  }

  /**
   * Registers a callback that is invoked when the server stops unexpectedly.
   * @param callback - Function to call when the server crashes or exits abnormally.
   * @remarks The callback is called after cleanup() completes so the client is
   *   already in Idle state by the time the callback runs.
   */
  public setOnServerStopped(callback: () => void): void {
    this.onServerStoppedCallback = callback;
  }

  /**
   * Gets the current client state
   * @returns Current state
   */
  public getState(): ClientState {
    return this.state;
  }

  /**
   * Gets the set of active session names
   * @returns Set of active session names
   */
  public getActiveSessions(): ReadonlySet<string> {
    return this.activeSessions;
  }

  /**
   * Disposes the client and stops the server
   * @remarks Safe to call multiple times; subsequent calls are no-ops
   */
  public dispose(): void {
    if (this.state === ClientState.Disposed) {
      return;
    }

    this.log('Disposing profiler client...');
    this.state = ClientState.Disposed;
    void this.cleanup();
  }

  /**
   * Waits until the server process emits the "READY" signal on stderr
   * @param timeoutMs - Maximum milliseconds to wait before giving up (default: 8000)
   * @returns Promise that resolves when READY is received or rejects on timeout/crash
   * @remarks The .NET server writes "READY" to stderr immediately after
   *   jsonRpc.StartListening() succeeds.  Waiting for this signal prevents
   *   "Pending response rejected since connection got disposed" errors that
   *   occur when JSON-RPC calls are sent before the server is ready to handle them.
   */
  private waitForServerReady(timeoutMs: number): Promise<void> {
    return new Promise<void>((resolve, reject) => {
      const timer = setTimeout(() => {
        // eslint-disable-next-line @typescript-eslint/no-use-before-define
        cleanup();
        reject(
          new Error(
            `Server did not become ready within ${timeoutMs}ms. ` +
            'Check the Output panel for server startup errors.',
          ),
        );
      }, timeoutMs);

      const cleanup = (): void => {
        clearTimeout(timer);
        this.serverProcess?.stderr?.off('data', onData);
        this.serverProcess?.off('exit', onExit);
      };

      const onData = (data: Buffer): void => {
        if (data.toString().includes('READY')) {
          this.log('Server ready signal received');
          cleanup();
          resolve();
        }
      };

      const onExit = (code: number | null): void => {
        cleanup();
        reject(
          new Error(
            `Server process exited (code ${code ?? 'null'}) before becoming ready. ` +
            'Check the Output panel for server startup errors.',
          ),
        );
      };

      this.serverProcess?.stderr?.on('data', onData);
      this.serverProcess?.once('exit', onExit);
    });
  }

  /**
   * Ensures the client is in a running state
   * @throws Error if not running or disposed
   * @remarks Used as a guard clause at the start of operations requiring an active connection
   */
  private ensureRunning(): void {
    if (this.state === ClientState.Disposed) {
      throw new Error('Client has been disposed');
    }

    if (
      !this.connection ||
      !this.serverProcess ||
      this.state !== ClientState.Running
    ) {
      throw new Error(
        'Server is not running. Call start() first and ensure it completed successfully.',
      );
    }
  }

  /**
   * Validates session name
   * @throws Error if session name is invalid
   */
  private validateSessionName(sessionName: string): void {
    if (!sessionName || sessionName.trim().length === 0) {
      throw new Error('Session name cannot be empty');
    }
  }

  /**
   * Handles server process failure
   */
  private async handleServerFailure(error: Error): Promise<void> {
    this.logError(`Server process failed: ${error.message}`);
    await this.cleanup();

    if (this.state !== ClientState.Disposed) {
      await vscode.window.showErrorMessage(
        `Profiler server failed: ${error.message}. Check the Output panel for details.`,
      );
    }
  }

  /**
   * Handles server process exit
   */
  private async handleServerExit(
    code: number | null,
    signal: string | null,
  ): Promise<void> {
    // Normal exit
    if (code === 0 && this.state === ClientState.Stopping) {
      this.log('Server exited normally');
      return;
    }

    const exitInfo = signal
      ? `signal ${signal}`
      : `code ${code ?? 'unknown'}`;

    // Exit during startup — waitForServerReady() will reject via its own onExit
    // listener, which triggers cleanup() in start()'s catch block.  We only need
    // to log here; no further action is required to avoid double-cleanup.
    if (this.state === ClientState.Starting) {
      this.logError(`Server exited during startup with ${exitInfo}`);
      return;
    }

    // Abnormal exit while running
    if (this.state === ClientState.Running) {
      this.logError(`Server exited unexpectedly with ${exitInfo}`);

      const hadActiveSessions = this.activeSessions.size > 0;
      await this.cleanup();

      if (hadActiveSessions) {
        await vscode.window.showWarningMessage(
          'Profiler server stopped unexpectedly. Active profiling sessions have been terminated.',
        );
      }

      this.onServerStoppedCallback?.();
    }
  }

  /**
   * Handles connection errors
   */
  private async handleConnectionError(error: Error): Promise<void> {
    if (this.state !== ClientState.Disposed) {
      await vscode.window.showErrorMessage(
        `Profiler connection error: ${error.message}`,
      );
    }
  }

  /**
   * Handles connection close
   */
  private async handleConnectionClose(): Promise<void> {
    if (this.state === ClientState.Running) {
      this.log('Connection closed unexpectedly');
      await this.cleanup();
      this.onServerStoppedCallback?.();
    }
  }

  /**
   * Cleans up resources
   * @remarks Implements graceful shutdown with SIGTERM followed by SIGKILL after 2s timeout
   */
  private async cleanup(): Promise<void> {
    const previousState = this.state;
    this.state = ClientState.Stopping;

    // Clear active sessions
    this.activeSessions.clear();

    // Dispose connection
    if (this.connection !== null) {
      try {
        this.connection.dispose();
      } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        this.logError(`Error disposing connection: ${message}`);
      }
      this.connection = null;
    }

    // Kill server process
    if (this.serverProcess !== null) {
      try {
        if (!this.serverProcess.killed) {
          this.serverProcess.kill('SIGTERM');

          // Give it a moment to exit gracefully
          await new Promise<void>((resolve) => {
            const timeout = setTimeout(() => {
              if (this.serverProcess && !this.serverProcess.killed) {
                this.log('Force killing server process');
                this.serverProcess.kill('SIGKILL');
              }
              resolve();
            }, 2000);

            this.serverProcess?.once('exit', () => {
              clearTimeout(timeout);
              resolve();
            });
          });
        }
      } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        this.logError(`Error killing server process: ${message}`);
      }
      this.serverProcess = null;
    }

    // Update state
    if (previousState !== ClientState.Disposed) {
      this.state = ClientState.Idle;
    }

    this.log('Cleanup completed');
  }

  /**
   * Logs an informational message
   * @param message - Message to log
   * @remarks Includes timestamp and component prefix for debugging
   */
  private log(message: string): void {
    const timestamp = new Date().toISOString();
    this.outputChannel.appendLine(`[${timestamp}] [ProfilerClient] ${message}`);
  }

  /**
   * Logs an error message
   * @param message - Error message to log
   * @remarks Includes timestamp, component prefix, and ERROR level indicator
   */
  private logError(message: string): void {
    const timestamp = new Date().toISOString();
    this.outputChannel.appendLine(
      `[${timestamp}] [ProfilerClient] ERROR: ${message}`,
    );
  }
}
