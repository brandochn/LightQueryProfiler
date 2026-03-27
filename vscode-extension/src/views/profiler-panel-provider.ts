import * as vscode from 'vscode';
import * as path from 'path';
import { ProfilerClient } from '../services/profiler-client';
import {
  EventExportImportService,
  DisplayEvent,
} from '../services/event-export-import.service';
import {
  AuthenticationMode,
  getAllAuthenticationModes,
} from '../models/authentication-mode';
import { validateConnectionSettings } from '../models/connection-settings';
import { ProfilerEvent } from '../models/profiler-event';

/**
 * Connection settings for SQL Server/Azure SQL
 */
interface ConnectionSettings {
  server: string;
  database: string;
  authenticationMode: AuthenticationMode;
  username?: string;
  password?: string;
}

/**
 * Profiler state enumeration
 */
enum ProfilerState {
  Stopped = 'stopped',
  Running = 'running',
  Paused = 'paused',
}

/**
 * Filter criteria for profiler events — mirrors WinForms EventFilter model.
 * All fields are optional substrings (case-insensitive contains match, AND logic).
 */
interface EventFilter {
  eventClass: string;
  textData: string;
  applicationName: string;
  ntUserName: string;
  loginName: string;
  databaseName: string;
}

/**
 * Message types sent from webview to extension
 */
interface WebviewIncomingMessage {
  command:
    | 'start'
    | 'stop'
    | 'pause'
    | 'resume'
    | 'clear'
    | 'applyFilters'
    | 'clearFilters'
    | 'exportEvents'
    | 'importEvents'
    | 'webviewReady';
  data?: ConnectionSettings | EventFilter;
}

/**
 * Message types sent from extension to webview
 */
interface WebviewOutgoingMessage {
  command:
    | 'updateState'
    | 'updateEventCount'
    | 'addEvents'
    | 'clearEvents'
    | 'updateFilter'
    | 'error'
    | 'setConnectionFieldsEnabled'
    | 'loadImportedEvents';
  data?: unknown;
}

/**
 * Provider for the profiler webview panel
 * @remarks Manages the webview UI lifecycle and communication with the profiler client
 * @example
 * ```typescript
 * const provider = new ProfilerPanelProvider(extensionUri, profilerClient, outputChannel);
 * provider.showPanel();
 * ```
 */
export class ProfilerPanelProvider {
  private panel: vscode.WebviewPanel | undefined;
  private readonly profilerClient: ProfilerClient;
  private readonly extensionUri: vscode.Uri;
  private readonly outputChannel: vscode.OutputChannel;
  private sessionName = 'VSCodeProfilerSession';
  private state: ProfilerState = ProfilerState.Stopped;
  private pollingInterval: NodeJS.Timeout | null = null;
  private readonly pollingIntervalMs = 900; // Match WinForms implementation
  private eventCount = 0;
  private readonly sessionEventKeys = new Set<string>();
  private eventFilter: EventFilter = {
    eventClass: '',
    textData: '',
    applicationName: '',
    ntUserName: '',
    loginName: '',
    databaseName: '',
  };

  /**
   * Host-side mirror of the webview's `allEvents` array.
   * Populated in `pollEvents()` (post-filter) and replaced on import.
   * Cleared in `handleStart()` and `handleClear()` to stay in sync with the webview.
   * Capped at `maxCapturedEvents` to prevent unbounded memory growth in the host.
   */
  private capturedEvents: DisplayEvent[] = [];

  /**
   * Maximum number of events kept in `capturedEvents`.
   * Matches the `MAX_EVENTS` cap used by the webview's `allEvents` array.
   */
  private static readonly maxCapturedEvents = 10_000;

  /**
   * Events waiting to be sent to the webview after it signals readiness via `webviewReady`.
   * Set by `importEvents()` when the panel is not yet open; cleared by the
   * `webviewReady` handler once the data has been forwarded.
   */
  private pendingImportEvents: DisplayEvent[] | null = null;

  constructor(
    extensionUri: vscode.Uri,
    profilerClient: ProfilerClient,
    outputChannel: vscode.OutputChannel,
  ) {
    this.extensionUri = extensionUri;
    this.profilerClient = profilerClient;
    this.outputChannel = outputChannel;

    // React to unexpected server crashes so the UI is updated immediately
    // instead of silently continuing to poll against a dead connection.
    this.profilerClient.setOnServerStopped(() => {
      void this.handleServerCrash();
    });
  }

  /**
   * Shows the profiler panel
   * @remarks Creates a new panel or reveals existing one
   */
  public showPanel(): void {
    const column = vscode.ViewColumn.One;

    // If panel already exists, reveal it
    if (this.panel) {
      this.panel.reveal(column);
      return;
    }

    // Create new panel
    this.panel = vscode.window.createWebviewPanel(
      'lightQueryProfiler',
      'Light Query Profiler',
      column,
      {
        enableScripts: true,
        retainContextWhenHidden: true,
        localResourceRoots: [this.extensionUri],
      },
    );

    // Set HTML content
    this.panel.webview.html = this.getHtmlContent(this.panel.webview);

    // Set icon — use a dedicated small SVG (icon-small.svg, 16×16 viewBox)
    // for the panel tab. The main icon.png is used by the Marketplace and the
    // activity-bar entry (via package.json). Using a separate file avoids the
    // issue where the detailed 128×128 design becomes unrecognisable when
    // VS Code renders it at ~16 px in the editor tab strip.
    this.panel.iconPath = {
      light: vscode.Uri.joinPath(this.extensionUri, 'media', 'icon-small.svg'),
      dark: vscode.Uri.joinPath(this.extensionUri, 'media', 'icon-small.svg'),
    };

    // Handle messages from webview
    this.panel.webview.onDidReceiveMessage(
      async (message: WebviewIncomingMessage) => {
        await this.handleMessage(message);
      },
      undefined,
    );

    // Handle panel disposal
    this.panel.onDidDispose(() => {
      this.log('Panel disposed');
      // Set panel to undefined first so postMessage becomes a no-op during cleanup.
      this.panel = undefined;
      if (this.state !== ProfilerState.Stopped) {
        // Stop polling and terminate the XEvent session on SQL Server so it
        // is not orphaned when the user closes the panel tab.
        void this.handleStop().catch((err) => {
          this.logError(
            `Error stopping profiler on panel dispose: ${String(err)}`,
          );
        });
      } else {
        this.stopPolling();
      }
    }, undefined);

    this.log('Panel created and shown');
  }

  /**
   * Handles incoming messages from the webview
   * @param message - Message from webview
   * @remarks Routes commands to appropriate handlers
   */
  private async handleMessage(message: WebviewIncomingMessage): Promise<void> {
    this.log(`Received message: ${message.command}`);

    try {
      switch (message.command) {
        case 'start':
          if (message.data && this.isConnectionSettings(message.data)) {
            await this.handleStart(message.data);
          } else {
            await this.showError('Invalid connection settings');
          }
          break;
        case 'stop':
          await this.handleStop();
          break;
        case 'pause':
          await this.handlePause();
          break;
        case 'resume':
          await this.handleResume();
          break;
        case 'clear':
          await this.handleClear();
          break;
        case 'applyFilters':
          if (message.data && this.isEventFilter(message.data)) {
            await this.handleApplyFilters(message.data);
          }
          break;
        case 'clearFilters':
          await this.handleClearFilters();
          break;
        case 'exportEvents':
          await this.exportEvents();
          break;
        case 'importEvents':
          await this.importEvents();
          break;
        case 'webviewReady':
          // If importEvents() stored pending data while the panel was opening,
          // forward it now that the webview has signalled it is ready.
          if (this.pendingImportEvents) {
            const pending = this.pendingImportEvents;
            this.pendingImportEvents = null;
            await this.postMessage({
              command: 'loadImportedEvents',
              data: pending,
            });
          }
          break;
        default:
          this.logError(`Unknown command: ${String(message.command)}`);
      }
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : String(error);
      this.logError(
        `Error handling message '${String(message.command)}': ${errorMessage}`,
      );
      await this.showError(`Command failed: ${errorMessage}`);
    }
  }

  /**
   * Handles start profiling command
   * @param settings - Connection settings for SQL Server/Azure SQL
   * @remarks Validates connection, starts server session, and begins polling
   */
  private async handleStart(settings: ConnectionSettings): Promise<void> {
    this.log('Starting profiling session...');

    // Validate connection settings before attempting to connect.
    // This mirrors WinForms ConfigureAsync which throws InvalidOperationException
    // when required fields (e.g., database for Azure SQL) are missing.
    const validationError = validateConnectionSettings(settings);
    if (validationError) {
      await this.showError(validationError);
      return;
    }

    try {
      // Ensure the .NET server process is running before calling startProfiling
      if (!this.profilerClient.isRunning()) {
        this.log('Server not running, starting server process...');
        await this.profilerClient.start();
      }

      // Start profiling
      await this.profilerClient.startProfiling(this.sessionName, settings);

      // Clear previous events before showing new session results
      this.eventCount = 0;
      this.sessionEventKeys.clear();
      this.capturedEvents = [];
      await this.postMessage({ command: 'clearEvents' });

      // Update state and disable connection fields while profiling is active
      this.state = ProfilerState.Running;
      await this.setConnectionFieldsEnabled(false);
      await this.updateState();

      // Start polling for events
      this.startPolling();

      this.log('Profiling started successfully');
      await vscode.window.showInformationMessage('Profiling started');
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : String(error);
      this.logError(`Failed to start profiling: ${errorMessage}`);
      this.state = ProfilerState.Stopped;
      await this.setConnectionFieldsEnabled(true);
      await this.updateState();
      await this.showError(`Failed to start profiling: ${errorMessage}`);
    }
  }

  /**
   * Updates the webview state
   * @remarks Sends current state and event count to webview
   */
  private async updateState(): Promise<void> {
    await this.postMessage({
      command: 'updateState',
      data: {
        state: this.state,
        eventCount: this.eventCount,
      },
    });
  }

  /**
   * Enables or disables connection settings form fields in the webview
   * @param enabled - True to enable fields (profiling stopped), false to disable them (profiling active)
   * @remarks Fields are disabled while profiling is running or starting to prevent
   * the user from modifying connection settings mid-session
   */
  private async setConnectionFieldsEnabled(enabled: boolean): Promise<void> {
    await this.postMessage({
      command: 'setConnectionFieldsEnabled',
      data: enabled,
    });
  }

  /**
   * Type guard for connection settings
   * @param data - Unknown data to validate
   * @returns True if data has ConnectionSettings structure
   * @remarks Validates required properties: server, database, authenticationMode
   */
  private isConnectionSettings(data: unknown): data is ConnectionSettings {
    if (typeof data !== 'object' || data === null) {
      return false;
    }

    const obj = data as Record<string, unknown>;
    return (
      typeof obj.server === 'string' &&
      typeof obj.database === 'string' &&
      typeof obj.authenticationMode === 'number'
    );
  }

  /**
   * Handles stop profiling command
   * @remarks Stops polling, terminates server session, and resets state
   */
  private async handleStop(): Promise<void> {
    this.log('Stopping profiling session...');
    this.stopPolling();

    if (this.profilerClient.isRunning()) {
      await this.profilerClient.stopProfiling(this.sessionName);
    }

    this.state = ProfilerState.Stopped;
    await this.setConnectionFieldsEnabled(true);
    await this.updateState();

    this.log('Profiling stopped');
    await vscode.window.showInformationMessage('Profiling stopped');
  }

  /**
   * Handles pause profiling command
   * @remarks Pauses client-side polling without stopping the server session
   */
  private async handlePause(): Promise<void> {
    this.stopPolling();
    this.state = ProfilerState.Paused;
    await this.updateState();
  }

  /**
   * Handles resume profiling command
   * @remarks Resumes client-side polling of an active server session
   */
  private async handleResume(): Promise<void> {
    this.state = ProfilerState.Running;
    await this.updateState();
    this.startPolling();
  }

  /**
   * Handles clear events command
   * @remarks Clears local event cache and resets event count without stopping profiling
   */
  private async handleClear(): Promise<void> {
    this.log('Clearing events');
    this.eventCount = 0;
    this.capturedEvents = [];
    // sessionEventKeys intentionally NOT cleared — session cache must survive Clear
    // so that already-seen ring_buffer events cannot re-appear after a clear.
    await this.postMessage({
      command: 'clearEvents',
    });
  }

  /**
   * Applies event filters — takes effect on the next poll cycle.
   * Works regardless of profiling state (before start, while running, while paused, after stop).
   * @param filter - Filter criteria to apply
   */
  private async handleApplyFilters(filter: EventFilter): Promise<void> {
    this.eventFilter = filter;
    this.log(`Filters applied: ${JSON.stringify(filter)}`);
    await this.postMessage({ command: 'updateFilter', data: filter });
  }

  /**
   * Clears all active filters. Future events are captured without any filter.
   * Already-displayed events in the table are NOT removed.
   */
  private async handleClearFilters(): Promise<void> {
    this.eventFilter = {
      eventClass: '',
      textData: '',
      applicationName: '',
      ntUserName: '',
      loginName: '',
      databaseName: '',
    };
    this.log('Filters cleared');
    await this.postMessage({ command: 'updateFilter', data: this.eventFilter });
  }

  /**
   * Type guard — checks that a message data object is a valid EventFilter
   */
  private isEventFilter(data: unknown): data is EventFilter {
    return (
      typeof data === 'object' &&
      data !== null &&
      'eventClass' in data &&
      'textData' in data &&
      'applicationName' in data &&
      'ntUserName' in data &&
      'loginName' in data &&
      'databaseName' in data
    );
  }

  /**
   * Handles an unexpected server crash.
   * Stops polling, resets provider state to Stopped, clears the dedup cache
   * (since the server restart will issue new sequence numbers), and updates the UI.
   * @remarks Called via the onServerStopped callback registered in the constructor.
   */
  private async handleServerCrash(): Promise<void> {
    this.logError('Server stopped unexpectedly — resetting profiler state');
    this.stopPolling();
    this.state = ProfilerState.Stopped;
    // Clear dedup cache: after a server restart sequence numbers start from 1 again,
    // so stale keys would silently block all new events from being displayed.
    this.sessionEventKeys.clear();
    await this.setConnectionFieldsEnabled(true);
    await this.updateState();
  }

  /**
   * Starts polling for events
   * @remarks Polls every 900ms to match WinForms implementation timing
   */
  private startPolling(): void {
    this.stopPolling();

    this.pollingInterval = setInterval(() => {
      void this.pollEvents();
    }, this.pollingIntervalMs);
  }

  /**
   * Stops polling for events
   * @remarks Safe to call multiple times; clears existing interval if any
   */
  private stopPolling(): void {
    if (this.pollingInterval !== null) {
      clearInterval(this.pollingInterval);
      this.pollingInterval = null;
    }
  }

  /**
   * Polls for new events from the profiler service
   * @remarks Filters out previously seen events using Set-based deduplication.
   *   The try/catch wraps the entire body (including the state guard) so that any
   *   future synchronous throw before the first await cannot escape as an unhandled
   *   rejection and silently kill the polling loop.
   */
  private async pollEvents(): Promise<void> {
    try {
      // Guard: only poll while actively running. If the server crashed,
      // handleServerCrash() will have set state to Stopped and called stopPolling().
      // This check also prevents stale interval ticks from firing after stopPolling()
      // is called on a different code path (pause, stop, dispose).
      if (this.state !== ProfilerState.Running) {
        return;
      }

      const events: ProfilerEvent[] = await this.profilerClient.getLastEvents(
        this.sessionName,
      );

      if (!events || events.length === 0) {
        return;
      }

      const newEvents: DisplayEvent[] = [];

      // Helper to get a string value from fields or actions (all values come as strings from the XML parser)
      const str = (
        obj: Record<string, unknown> | undefined,
        ...keys: string[]
      ): string => {
        if (!obj) {
          return '';
        }
        for (const k of keys) {
          const v = obj[k];
          if (v !== undefined && v !== null && String(v).length > 0) {
            return String(v);
          }
        }
        return '';
      };

      for (const event of events) {
        const f = event.fields;
        const a = event.actions;

        // TextData: options_text (login/logout), batch_text (sql_batch_*), statement (rpc_*)
        const textData = str(f, 'options_text', 'batch_text', 'statement');

        const displayEvent = {
          eventClass: event.name ?? 'Unknown',
          textData,
          applicationName: str(a, 'client_app_name'),
          hostName: str(a, 'client_hostname'),
          ntUserName: str(a, 'nt_username'),
          loginName: str(a, 'server_principal_name', 'username'),
          clientProcessId: str(a, 'client_pid'),
          spid: str(a, 'session_id'),
          startTime: event.timestamp ?? '',
          cpu: str(f, 'cpu_time'),
          reads: str(f, 'logical_reads'),
          writes: str(f, 'writes'),
          duration: str(f, 'duration'),
          databaseId: str(f, 'database_id'),
          databaseName: str(a, 'database_name'),
        };

        // Dedup key — mirrors ProfilerEvent.GetEventKey() priority exactly:
        //   1. event_sequence  (unique counter per session, most reliable)
        //   2. attach_activity_id (GUID, unique per activity)
        //   3. timestamp|name|session_id  (weakest, same format as C# fallback)
        const seqKey = str(a, 'event_sequence');
        const activityKey = str(a, 'attach_activity_id');
        const sessionId = str(a, 'session_id');
        const eventKey = seqKey
          ? `seq:${seqKey}`
          : activityKey
            ? `activity:${activityKey}`
            : `${event.timestamp ?? ''}|${event.name ?? ''}|${sessionId}`;

        if (this.sessionEventKeys.has(eventKey)) {
          continue;
        }
        this.sessionEventKeys.add(eventKey);

        // Apply active filters (case-insensitive contains, AND logic).
        // Filtered-out events are still added to sessionEventKeys so they won't
        // re-surface if the filter is relaxed later in the same session.
        const fil = this.eventFilter;
        const contains = (value: string, term: string): boolean =>
          !term || value.toLowerCase().includes(term.toLowerCase());
        if (
          !contains(displayEvent.eventClass, fil.eventClass) ||
          !contains(displayEvent.textData, fil.textData) ||
          !contains(displayEvent.applicationName, fil.applicationName) ||
          !contains(displayEvent.ntUserName, fil.ntUserName) ||
          !contains(displayEvent.loginName, fil.loginName) ||
          !contains(displayEvent.databaseName, fil.databaseName)
        ) {
          continue;
        }

        newEvents.push(displayEvent);
      }

      if (newEvents.length > 0) {
        this.eventCount += newEvents.length;

        await this.postMessage({
          command: 'addEvents',
          data: newEvents,
        });

        await this.postMessage({
          command: 'updateEventCount',
          data: this.eventCount,
        });

        // Sync host-side captured events — only post-filter events that were
        // actually sent to the webview are stored here. Capped at
        // maxCapturedEvents to match the webview's allEvents cap.
        this.capturedEvents.push(...newEvents);
        if (
          this.capturedEvents.length > ProfilerPanelProvider.maxCapturedEvents
        ) {
          this.capturedEvents = this.capturedEvents.slice(
            this.capturedEvents.length -
              ProfilerPanelProvider.maxCapturedEvents,
          );
        }
      }
    } catch (error) {
      this.logError(`Error polling events: ${String(error)}`);
    }
  }

  /**
   * Shows an error message in the webview and as a VS Code notification
   * @param message - Error message to display
   * @remarks Logs error and shows both in webview and VS Code UI
   */
  private async showError(message: string): Promise<void> {
    this.logError(message);
    await this.postMessage({
      command: 'error',
      data: message,
    });
    await vscode.window.showErrorMessage(`Light Query Profiler: ${message}`);
  }

  /**
   * Exports captured events to a JSON file chosen via a VS Code save dialog.
   *
   * @remarks
   * Uses `capturedEvents` (host-side mirror) so the panel does not need to be
   * open. Shows an informational message when there are no events to export.
   * Called both from the webview toolbar button (`exportEvents` message) and
   * from the `lightQueryProfiler.exportEvents` palette command.
   */
  public async exportEvents(): Promise<void> {
    if (this.capturedEvents.length === 0) {
      await vscode.window.showInformationMessage(
        'Light Query Profiler: No events to export.',
      );
      return;
    }

    const defaultUri = vscode.Uri.file(
      EventExportImportService.generateDefaultFilename(),
    );

    const uri = await vscode.window.showSaveDialog({
      defaultUri,
      // eslint-disable-next-line @typescript-eslint/naming-convention
      filters: { 'JSON Files': ['json'], 'All Files': ['*'] },
      title: 'Export Profiler Events',
      saveLabel: 'Export',
    });

    if (!uri) {
      return; // User cancelled
    }

    try {
      await EventExportImportService.exportEvents(
        this.capturedEvents,
        uri.fsPath,
      );
      const count = this.capturedEvents.length;
      this.log(`Exported ${count} events to ${uri.fsPath}`);
      await vscode.window.showInformationMessage(
        `Light Query Profiler: Exported ${count} event(s) to ${path.basename(uri.fsPath)}`,
      );
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      this.logError(`Export failed: ${message}`);
      await vscode.window.showErrorMessage(
        `Light Query Profiler: Export failed — ${message}`,
      );
    }
  }

  /**
   * Imports events from a JSON file chosen via a VS Code open dialog.
   *
   * @remarks
   * If the panel is already open the imported events are sent directly via
   * `loadImportedEvents`. If not, the panel is opened and the events are
   * stored in `pendingImportEvents`; the `webviewReady` handshake then
   * forwards them once the webview has finished initialising.
   * Called both from the webview toolbar button (`importEvents` message) and
   * from the `lightQueryProfiler.importEvents` palette command.
   */
  public async importEvents(): Promise<void> {
    const uris = await vscode.window.showOpenDialog({
      canSelectFiles: true,
      canSelectFolders: false,
      canSelectMany: false,
      // eslint-disable-next-line @typescript-eslint/naming-convention
      filters: { 'JSON Files': ['json'], 'All Files': ['*'] },
      title: 'Import Profiler Events',
      openLabel: 'Import',
    });

    if (!uris || uris.length === 0) {
      return; // User cancelled
    }

    const selectedUri = uris[0];
    if (!selectedUri) {
      return; // Should never happen given the length check above
    }

    // Confirm replacement when events are already loaded
    if (this.capturedEvents.length > 0) {
      const answer = await vscode.window.showWarningMessage(
        `This will replace ${this.capturedEvents.length} existing event(s). Continue?`,
        { modal: true },
        'Replace',
      );
      if (answer !== 'Replace') {
        return;
      }
    }

    try {
      const result = await EventExportImportService.importEvents(
        selectedUri.fsPath,
      );
      const imported = result.events;

      // Update host-side state so subsequent exports reflect the imported data
      this.capturedEvents = [...imported];
      this.eventCount = imported.length;

      if (this.panel) {
        // Panel is already open — send directly
        await this.postMessage({
          command: 'loadImportedEvents',
          data: imported,
        });
      } else {
        // Panel not yet open — store as pending; webviewReady will forward
        this.pendingImportEvents = imported;
        this.showPanel();
      }

      this.log(`Imported ${imported.length} events from ${selectedUri.fsPath}`);
      await vscode.window.showInformationMessage(
        `Light Query Profiler: Imported ${imported.length} event(s) from ${path.basename(selectedUri.fsPath)}`,
      );
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      this.logError(`Import failed: ${message}`);
      await vscode.window.showErrorMessage(
        `Light Query Profiler: Import failed — ${message}`,
      );
    }
  }

  /**
   * Posts a message to the webview
   * @param message - Message to send to the webview
   * @remarks No-op if panel is not initialized
   */
  private async postMessage(message: WebviewOutgoingMessage): Promise<void> {
    if (this.panel) {
      await this.panel.webview.postMessage(message);
    }
  }

  /**
   * Disposes the provider and cleans up resources
   * @remarks Stops polling and profiling session if active
   */
  public async dispose(): Promise<void> {
    this.log('Disposing profiler panel provider...');
    this.stopPolling();

    if (this.state !== ProfilerState.Stopped) {
      try {
        await this.handleStop();
      } catch (error) {
        this.logError(
          `Error stopping profiler during dispose: ${String(error)}`,
        );
      }
    }

    if (this.panel) {
      this.panel.dispose();
      this.panel = undefined;
    }

    this.log('Profiler panel provider disposed');
  }

  /**
   * Logs an informational message to console
   * @param message - Message to log
   * @remarks Includes timestamp and component prefix for debugging
   */
  private log(message: string): void {
    const timestamp = new Date().toISOString();
    this.outputChannel.appendLine(
      `[${timestamp}] [ProfilerPanelProvider] ${message}`,
    );
  }

  /**
   * Logs an error message to console
   * @param message - Error message to log
   * @remarks Includes timestamp, component prefix, and ERROR level indicator
   */
  private logError(message: string): void {
    const timestamp = new Date().toISOString();
    this.outputChannel.appendLine(
      `[${timestamp}] [ProfilerPanelProvider] ERROR: ${message}`,
    );
  }

  /**
   * Gets the HTML content for the webview
   * @param webview - Webview instance for CSP source
   * @returns HTML string for the webview
   * @remarks Includes inline styles, scripts, and Content Security Policy headers
   */
  private getHtmlContent(webview: vscode.Webview): string {
    const authModes = getAllAuthenticationModes();

    const hlJsUri = webview
      .asWebviewUri(
        vscode.Uri.joinPath(this.extensionUri, 'media', 'highlight.min.js'),
      )
      .toString();
    const hlSqlUri = webview
      .asWebviewUri(
        vscode.Uri.joinPath(this.extensionUri, 'media', 'highlight-sql.min.js'),
      )
      .toString();
    const hlCssUri = webview
      .asWebviewUri(
        vscode.Uri.joinPath(
          this.extensionUri,
          'media',
          'highlight-vs2015.min.css',
        ),
      )
      .toString();

    return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src ${webview.cspSource} 'unsafe-inline'; script-src ${webview.cspSource} 'unsafe-inline';">
  <title>Light Query Profiler</title>
  <link rel="stylesheet" href="${hlCssUri}">
  <script src="${hlJsUri}"></script>
  <script src="${hlSqlUri}"></script>
  <style>
    *, *::before, *::after { box-sizing: border-box; }

    body {
      margin: 0;
      padding: 0;
      color: var(--vscode-foreground);
      font-family: var(--vscode-font-family);
      font-size: var(--vscode-font-size);
      background-color: var(--vscode-sideBar-background, var(--vscode-editor-background));
      height: 100vh;
      display: flex;
      flex-direction: column;
      overflow: hidden;
    }

    /* ── Top header bar ─────────────────────────────────────────────── */
    .app-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 8px 16px;
      background-color: var(--vscode-titleBar-activeBackground, var(--vscode-editor-background));
      border-bottom: 1px solid var(--vscode-panel-border);
      flex-shrink: 0;
      gap: 12px;
    }

    .session-timer-group {
      display: flex;
      align-items: center;
      gap: 6px;
      user-select: none;
    }

    .session-timer-label {
      font-size: 11px;
      font-weight: 600;
      letter-spacing: 0.4px;
      text-transform: uppercase;
      color: var(--vscode-descriptionForeground);
    }

    .session-timer {
      font-family: var(--vscode-editor-font-family, monospace);
      font-size: 12px;
      font-weight: 600;
      letter-spacing: 0.5px;
      color: var(--vscode-descriptionForeground);
      min-width: 44px;
    }

    .session-timer.running {
      color: var(--vscode-foreground);
    }

    .header-status {
      display: flex;
      align-items: center;
      gap: 16px;
      font-size: 12px;
      color: var(--vscode-descriptionForeground);
    }

    .status-badge {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 3px 10px;
      border-radius: 10px;
      font-size: 11px;
      font-weight: 600;
      letter-spacing: 0.4px;
      text-transform: uppercase;
      border: 1px solid transparent;
      transition: background-color 0.2s, color 0.2s, border-color 0.2s;
    }

    .status-badge.stopped {
      background-color: rgba(var(--vscode-testing-iconFailed-rgb, 255,60,60), 0.12);
      color: var(--vscode-testing-iconFailed, #f14c4c);
      border-color: rgba(var(--vscode-testing-iconFailed-rgb, 255,60,60), 0.3);
    }

    .status-badge.running {
      background-color: rgba(var(--vscode-testing-iconPassed-rgb, 60,200,60), 0.12);
      color: var(--vscode-testing-iconPassed, #4ec94e);
      border-color: rgba(var(--vscode-testing-iconPassed-rgb, 60,200,60), 0.3);
    }

    .status-badge.paused {
      background-color: rgba(var(--vscode-notificationsWarningIcon-foreground-rgb, 200,160,0), 0.12);
      color: var(--vscode-notificationsWarningIcon-foreground, #cca700);
      border-color: rgba(var(--vscode-notificationsWarningIcon-foreground-rgb, 200,160,0), 0.3);
    }

    .status-dot {
      width: 7px;
      height: 7px;
      border-radius: 50%;
      background-color: currentColor;
      flex-shrink: 0;
    }

    .status-badge.running .status-dot {
      animation: pulse 1.4s ease-in-out infinite;
    }

    @keyframes pulse {
      0%, 100% { opacity: 1; transform: scale(1); }
      50%       { opacity: 0.45; transform: scale(0.8); }
    }

    .events-counter {
      display: flex;
      align-items: center;
      gap: 5px;
      font-size: 12px;
    }

    .events-counter-label {
      color: var(--vscode-descriptionForeground);
    }

    .events-counter-value {
      font-weight: 700;
      color: var(--vscode-foreground);
      min-width: 20px;
      text-align: right;
    }

    /* ── Scrollable main content ────────────────────────────────────── */
    .main-content {
      flex: 1;
      overflow-y: auto;
      padding: 12px 16px 0 16px;
      display: flex;
      flex-direction: column;
      gap: 10px;
      min-height: 0;
    }

    /* ── Card / section ─────────────────────────────────────────────── */
    .card {
      background-color: var(--vscode-editor-background);
      border: 1px solid var(--vscode-panel-border);
      border-radius: 5px;
      overflow: hidden;
      flex-shrink: 0;
    }

    .card-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 7px 12px;
      background-color: var(--vscode-sideBarSectionHeader-background, rgba(128,128,128,0.08));
      border-bottom: 1px solid var(--vscode-panel-border);
      cursor: pointer;
      user-select: none;
    }

    .card-header-left {
      display: flex;
      align-items: center;
      gap: 6px;
      font-size: 11px;
      font-weight: 700;
      letter-spacing: 0.6px;
      text-transform: uppercase;
      color: var(--vscode-sideBarSectionHeader-foreground, var(--vscode-foreground));
    }

    .card-chevron {
      font-size: 10px;
      transition: transform 0.15s;
      color: var(--vscode-descriptionForeground);
    }

    .card.collapsed .card-chevron { transform: rotate(-90deg); }
    .card.collapsed .card-body { display: none; }

    .card-body {
      padding: 12px;
    }

    /* ── 2-column form grid ─────────────────────────────────────────── */
    .form-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 10px 16px;
    }

    .form-grid .span-full {
      grid-column: 1 / -1;
    }

    .form-group {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }

    label {
      font-size: 11px;
      font-weight: 600;
      letter-spacing: 0.2px;
      color: var(--vscode-descriptionForeground);
      text-transform: uppercase;
    }

    input[type="text"],
    input[type="password"],
    select {
      width: 100%;
      padding: 5px 8px;
      background-color: var(--vscode-input-background);
      color: var(--vscode-input-foreground);
      border: 1px solid var(--vscode-input-border, var(--vscode-panel-border));
      border-radius: 3px;
      font-family: var(--vscode-font-family);
      font-size: var(--vscode-font-size);
      transition: border-color 0.15s;
    }

    input:focus,
    select:focus {
      outline: none;
      border-color: var(--vscode-focusBorder);
      box-shadow: 0 0 0 1px var(--vscode-focusBorder);
    }

    /* ── Toolbar ────────────────────────────────────────────────────── */
    .toolbar {
      display: flex;
      align-items: center;
      gap: 4px;
      padding: 8px 12px;
      border-top: 1px solid var(--vscode-panel-border);
      background-color: var(--vscode-sideBarSectionHeader-background, rgba(128,128,128,0.05));
    }

    .toolbar-divider {
      width: 1px;
      height: 18px;
      background-color: var(--vscode-panel-border);
      margin: 0 4px;
    }

    .btn {
      display: inline-flex;
      align-items: center;
      gap: 5px;
      padding: 5px 12px;
      border: 1px solid transparent;
      border-radius: 3px;
      cursor: pointer;
      font-family: var(--vscode-font-family);
      font-size: 12px;
      font-weight: 500;
      white-space: nowrap;
      transition: background-color 0.12s, opacity 0.12s;
    }

    .btn:disabled {
      opacity: 0.4;
      cursor: not-allowed;
      pointer-events: none;
    }

    input:disabled,
    select:disabled {
      opacity: 0.45;
      cursor: not-allowed;
      pointer-events: none;
    }

    .btn-primary {
      background-color: var(--vscode-button-background);
      color: var(--vscode-button-foreground);
      border-color: var(--vscode-button-background);
    }
    .btn-primary:hover:not(:disabled) {
      background-color: var(--vscode-button-hoverBackground);
    }

    .btn-secondary {
      background-color: var(--vscode-button-secondaryBackground);
      color: var(--vscode-button-secondaryForeground);
      border-color: var(--vscode-button-secondaryBackground);
    }
    .btn-secondary:hover:not(:disabled) {
      background-color: var(--vscode-button-secondaryHoverBackground);
    }

    .btn-danger {
      background-color: transparent;
      color: var(--vscode-errorForeground, #f14c4c);
      border-color: transparent;
    }
    .btn-danger:hover:not(:disabled) {
      background-color: rgba(var(--vscode-testing-iconFailed-rgb, 241,76,76), 0.1);
      border-color: rgba(var(--vscode-testing-iconFailed-rgb, 241,76,76), 0.25);
    }

    .btn-icon {
      font-size: 13px;
      line-height: 1;
    }

    /* Loading spinner inside button */
    .btn-spinner {
      display: inline-block;
      width: 11px;
      height: 11px;
      border: 2px solid rgba(255,255,255,0.3);
      border-top-color: var(--vscode-button-foreground);
      border-radius: 50%;
      animation: spin 0.7s linear infinite;
    }
    @keyframes spin { to { transform: rotate(360deg); } }

    /* ── Stats bar ──────────────────────────────────────────────────── */
    .stats-bar {
      display: flex;
      align-items: center;
      gap: 0;
      padding: 5px 12px;
      background-color: var(--vscode-sideBarSectionHeader-background, rgba(128,128,128,0.05));
      border-bottom: 1px solid var(--vscode-panel-border);
      flex-shrink: 0;
    }

    .stat-item {
      display: flex;
      align-items: center;
      gap: 5px;
      padding: 0 12px;
      border-right: 1px solid var(--vscode-panel-border);
      font-size: 11px;
    }

    .stat-item:first-child { padding-left: 0; }
    .stat-item:last-child  { border-right: none; }

    .stat-label {
      color: var(--vscode-descriptionForeground);
    }

    .stat-value {
      font-weight: 700;
      color: var(--vscode-foreground);
      font-variant-numeric: tabular-nums;
    }

    /* ── Events table ───────────────────────────────────────────────── */
    .events-section {
      flex: 1;
      display: flex;
      flex-direction: column;
      min-height: 0;
      border: 1px solid var(--vscode-panel-border);
      border-radius: 5px;
      overflow: hidden;
      background-color: var(--vscode-editor-background);
      margin-bottom: 10px;
    }

    .events-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 7px 12px;
      background-color: var(--vscode-sideBarSectionHeader-background, rgba(128,128,128,0.08));
      border-bottom: 1px solid var(--vscode-panel-border);
      font-size: 11px;
      font-weight: 700;
      letter-spacing: 0.6px;
      text-transform: uppercase;
      color: var(--vscode-sideBarSectionHeader-foreground, var(--vscode-foreground));
      flex-shrink: 0;
    }

    .events-container {
      flex: 1;
      overflow-y: auto;
      min-height: 0;
    }

    .events-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 12px;
      table-layout: fixed;
    }

    /* 15 columns matching WinForms: EventClass|TextData|ApplicationName|HostName|NTUserName|LoginName|ClientProcessID|SPID|StartTime|CPU|Reads|Writes|Duration|DatabaseID|DatabaseName */
    .events-table colgroup col:nth-child(1)  { width: 110px; } /* EventClass */
    .events-table colgroup col:nth-child(2)  { width: 200px; } /* TextData */
    .events-table colgroup col:nth-child(3)  { width: 130px; } /* ApplicationName */
    .events-table colgroup col:nth-child(4)  { width: 100px; } /* HostName */
    .events-table colgroup col:nth-child(5)  { width: 100px; } /* NTUserName */
    .events-table colgroup col:nth-child(6)  { width: 120px; } /* LoginName */
    .events-table colgroup col:nth-child(7)  { width: 80px;  } /* ClientProcessID */
    .events-table colgroup col:nth-child(8)  { width: 50px;  } /* SPID */
    .events-table colgroup col:nth-child(9)  { width: 110px; } /* StartTime */
    .events-table colgroup col:nth-child(10) { width: 70px;  } /* CPU */
    .events-table colgroup col:nth-child(11) { width: 60px;  } /* Reads */
    .events-table colgroup col:nth-child(12) { width: 60px;  } /* Writes */
    .events-table colgroup col:nth-child(13) { width: 80px;  } /* Duration */
    .events-table colgroup col:nth-child(14) { width: 70px;  } /* DatabaseID */
    .events-table colgroup col:nth-child(15) { width: 100px; } /* DatabaseName */

    .events-table thead {
      position: sticky;
      top: 0;
      z-index: 10;
    }

    .events-table th {
      padding: 6px 10px;
      text-align: left;
      background-color: var(--vscode-sideBarSectionHeader-background, rgba(128,128,128,0.1));
      font-size: 11px;
      font-weight: 700;
      letter-spacing: 0.4px;
      text-transform: uppercase;
      color: var(--vscode-descriptionForeground);
      border-bottom: 2px solid var(--vscode-panel-border);
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      position: relative;
    }

    .events-table th.sortable {
      cursor: pointer;
      user-select: none;
    }
    .events-table th.sortable:hover {
      color: var(--vscode-foreground);
      background-color: var(--vscode-list-hoverBackground);
    }
    .events-table th.sort-active {
      color: var(--vscode-foreground);
    }

    /* Column resize handle */
    .col-resizer {
      position: absolute;
      right: 0;
      top: 0;
      height: 100%;
      width: 5px;
      cursor: col-resize;
      user-select: none;
      z-index: 1;
    }
    .col-resizer:hover,
    .col-resizer.resizing {
      background-color: var(--vscode-focusBorder, #007acc);
      opacity: 0.6;
    }

    .events-table td {
      padding: 5px 10px;
      border-bottom: 1px solid var(--vscode-panel-border);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      vertical-align: middle;
      max-width: 0;
    }

    /* Zebra striping */
    .events-table tbody tr:nth-child(even) {
      background-color: rgba(128, 128, 128, 0.04);
    }

    .events-table tbody tr:hover {
      background-color: var(--vscode-list-hoverBackground) !important;
      cursor: pointer;
    }

    .events-table tbody tr.selected {
      background-color: var(--vscode-list-activeSelectionBackground) !important;
      color: var(--vscode-list-activeSelectionForeground);
    }

    /* Duration coloring */
    .dur-fast   { color: var(--vscode-testing-iconPassed, #4ec94e); }
    .dur-medium { color: var(--vscode-notificationsWarningIcon-foreground, #cca700); font-weight: 600; }
    .dur-slow   { color: var(--vscode-testing-iconFailed, #f14c4c); font-weight: 600; }

    /* Event type badge */
    .event-badge {
      display: inline-block;
      padding: 1px 6px;
      border-radius: 3px;
      font-size: 11px;
      font-weight: 600;
      background-color: rgba(var(--vscode-button-background-rgb, 0,122,204), 0.15);
      color: var(--vscode-button-background, #007acc);
      border: 1px solid rgba(var(--vscode-button-background-rgb, 0,122,204), 0.25);
      white-space: nowrap;
    }

    .no-events {
      padding: 48px 20px;
      text-align: center;
      color: var(--vscode-descriptionForeground);
      font-size: 13px;
    }

    .no-events-icon {
      font-size: 32px;
      display: block;
      margin-bottom: 10px;
      opacity: 0.4;
    }

    /* ── Details panel (tabbed) ─────────────────────────────────────── */
    .details-panel {
      flex-shrink: 0;
      border: 1px solid var(--vscode-panel-border);
      border-radius: 5px;
      overflow: hidden;
      background-color: var(--vscode-editor-background);
      margin-bottom: 10px;
      display: flex;
      flex-direction: column;
      height: clamp(160px, 28vh, 320px);
    }

    /* Tab bar */
    .details-tab-bar {
      display: flex;
      align-items: stretch;
      flex-shrink: 0;
      background-color: var(--vscode-sideBarSectionHeader-background, rgba(128,128,128,0.08));
      border-bottom: 1px solid var(--vscode-panel-border);
    }

    .details-tab {
      padding: 6px 16px;
      font-size: 12px;
      font-weight: 500;
      cursor: pointer;
      border: none;
      border-bottom: 2px solid transparent;
      background: none;
      color: var(--vscode-descriptionForeground);
      user-select: none;
      transition: color 0.12s, border-color 0.12s;
      white-space: nowrap;
    }
    .details-tab:hover {
      color: var(--vscode-foreground);
    }
    .details-tab.active {
      color: var(--vscode-foreground);
      border-bottom-color: var(--vscode-button-background, #007acc);
      font-weight: 600;
    }

    .details-tab-spacer { flex: 1; }

    .details-panel-close {
      background: none;
      border: none;
      cursor: pointer;
      color: var(--vscode-descriptionForeground);
      font-size: 14px;
      padding: 0 10px;
      line-height: 1;
      align-self: center;
    }
    .details-panel-close:hover {
      color: var(--vscode-foreground);
      background-color: var(--vscode-toolbar-hoverBackground);
    }

    /* Tab content panes */
    .details-tab-content { display: none; }
    .details-tab-content.active {
      display: flex;
      flex-direction: column;
      flex: 1;
      min-height: 0;
      overflow: hidden;
    }

    /* Text tab */
    .query-code {
      padding: 12px 14px;
      font-family: var(--vscode-editor-font-family, 'Courier New', monospace);
      font-size: 12px;
      line-height: 1.6;
      white-space: pre-wrap;
      word-break: break-word;
      flex: 1;
      min-height: 0;
      overflow-y: auto;
      color: var(--vscode-editor-foreground, var(--vscode-foreground));
    }

    /* Details tab — key/value table */
    .details-kv-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 12px;
      display: block;
      height: 100%;
      overflow-y: auto;
    }
    .details-kv-table td {
      padding: 4px 10px;
      border-bottom: 1px solid var(--vscode-panel-border);
      vertical-align: top;
    }
    .details-kv-table tr:last-child td { border-bottom: none; }
    .details-kv-table td:first-child {
      width: 140px;
      min-width: 140px;
      font-weight: 600;
      color: var(--vscode-descriptionForeground);
      white-space: nowrap;
    }
    .details-kv-table td:last-child {
      color: var(--vscode-foreground);
      word-break: break-word;
    }
    /* Zebra striping on details table */
    .details-kv-table tr:nth-child(even) td {
      background-color: rgba(128,128,128,0.04);
    }
    .details-kv-table tr:hover td {
      background-color: var(--vscode-list-hoverBackground);
    }

    /* ── Error banner ───────────────────────────────────────────────── */
    .error-banner {
      display: flex;
      align-items: flex-start;
      gap: 8px;
      padding: 9px 12px;
      background-color: var(--vscode-inputValidation-errorBackground);
      color: var(--vscode-inputValidation-errorForeground);
      border: 1px solid var(--vscode-inputValidation-errorBorder);
      border-radius: 4px;
      font-size: 12px;
      flex-shrink: 0;
    }

    .error-banner-icon { flex-shrink: 0; }
    .error-banner-close {
      margin-left: auto;
      background: none;
      border: none;
      cursor: pointer;
      color: inherit;
      opacity: 0.7;
      font-size: 14px;
      padding: 0;
      line-height: 1;
      flex-shrink: 0;
    }
    .error-banner-close:hover { opacity: 1; }

    .hidden { display: none !important; }

    /* ── Search bar ─────────────────────────────────────────────────── */
    .search-bar {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 5px 12px;
      background-color: var(--vscode-sideBarSectionHeader-background, rgba(128,128,128,0.05));
      border-bottom: 1px solid var(--vscode-panel-border);
      flex-shrink: 0;
    }

    .search-input {
      flex: 1;
      max-width: 280px;
    }

    .search-counter {
      font-size: 11px;
      color: var(--vscode-descriptionForeground);
      white-space: nowrap;
      min-width: 52px;
      text-align: right;
      font-variant-numeric: tabular-nums;
    }

    .search-wrap-msg {
      font-size: 11px;
      color: var(--vscode-notificationsWarningIcon-foreground, #cca700);
      white-space: nowrap;
    }

    /* Row match highlighting */
    .search-match {
      outline: 1px solid rgba(255, 200, 0, 0.35);
      background-color: rgba(255, 200, 0, 0.10) !important;
    }

    .search-current {
      outline: 2px solid rgba(255, 200, 0, 0.85) !important;
      background-color: rgba(255, 200, 0, 0.28) !important;
    }

    /* ── Filter modal ────────────────────────────────────────────────── */
    .filter-modal-overlay {
      display: none;
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.55);
      z-index: 1000;
      align-items: center;
      justify-content: center;
    }
    .filter-modal-overlay.open {
      display: flex;
    }
    .filter-modal {
      background: var(--vscode-editor-background);
      border: 1px solid var(--vscode-panel-border, #454545);
      border-radius: 6px;
      width: 360px;
      max-width: 92vw;
      box-shadow: 0 8px 32px rgba(0, 0, 0, 0.45);
      display: flex;
      flex-direction: column;
    }
    .filter-modal-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 11px 16px;
      border-bottom: 1px solid var(--vscode-panel-border, #454545);
      font-size: 13px;
      font-weight: 600;
    }
    .filter-modal-close {
      background: none;
      border: none;
      color: var(--vscode-foreground);
      cursor: pointer;
      font-size: 13px;
      padding: 2px 6px;
      border-radius: 3px;
      line-height: 1;
    }
    .filter-modal-close:hover {
      background: var(--vscode-toolbar-hoverBackground, rgba(90,93,94,0.31));
    }
    .filter-modal-body {
      padding: 14px 16px;
      display: flex;
      flex-direction: column;
      gap: 10px;
    }
    .filter-field {
      display: flex;
      flex-direction: column;
      gap: 3px;
    }
    .filter-field label {
      font-size: 11px;
      font-weight: 600;
      color: var(--vscode-foreground);
      opacity: 0.85;
    }
    .filter-field input {
      background: var(--vscode-input-background);
      border: 1px solid var(--vscode-input-border, #3c3c3c);
      color: var(--vscode-input-foreground);
      padding: 5px 8px;
      border-radius: 3px;
      font-size: 12px;
      font-family: inherit;
      width: 100%;
    }
    .filter-field input::placeholder {
      color: var(--vscode-input-placeholderForeground, #888);
      font-style: italic;
    }
    .filter-field input:focus {
      outline: 1px solid var(--vscode-focusBorder, #007fd4);
      border-color: var(--vscode-focusBorder, #007fd4);
    }
    .filter-modal-footer {
      display: flex;
      gap: 8px;
      padding: 11px 16px;
      border-top: 1px solid var(--vscode-panel-border, #454545);
    }

    /* ── Filter active badge on Filters button ───────────────────────── */
    #filterBtn {
      position: relative;
    }
    #filterBtn.filter-active::after {
      content: '';
      position: absolute;
      top: 5px;
      right: 5px;
      width: 7px;
      height: 7px;
      border-radius: 50%;
      background: var(--vscode-notificationsWarningIcon-foreground, #f5a623);
      pointer-events: none;
    }
  </style>
</head>
<body>

  <!-- ── Top header bar ──────────────────────────────────────────── -->
  <div class="app-header">
    <div class="session-timer-group">
      <span class="session-timer-label">Session Duration</span>
      <span class="session-timer" id="sessionTimer">—</span>
    </div>
    <div class="header-status">
      <div class="status-badge stopped" id="statusBadge">
        <span class="status-dot"></span>
        <span id="statusText">Stopped</span>
      </div>
      <div class="events-counter">
        <span class="events-counter-label">Events:</span>
        <span class="events-counter-value" id="eventCount">0</span>
      </div>
    </div>
  </div>

  <!-- ── Main scrollable area ────────────────────────────────────── -->
  <div class="main-content">

    <!-- Error banner -->
    <div id="errorContainer" class="error-banner hidden">
      <span class="error-banner-icon">⚠</span>
      <span id="errorText"></span>
      <button class="error-banner-close" id="errorClose" title="Dismiss">✕</button>
    </div>

    <!-- Connection Settings card -->
    <div class="card" id="connectionCard">
      <div class="card-header" id="connectionCardHeader">
        <div class="card-header-left">
          <span>⚙</span>
          <span>Connection Settings</span>
        </div>
        <span class="card-chevron">▾</span>
      </div>
      <div class="card-body">
        <div class="form-grid">
          <div class="form-group">
            <label for="authMode">Authentication Mode</label>
            <select id="authMode">
              ${authModes.map((mode) => '<option value="' + mode.value + '">' + mode.label + '</option>').join('')}
            </select>
          </div>

          <div class="form-group">
            <label for="server">Server</label>
            <input type="text" id="server" placeholder="e.g. myserver\\instance or myserver.database.windows.net" />
          </div>

          <div class="form-group hidden" id="databaseGroup">
            <label for="database">Database</label>
            <input type="text" id="database" placeholder="master" value="master" />
          </div>

          <div class="form-group hidden" id="usernameGroup">
            <label for="username">Username</label>
            <input type="text" id="username" autocomplete="username" />
          </div>

          <div class="form-group hidden span-full" id="passwordGroup">
            <label for="password">Password</label>
            <input type="password" id="password" autocomplete="current-password" />
          </div>
        </div>
      </div>

      <!-- Toolbar inside the card footer -->
      <div class="toolbar">
        <button class="btn btn-primary" id="startBtn">
          <span class="btn-icon" id="startIcon">▶</span>
          <span id="startLabel">Start</span>
        </button>
        <div class="toolbar-divider"></div>
        <button class="btn btn-secondary" id="pauseBtn" disabled>
          <span class="btn-icon">⏸</span> Pause
        </button>
        <button class="btn btn-secondary hidden" id="resumeBtn">
          <span class="btn-icon">▶</span> Resume
        </button>
        <button class="btn btn-secondary" id="stopBtn" disabled>
          <span class="btn-icon">⏹</span> Stop
        </button>
        <div class="toolbar-divider"></div>
        <button class="btn btn-danger" id="clearBtn">
          <span class="btn-icon">🗑</span> Clear
        </button>
        <div class="toolbar-divider"></div>
        <button class="btn btn-secondary" id="filterBtn" title="Configure filters">
          <span class="btn-icon">⧩</span> Filters
        </button>
        <button class="btn btn-secondary" id="clearFilterBtn" title="Clear all filters" disabled>
          <span class="btn-icon">✕</span> Clear Filters
        </button>
        <div class="toolbar-divider"></div>
        <button class="btn btn-secondary" id="exportBtn" title="Export captured events to a JSON file" aria-label="Export Events">
          <span class="btn-icon">⬆</span> Export...
        </button>
        <button class="btn btn-secondary" id="importBtn" title="Import events from a JSON file" aria-label="Import Events">
          <span class="btn-icon">⬇</span> Import...
        </button>
      </div>
    </div>

    <!-- Events section -->
    <div class="events-section">
      <div class="events-header">
        <span>Captured Events</span>
      </div>

      <!-- Stats bar -->
      <div class="stats-bar" id="statsBar">
        <div class="stat-item">
          <span class="stat-label">Total</span>
          <span class="stat-value" id="statTotal">0</span>
        </div>
        <div class="stat-item">
          <span class="stat-label">Avg Duration</span>
          <span class="stat-value" id="statAvg">—</span>
        </div>
        <div class="stat-item">
          <span class="stat-label">Max Duration</span>
          <span class="stat-value" id="statMax">—</span>
        </div>
        <div class="stat-item">
          <span class="stat-label">Max Reads</span>
          <span class="stat-value" id="statReads">—</span>
        </div>
      </div>

      <!-- Search bar -->
      <div class="search-bar" id="searchBar">
        <input class="search-input" id="searchInput" type="text" placeholder="Find in results…" autocomplete="off" spellcheck="false"/>
        <span class="search-counter" id="searchCounter"></span>
        <span class="search-wrap-msg" id="searchWrapMsg"></span>
        <button class="btn btn-secondary" id="searchPrevBtn" title="Previous match (Shift+Enter)" style="font-size:11px;padding:3px 8px;">⬆ Prev</button>
        <button class="btn btn-secondary" id="searchNextBtn" title="Next match (Enter)" style="font-size:11px;padding:3px 8px;">⬇ Next</button>
        <button class="btn btn-secondary" id="searchClearBtn" title="Clear search" style="font-size:11px;padding:3px 8px;">✕</button>
      </div>

      <div class="events-container" id="eventsContainer">
        <table class="events-table">
          <colgroup>
            <col/><col/><col/><col/><col/><col/><col/><col/><col/><col/><col/><col/><col/><col/><col/>
          </colgroup>
          <thead>
            <tr>
              <th class="sortable" data-col="eventClass"      data-type="string">EventClass ↕</th>
              <th class="sortable" data-col="textData"        data-type="string">TextData ↕</th>
              <th class="sortable" data-col="applicationName" data-type="string">ApplicationName ↕</th>
              <th class="sortable" data-col="hostName"        data-type="string">HostName ↕</th>
              <th class="sortable" data-col="ntUserName"      data-type="string">NTUserName ↕</th>
              <th class="sortable" data-col="loginName"       data-type="string">LoginName ↕</th>
              <th class="sortable" data-col="clientProcessId" data-type="number">ClientProcessID ↕</th>
              <th class="sortable" data-col="spid"            data-type="number">SPID ↕</th>
              <th class="sortable" data-col="startTime"       data-type="string">StartTime ↕</th>
              <th class="sortable" data-col="cpu"             data-type="number">CPU ↕</th>
              <th class="sortable" data-col="reads"           data-type="number">Reads ↕</th>
              <th class="sortable" data-col="writes"          data-type="number">Writes ↕</th>
              <th class="sortable" data-col="duration"        data-type="number">Duration (ms) ↕</th>
              <th class="sortable" data-col="databaseId"      data-type="number">DatabaseID ↕</th>
              <th class="sortable" data-col="databaseName"    data-type="string">DatabaseName ↕</th>
            </tr>
          </thead>
          <tbody id="eventsTableBody">
            <tr>
              <td colspan="15" class="no-events">
                <span class="no-events-icon">🔍</span>
                No events captured yet.<br>Configure connection and click <strong>Start</strong> to begin profiling.
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Details panel (hidden until row selected) -->
    <div class="details-panel hidden" id="queryPanel">
      <!-- Tab bar -->
      <div class="details-tab-bar">
        <button class="details-tab active" id="tabText" data-tab="tabContentText">Text</button>
        <button class="details-tab" id="tabDetails" data-tab="tabContentDetails">Details</button>
        <div class="details-tab-spacer"></div>
        <button class="btn btn-secondary" id="btnCopyText" title="Copy query text to clipboard" disabled style="font-size:11px;padding:3px 8px;margin:3px 4px;align-self:center;">&#128203; Copy</button>
        <button class="details-panel-close" id="queryPanelClose" title="Close">✕</button>
      </div>
      <!-- Text tab content -->
      <div class="details-tab-content active" id="tabContentText">
        <div class="query-code" id="queryCode"></div>
      </div>
      <!-- Details tab content -->
      <div class="details-tab-content" id="tabContentDetails">
        <table class="details-kv-table" id="detailsKvTable">
          <tbody id="detailsKvBody"></tbody>
        </table>
      </div>
    </div>

  </div><!-- /main-content -->

  <!-- ── Filter Modal ─────────────────────────────────────────────────── -->
  <div class="filter-modal-overlay" id="filterModalOverlay" role="dialog" aria-modal="true" aria-labelledby="filterModalTitle">
    <div class="filter-modal">
      <div class="filter-modal-header">
        <span id="filterModalTitle">Filters</span>
        <button class="filter-modal-close" id="filterCloseBtn" title="Close">✕</button>
      </div>
      <div class="filter-modal-body">
        <div class="filter-field">
          <label for="fEventClass">EventClass</label>
          <input type="text" id="fEventClass" placeholder="Contains" autocomplete="off" spellcheck="false" />
        </div>
        <div class="filter-field">
          <label for="fTextData">TextData</label>
          <input type="text" id="fTextData" placeholder="Contains" autocomplete="off" spellcheck="false" />
        </div>
        <div class="filter-field">
          <label for="fApplicationName">ApplicationName</label>
          <input type="text" id="fApplicationName" placeholder="Contains" autocomplete="off" spellcheck="false" />
        </div>
        <div class="filter-field">
          <label for="fNTUserName">NTUserName</label>
          <input type="text" id="fNTUserName" placeholder="Contains" autocomplete="off" spellcheck="false" />
        </div>
        <div class="filter-field">
          <label for="fLoginName">LoginName</label>
          <input type="text" id="fLoginName" placeholder="Contains" autocomplete="off" spellcheck="false" />
        </div>
        <div class="filter-field">
          <label for="fDatabaseName">DatabaseName</label>
          <input type="text" id="fDatabaseName" placeholder="Contains" autocomplete="off" spellcheck="false" />
        </div>
      </div>
      <div class="filter-modal-footer">
        <button class="btn btn-primary"   id="filterApplyBtn"  disabled>Apply</button>
        <button class="btn btn-secondary" id="filterCancelBtn">Close</button>
      </div>
    </div>
  </div>

  <script>
    (function() {
      'use strict';
      const vscode = acquireVsCodeApi();

      // ── DOM refs ────────────────────────────────────────────────────
      const authMode         = document.getElementById('authMode');
      const serverInput      = document.getElementById('server');
      const databaseInput    = document.getElementById('database');
      const usernameInput    = document.getElementById('username');
      const passwordInput    = document.getElementById('password');
      const usernameGroup    = document.getElementById('usernameGroup');
      const passwordGroup    = document.getElementById('passwordGroup');
      const databaseGroup    = document.getElementById('databaseGroup');

      const startBtn         = document.getElementById('startBtn');
      const startIcon        = document.getElementById('startIcon');
      const startLabel       = document.getElementById('startLabel');
      const pauseBtn         = document.getElementById('pauseBtn');
      const resumeBtn        = document.getElementById('resumeBtn');
      const stopBtn          = document.getElementById('stopBtn');
      const clearBtn         = document.getElementById('clearBtn');

      const statusBadge      = document.getElementById('statusBadge');
      const statusText       = document.getElementById('statusText');
      const eventCount       = document.getElementById('eventCount');
      const eventsTableBody  = document.getElementById('eventsTableBody');
      const eventsContainer  = document.getElementById('eventsContainer');
      const queryPanel       = document.getElementById('queryPanel');
      const queryCode        = document.getElementById('queryCode');
      const queryPanelClose  = document.getElementById('queryPanelClose');
      const tabText          = document.getElementById('tabText');
      const tabDetails       = document.getElementById('tabDetails');
      const tabContentText   = document.getElementById('tabContentText');
      const tabContentDetails= document.getElementById('tabContentDetails');
      const detailsKvBody    = document.getElementById('detailsKvBody');
      const errorContainer   = document.getElementById('errorContainer');
      const errorText        = document.getElementById('errorText');
      const errorClose       = document.getElementById('errorClose');
      const connectionCard   = document.getElementById('connectionCard');
      const connectionCardHdr= document.getElementById('connectionCardHeader');

      const statTotal        = document.getElementById('statTotal');
      const statAvg          = document.getElementById('statAvg');
      const statMax          = document.getElementById('statMax');
      const statReads        = document.getElementById('statReads');
      const eventsTableHead  = document.querySelector('.events-table thead');
      const btnCopyText      = document.getElementById('btnCopyText');

      const searchInput      = document.getElementById('searchInput');
      const searchPrevBtn    = document.getElementById('searchPrevBtn');
      const searchNextBtn    = document.getElementById('searchNextBtn');
      const searchClearBtn   = document.getElementById('searchClearBtn');
      const searchCounter    = document.getElementById('searchCounter');
      const searchWrapMsg    = document.getElementById('searchWrapMsg');

      // Filter controls
      const filterBtn            = document.getElementById('filterBtn');
      const clearFilterBtn       = document.getElementById('clearFilterBtn');
      const exportBtn            = document.getElementById('exportBtn');
      const importBtn            = document.getElementById('importBtn');
      const filterModalOverlay   = document.getElementById('filterModalOverlay');
      const filterCloseBtn       = document.getElementById('filterCloseBtn');
      const filterApplyBtn       = document.getElementById('filterApplyBtn');
      const filterCancelBtn      = document.getElementById('filterCancelBtn');
      const fEventClass          = document.getElementById('fEventClass');
      const fTextData            = document.getElementById('fTextData');
      const fApplicationName     = document.getElementById('fApplicationName');
      const fNTUserName          = document.getElementById('fNTUserName');
      const fLoginName           = document.getElementById('fLoginName');
      const fDatabaseName        = document.getElementById('fDatabaseName');

      // ── State ───────────────────────────────────────────────────────
      let currentState        = 'stopped';
      let selectedEventRow    = null;
      let allEvents           = [];        // flat array of event objects for stats (capped at MAX_EVENTS)
      const MAX_EVENTS        = 10000;     // cap to prevent unbounded memory/DOM growth
      let sortCol             = 'duration'; // active sort column key
      let sortDesc            = true;       // sort direction
      let currentTextData     = '';         // raw text for copy button
      let isStarting          = false;

      // Incremental stats accumulators — updated per-event in addEvents()
      // so updateStats() is O(1) instead of O(n) over the full allEvents array.
      let statsTotal     = 0;
      let statsDurSum    = 0.0;
      let statsDurMax    = 0.0;
      let statsDurCount  = 0;
      let statsReadsMax  = 0;

      // Filter state — mirrors EventFilter on the TypeScript side
      let activeFilter = {
        eventClass: '', textData: '', applicationName: '',
        ntUserName: '', loginName: '', databaseName: '',
      };

      // Search state
      let searchMatches       = [];   // array of <tr> elements matching the current query
      let searchIndex         = -1;   // index into searchMatches of the currently highlighted row
      let searchAtWrapEnd     = false; // true when user just hit next at last match (pending wrap forward)
      let searchAtWrapStart   = false; // true when user just hit prev at first match (pending wrap back)

      // Timer state
      let sessionStartTime    = null; // Date.now() snapshot when current run started
      let timerInterval       = null; // setInterval handle (1-second tick)

      // ── Auth mode visibility ────────────────────────────────────────
      function formatDuration(ms) {
        const totalSec = Math.floor(ms / 1000);
        const h   = Math.floor(totalSec / 3600);
        const m   = Math.floor((totalSec % 3600) / 60);
        const s   = totalSec % 60;
        const pad = n => String(n).padStart(2, '0');
        return pad(h) + ':' + pad(m) + ':' + pad(s);
      }

      function updateAuthVisibility() {
        const mode = parseInt(authMode.value);
        const isWindows = mode === 0;
        const needsCreds = mode === 1 || mode === 2;

        databaseGroup.classList.toggle('hidden', isWindows);
        if (isWindows) { databaseInput.value = ''; }
        usernameGroup.classList.toggle('hidden', !needsCreds);
        passwordGroup.classList.toggle('hidden', !needsCreds);
      }

      authMode.addEventListener('change', updateAuthVisibility);
      updateAuthVisibility();

      // ── Restore saved state ─────────────────────────────────────────
      const savedState = vscode.getState();
      if (savedState) {
        if (savedState.server)   { serverInput.value = savedState.server; }
        if (savedState.database) { databaseInput.value = savedState.database; }
        if (typeof savedState.authenticationMode === 'number') {
          authMode.value = String(savedState.authenticationMode);
          updateAuthVisibility();
        }
        if (savedState.username) { usernameInput.value = savedState.username; }
      }

      // ── Collapsible card ────────────────────────────────────────────
      connectionCardHdr.addEventListener('click', () => {
        connectionCard.classList.toggle('collapsed');
      });

      // ── Button handlers ─────────────────────────────────────────────
      startBtn.addEventListener('click', () => {
        if (isStarting) { return; }

        const mode = parseInt(authMode.value);
        const serverVal   = serverInput.value.trim();
        const databaseVal = databaseInput.value.trim();
        const usernameVal = usernameInput.value.trim();
        const passwordVal = passwordInput.value;

        // ── Client-side validation ───────────────────────────────────
        // Mirrors WinForms ConfigureAsync validation logic:
        //   - Server is always required
        //   - Database is required for Azure SQL Database (mode === 2)
        //   - Username and Password are required for SQL Server Auth (1) and Azure SQL (2)
        if (!serverVal) {
          showError('Server is required');
          return;
        }

        if (mode === 2 && !databaseVal) {
          showError('Database is required for Azure SQL Database authentication');
          return;
        }

        if ((mode === 1 || mode === 2) && !usernameVal) {
          showError('Username is required for SQL Server and Azure SQL authentication');
          return;
        }

        if ((mode === 1 || mode === 2) && !passwordVal) {
          showError('Password is required for SQL Server and Azure SQL authentication');
          return;
        }

        const settings = {
          server: serverVal,
          // For Azure SQL the database field is required (validated above).
          // For other modes, fall back to 'master' if the field is left blank.
          database: databaseVal || (mode !== 2 ? 'master' : ''),
          authenticationMode: mode,
          username: usernameVal || undefined,
          password: passwordVal || undefined,
        };
        vscode.setState({
          server: settings.server,
          database: settings.database,
          authenticationMode: settings.authenticationMode,
          username: settings.username,
        });
        setStarting(true);
        vscode.postMessage({ command: 'start', data: settings });
      });

      pauseBtn.addEventListener('click',  () => vscode.postMessage({ command: 'pause' }));
      resumeBtn.addEventListener('click', () => vscode.postMessage({ command: 'resume' }));
      stopBtn.addEventListener('click',   () => vscode.postMessage({ command: 'stop' }));
      clearBtn.addEventListener('click',  () => vscode.postMessage({ command: 'clear' }));
      exportBtn.addEventListener('click', () => vscode.postMessage({ command: 'exportEvents' }));
      importBtn.addEventListener('click', () => vscode.postMessage({ command: 'importEvents' }));

      errorClose.addEventListener('click', () => errorContainer.classList.add('hidden'));
      queryPanelClose.addEventListener('click', () => {
        queryPanel.classList.add('hidden');
        if (selectedEventRow) {
          selectedEventRow.classList.remove('selected');
          selectedEventRow = null;
        }
      });

      btnCopyText.addEventListener('click', () => {
        if (currentTextData) {
          navigator.clipboard.writeText(currentTextData).catch(() => {});
        }
      });

      // ── Tab switching ───────────────────────────────────────────────
      function switchTab(tab) {
        [tabText, tabDetails].forEach(t => t.classList.remove('active'));
        [tabContentText, tabContentDetails].forEach(c => c.classList.remove('active'));
        tab.classList.add('active');
        const contentId = tab.dataset.tab;
        document.getElementById(contentId).classList.add('active');
      }
      tabText.addEventListener('click',    () => switchTab(tabText));
      tabDetails.addEventListener('click', () => switchTab(tabDetails));

      // ── Generic column sort ─────────────────────────────────────────
      eventsTableHead.addEventListener('click', (e) => {
        const th = e.target.closest('th.sortable');
        if (!th) { return; }
        const col  = th.dataset.col;
        const type = th.dataset.type || 'string';
        if (sortCol === col) {
          sortDesc = !sortDesc;
        } else {
          sortCol  = col;
          sortDesc = true;
        }
        // Update all th indicators
        eventsTableHead.querySelectorAll('th.sortable').forEach(h => {
          const base = h.textContent.replace(/ [↕↓↑]$/, '');
          h.textContent = base + ' ↕';
          h.classList.remove('sort-active');
        });
        const base = th.textContent.replace(/ [↕↓↑]$/, '');
        th.textContent = base + ' ' + (sortDesc ? '↓' : '↑');
        th.classList.add('sort-active');
        // Re-inject resize handles (textContent clobbers them)
        eventsTableHead.querySelectorAll('th').forEach(h => {
          if (!h.querySelector('.col-resizer')) {
            const d = document.createElement('div');
            d.className = 'col-resizer';
            h.appendChild(d);
          }
        });
        // Sort rows
        const rows = Array.from(eventsTableBody.querySelectorAll('tr[data-duration]'));
        rows.sort((a, b) => {
          const va = a.dataset[col] || '';
          const vb = b.dataset[col] || '';
          let cmp;
          if (type === 'number') {
            cmp = (parseFloat(va) || 0) - (parseFloat(vb) || 0);
          } else {
            cmp = va.localeCompare(vb);
          }
          return sortDesc ? -cmp : cmp;
        });
        rows.forEach(r => eventsTableBody.appendChild(r));
      });

      // ── Column resize drag handles ──────────────────────────────────
      function injectResizers() {
        eventsTableHead.querySelectorAll('th').forEach(th => {
          if (!th.querySelector('.col-resizer')) {
            const d = document.createElement('div');
            d.className = 'col-resizer';
            th.appendChild(d);
          }
        });
      }
      injectResizers();

      const colgroup = document.querySelector('.events-table colgroup');
      const cols     = colgroup ? Array.from(colgroup.querySelectorAll('col')) : [];

      eventsTableHead.addEventListener('mousedown', (e) => {
        const resizer = e.target.closest('.col-resizer');
        if (!resizer) { return; }
        e.preventDefault();
        const th       = resizer.parentElement;
        const thIndex  = Array.from(eventsTableHead.querySelectorAll('th')).indexOf(th);
        const col      = cols[thIndex];
        const startX   = e.pageX;
        const startW   = th.offsetWidth;
        resizer.classList.add('resizing');

        function onMouseMove(me) {
          const newW = Math.max(40, startW + me.pageX - startX);
          if (col) { col.style.width = newW + 'px'; }
          th.style.width = newW + 'px';
        }
        function onMouseUp() {
          resizer.classList.remove('resizing');
          document.removeEventListener('mousemove', onMouseMove);
          document.removeEventListener('mouseup', onMouseUp);
        }
        document.addEventListener('mousemove', onMouseMove);
        document.addEventListener('mouseup', onMouseUp);
      });

      // ── Starting indicator ──────────────────────────────────────────
      function setStarting(on) {
        isStarting = on;
        if (on) {
          startIcon.innerHTML = '<span class="btn-spinner"></span>';
          startLabel.textContent = 'Starting…';
          startBtn.disabled = true;
        } else {
          startIcon.textContent = '▶';
          startLabel.textContent = 'Start';
        }
      }

      // ── Filter dialog ────────────────────────────────────────────────

      /** Update Apply button enabled state based on whether any input has a value */
      function updateFilterApplyBtn() {
        const hasValue =
          fEventClass.value.trim()      !== '' ||
          fTextData.value.trim()        !== '' ||
          fApplicationName.value.trim() !== '' ||
          fNTUserName.value.trim()      !== '' ||
          fLoginName.value.trim()       !== '' ||
          fDatabaseName.value.trim()    !== '';
        filterApplyBtn.disabled = !hasValue;
      }

      /** Open the filter dialog, pre-populating inputs from current activeFilter */
      function openFilterDialog() {
        fEventClass.value      = activeFilter.eventClass;
        fTextData.value        = activeFilter.textData;
        fApplicationName.value = activeFilter.applicationName;
        fNTUserName.value      = activeFilter.ntUserName;
        fLoginName.value       = activeFilter.loginName;
        fDatabaseName.value    = activeFilter.databaseName;
        updateFilterApplyBtn();
        filterModalOverlay.classList.add('open');
        fEventClass.focus();
      }

      /** Close the filter dialog without saving */
      function closeFilterDialog() {
        filterModalOverlay.classList.remove('open');
      }

      /**
       * Update the filter button badge and clearFilterBtn state.
       * Called both when the extension sends updateFilter, and when Apply is clicked.
       */
      function updateFilterUI(filter) {
        activeFilter = filter;
        const isActive =
          filter.eventClass      !== '' ||
          filter.textData        !== '' ||
          filter.applicationName !== '' ||
          filter.ntUserName      !== '' ||
          filter.loginName       !== '' ||
          filter.databaseName    !== '';
        filterBtn.classList.toggle('filter-active', isActive);
        clearFilterBtn.disabled = !isActive;
      }

      /** Read inputs and send applyFilters to extension */
      function applyFilterDialog() {
        const filter = {
          eventClass:      fEventClass.value.trim(),
          textData:        fTextData.value.trim(),
          applicationName: fApplicationName.value.trim(),
          ntUserName:      fNTUserName.value.trim(),
          loginName:       fLoginName.value.trim(),
          databaseName:    fDatabaseName.value.trim(),
        };
        vscode.postMessage({ command: 'applyFilters', data: filter });
        updateFilterUI(filter);
        closeFilterDialog();
      }

      // Filter button listeners
      filterBtn.addEventListener('click', openFilterDialog);

      clearFilterBtn.addEventListener('click', () => {
        vscode.postMessage({ command: 'clearFilters' });
        updateFilterUI({ eventClass: '', textData: '', applicationName: '', ntUserName: '', loginName: '', databaseName: '' });
      });

      filterApplyBtn.addEventListener('click', applyFilterDialog);
      filterCancelBtn.addEventListener('click', closeFilterDialog);
      filterCloseBtn.addEventListener('click', closeFilterDialog);

      // Close when clicking the dark overlay backdrop (outside the modal card)
      filterModalOverlay.addEventListener('click', (e) => {
        if (e.target === filterModalOverlay) { closeFilterDialog(); }
      });

      // Enable/disable Apply button live as user types
      [fEventClass, fTextData, fApplicationName, fNTUserName, fLoginName, fDatabaseName]
        .forEach(input => input.addEventListener('input', updateFilterApplyBtn));

      // Close on Escape
      document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape' && filterModalOverlay.classList.contains('open')) {
          closeFilterDialog();
        }
      });

      // ── Messages from extension ─────────────────────────────────────
      window.addEventListener('message', (event) => {
        const msg = event.data;
        switch (msg.command) {
          case 'updateState':
            setStarting(false);
            updateState(msg.data.state);
            eventCount.textContent = msg.data.eventCount;
            statTotal.textContent  = msg.data.eventCount;
            break;
          case 'updateEventCount':
            eventCount.textContent = msg.data;
            statTotal.textContent  = msg.data;
            break;
          case 'addEvents':
            addEvents(msg.data);
            break;
          case 'clearEvents':
            clearEventsUI();
            break;
          case 'updateFilter':
            updateFilterUI(msg.data);
            break;
          case 'error':
            setStarting(false);
            updateState(currentState);
            showError(msg.data);
            break;
          case 'loadImportedEvents':
            // Replace all events with the imported set.
            // clearEventsUI() resets DOM, allEvents, accumulators and search.
            clearEventsUI();
            if (msg.data && msg.data.length > 0) {
              addEvents(msg.data);
            }
            break;
          case 'setConnectionFieldsEnabled': {
            const enabled = /** @type {boolean} */ (msg.data);
            authMode.disabled      = !enabled;
            serverInput.disabled   = !enabled;
            databaseInput.disabled = !enabled;
            usernameInput.disabled = !enabled;
            passwordInput.disabled = !enabled;
            break;
          }
        }
      });

      // ── State machine ───────────────────────────────────────────────
      function updateState(state) {
        currentState = state;

        // Badge
        statusBadge.className = 'status-badge ' + state;
        const labels = { stopped: 'Stopped', running: 'Running', paused: 'Paused' };
        statusText.textContent = labels[state] || state;

        // Buttons
        const isRunning = state === 'running';
        const isPaused  = state === 'paused';
        const isStopped = state === 'stopped';

        startBtn.disabled  = !isStopped;
        if (isStopped) { setStarting(false); }

        pauseBtn.disabled  = !isRunning;
        pauseBtn.classList.toggle('hidden', isPaused);
        resumeBtn.classList.toggle('hidden', !isPaused);
        resumeBtn.disabled = !isPaused;
        stopBtn.disabled   = isStopped;
        // Export and Import are only available when stopped — running would
        // mix live events with exported/imported data, producing a confusing result.
        exportBtn.disabled = !isStopped;
        importBtn.disabled = !isStopped;

        // Timer
        const timerEl = document.getElementById('sessionTimer');
        if (isRunning) {
          // Reset and start fresh (also covers Resume → new run)
          clearInterval(timerInterval);
          sessionStartTime = Date.now();
          timerEl.className = 'session-timer running';
          timerEl.textContent = '00:00:00';
          timerInterval = setInterval(function() {
            timerEl.textContent = formatDuration(Date.now() - sessionStartTime);
          }, 1000);
        } else if (isPaused) {
          // Freeze display — stop ticking but keep current value
          clearInterval(timerInterval);
          timerInterval = null;
          timerEl.className = 'session-timer';
        } else {
          // Stopped — clear everything
          clearInterval(timerInterval);
          timerInterval = null;
          sessionStartTime = null;
          timerEl.className = 'session-timer';
          timerEl.textContent = '\u2014'; // —
        }
      }

      // ── Add events ──────────────────────────────────────────────────
      function addEvents(events) {
        // Remove placeholder row
        const placeholder = eventsTableBody.querySelector('td[colspan]');
        if (placeholder) { eventsTableBody.innerHTML = ''; }

        // ── Scroll anchoring: capture selected row position before inserting ──
        // When new rows are inserted above the selected row, the browser keeps
        // scrollTop at the same absolute pixel value, which causes the selected
        // row to drift downward visually. We compensate by measuring the delta
        // in offsetTop before/after insertion and adjusting scrollTop to match.
        // NOTE: this code runs as plain JavaScript in the webview — no TypeScript
        // syntax (as casts, type annotations) is allowed here.
        const anchorRow = selectedEventRow;
        const anchorOffsetBefore = anchorRow ? anchorRow.offsetTop : null;
        const scrollTopBefore = eventsContainer ? eventsContainer.scrollTop : 0;

        events.forEach(event => {
          allEvents.push(event);

          // ── Update incremental stats accumulators (O(1) per event) ──
          statsTotal++;
          const durUs = parseFloat(event.duration || '');
          if (!isNaN(durUs)) {
            const durMs = durUs / 1000;
            statsDurSum += durMs;
            statsDurCount++;
            if (durMs > statsDurMax) { statsDurMax = durMs; }
          }
          const readsVal = parseFloat(event.reads || '');
          if (!isNaN(readsVal) && readsVal > statsReadsMax) { statsReadsMax = readsVal; }

          // duration arrives as string in microseconds; convert to ms
          const durMs2 = isNaN(durUs) ? null : durUs / 1000;
          const durClass = durMs2 === null ? '' :
            durMs2 < 100  ? 'dur-fast' :
            durMs2 < 1000 ? 'dur-medium' : 'dur-slow';
          const durText = durMs2 !== null ? durMs2.toFixed(2) : '';

          // TextData: truncate for display, full value stored on event object
          const textDisplay = event.textData
            ? (event.textData.length > 60 ? event.textData.substring(0, 60) + '…' : event.textData)
            : '';

          const row = document.createElement('tr');
          row.dataset.duration        = durMs2 !== null ? String(durMs2) : '0';
          row.dataset.eventClass      = event.eventClass      || '';
          row.dataset.textData        = event.textData        || '';
          row.dataset.applicationName = event.applicationName || '';
          row.dataset.hostName        = event.hostName        || '';
          row.dataset.ntUserName      = event.ntUserName      || '';
          row.dataset.loginName       = event.loginName       || '';
          row.dataset.clientProcessId = event.clientProcessId || '';
          row.dataset.spid            = event.spid            || '';
          row.dataset.startTime       = event.startTime       || '';
          row.dataset.cpu             = event.cpu             || '';
          row.dataset.reads           = event.reads           || '';
          row.dataset.writes          = event.writes          || '';
          row.dataset.databaseId      = event.databaseId      || '';
          row.dataset.databaseName    = event.databaseName    || '';

          row.innerHTML =
            '<td><span class="event-badge">' + escapeHtml(event.eventClass || '') + '</span></td>' +
            '<td title="' + escapeHtml(event.textData || '') + '">' + escapeHtml(textDisplay) + '</td>' +
            '<td>' + escapeHtml(event.applicationName || '') + '</td>' +
            '<td>' + escapeHtml(event.hostName || '') + '</td>' +
            '<td>' + escapeHtml(event.ntUserName || '') + '</td>' +
            '<td>' + escapeHtml(event.loginName || '') + '</td>' +
            '<td>' + escapeHtml(event.clientProcessId || '') + '</td>' +
            '<td>' + escapeHtml(event.spid || '') + '</td>' +
            '<td>' + formatTimestamp(event.startTime) + '</td>' +
            '<td>' + escapeHtml(event.cpu || '') + '</td>' +
            '<td>' + escapeHtml(event.reads || '') + '</td>' +
            '<td>' + escapeHtml(event.writes || '') + '</td>' +
            '<td class="' + durClass + '">' + durText + '</td>' +
            '<td>' + escapeHtml(event.databaseId || '') + '</td>' +
            '<td>' + escapeHtml(event.databaseName || '') + '</td>';

          row.addEventListener('click', () => selectRow(row, event));
          eventsTableBody.insertBefore(row, eventsTableBody.firstChild);
        });

        // ── Scroll anchoring: restore visual position of selected row ──
        // After inserting new rows at the top, compensate the container's
        // scrollTop by the exact number of pixels the anchor row moved down.
        // This keeps the selected row stationary on screen regardless of
        // how many new events arrive. If no row is selected, scrollTop is
        // left untouched so the table continues showing the newest events.
        if (anchorRow !== null && anchorOffsetBefore !== null && eventsContainer) {
          const anchorOffsetAfter = anchorRow.offsetTop;
          const delta = anchorOffsetAfter - anchorOffsetBefore;
          if (delta > 0) {
            eventsContainer.scrollTop = scrollTopBefore + delta;
          }
        }

        // Cap allEvents to prevent unbounded memory growth.
        // The cap only affects the backup array; the DOM table is not trimmed here
        // because removing oldest DOM rows would conflict with the newest-on-top
        // insertion order and user selections. The stats accumulators are not
        // affected by the cap — they track the full session lifetime.
        if (allEvents.length > MAX_EVENTS) {
          allEvents = allEvents.slice(allEvents.length - MAX_EVENTS);
        }

        updateStats();
      }

      // ── Stats ───────────────────────────────────────────────────────
      // Uses incremental accumulators updated in addEvents() — O(1) per call.
      function updateStats() {
        statTotal.textContent  = statsTotal;
        eventCount.textContent = String(statsTotal);

        if (statsDurCount > 0) {
          const avg = statsDurSum / statsDurCount;
          statAvg.textContent = avg.toFixed(1) + ' ms';
          statMax.textContent = statsDurMax.toFixed(1) + ' ms';
          statMax.className = 'stat-value ' +
            (statsDurMax < 100 ? 'dur-fast' : statsDurMax < 1000 ? 'dur-medium' : 'dur-slow');
        } else {
          statAvg.textContent = '—';
          statMax.textContent = '—';
          statMax.className   = 'stat-value';
        }

        if (statsReadsMax > 0) {
          statReads.textContent = statsReadsMax.toLocaleString();
        } else {
          statReads.textContent = '—';
        }
      }

      // ── Select row ──────────────────────────────────────────────────
      function selectRow(row, event) {
        if (selectedEventRow) { selectedEventRow.classList.remove('selected'); }
        row.classList.add('selected');
        selectedEventRow = row;

        // ── Text tab: highlight.js SQL syntax highlighting ───────────
        currentTextData = event.textData || '';
        if (currentTextData) {
          let highlighted;
          try {
            highlighted = hljs.highlight(currentTextData, { language: 'sql' }).value;
          } catch (_) {
            highlighted = escapeHtml(currentTextData);
          }
          queryCode.innerHTML = '<code class="hljs language-sql">' + highlighted + '</code>';
        } else {
          queryCode.innerHTML = '<span style="color:var(--vscode-descriptionForeground);font-style:italic">No text data</span>';
        }
        btnCopyText.disabled = !currentTextData;

        // ── Details tab: key/value table ─────────────────────────────
        const columns = [
          ['EventClass',       event.eventClass      || ''],
          ['TextData',         event.textData        || ''],
          ['ApplicationName',  event.applicationName || ''],
          ['HostName',         event.hostName        || ''],
          ['NTUserName',       event.ntUserName      || ''],
          ['LoginName',        event.loginName       || ''],
          ['ClientProcessID',  event.clientProcessId || ''],
          ['SPID',             event.spid            || ''],
          ['StartTime',        formatTimestamp(event.startTime)],
          ['CPU',              event.cpu             || ''],
          ['Reads',            event.reads           || ''],
          ['Writes',           event.writes          || ''],
          ['Duration',         event.duration        || ''],
          ['DatabaseID',       event.databaseId      || ''],
          ['DatabaseName',     event.databaseName    || ''],
        ];
        detailsKvBody.innerHTML = columns.map(([name, value]) =>
          '<tr><td>' + escapeHtml(name) + '</td><td>' + escapeHtml(String(value)) + '</td></tr>'
        ).join('');

        queryPanel.classList.remove('hidden');
      }

      // ── Search ──────────────────────────────────────────────────────
      function updateSearchCounter() {
        if (searchMatches.length === 0) {
          searchCounter.textContent = searchInput.value.trim() ? '0 / 0' : '';
        } else {
          searchCounter.textContent = (searchIndex + 1) + ' / ' + searchMatches.length;
        }
      }

      function clearSearch() {
        searchMatches.forEach(function(r) {
          r.classList.remove('search-match', 'search-current');
        });
        searchMatches = [];
        searchIndex = -1;
        searchAtWrapEnd = false;
        searchAtWrapStart = false;
        searchInput.value = '';
        searchCounter.textContent = '';
        searchWrapMsg.textContent = '';
      }

      function navigateTo(index) {
        // Remove current highlight from previous row
        if (searchIndex >= 0 && searchIndex < searchMatches.length) {
          searchMatches[searchIndex].classList.remove('search-current');
        }
        searchIndex = index;
        const row = searchMatches[searchIndex];
        row.classList.add('search-current');
        row.scrollIntoView({ block: 'nearest' });
        row.click();
        searchAtWrapEnd = false;
        searchAtWrapStart = false;
        searchWrapMsg.textContent = '';
        updateSearchCounter();
      }

      function runSearch() {
        const query = searchInput.value.trim().toLowerCase();
        searchWrapMsg.textContent = '';
        searchAtWrapEnd = false;
        searchAtWrapStart = false;

        // Remove old highlights
        searchMatches.forEach(function(r) {
          r.classList.remove('search-match', 'search-current');
        });
        searchMatches = [];
        searchIndex = -1;

        if (!query) {
          updateSearchCounter();
          return;
        }

        const rows = Array.from(eventsTableBody.querySelectorAll('tr[data-duration]'));
        rows.forEach(function(row) {
          const text = Object.values(row.dataset).join(' ').toLowerCase();
          if (text.includes(query)) {
            row.classList.add('search-match');
            searchMatches.push(row);
          }
        });

        if (searchMatches.length > 0) {
          searchIndex = 0;
          searchMatches[0].classList.add('search-current');
          searchMatches[0].scrollIntoView({ block: 'nearest' });
          searchMatches[0].click();
        }
        updateSearchCounter();
      }

      function searchNext() {
        if (searchMatches.length === 0) { return; }
        if (searchIndex >= searchMatches.length - 1) {
          // At the last match
          if (!searchAtWrapEnd) {
            searchAtWrapEnd = true;
            searchWrapMsg.textContent = '⚠ End of results — Enter to wrap to start';
            return;
          }
          // Second press — wrap to start
          searchAtWrapEnd = false;
          searchWrapMsg.textContent = '';
          navigateTo(0);
          return;
        }
        searchAtWrapEnd = false;
        searchAtWrapStart = false;
        searchWrapMsg.textContent = '';
        navigateTo(searchIndex + 1);
      }

      function searchPrev() {
        if (searchMatches.length === 0) { return; }
        if (searchIndex <= 0) {
          // At the first match
          if (!searchAtWrapStart) {
            searchAtWrapStart = true;
            searchWrapMsg.textContent = '⚠ Start of results — Shift+Enter to wrap to end';
            return;
          }
          // Second press — wrap to end
          searchAtWrapStart = false;
          searchWrapMsg.textContent = '';
          navigateTo(searchMatches.length - 1);
          return;
        }
        searchAtWrapEnd = false;
        searchAtWrapStart = false;
        searchWrapMsg.textContent = '';
        navigateTo(searchIndex - 1);
      }

      // ── Search listeners ─────────────────────────────────────────────
      searchInput.addEventListener('input', function() {
        runSearch();
      });

      searchInput.addEventListener('keydown', function(e) {
        if (e.key === 'Enter') {
          e.preventDefault();
          if (e.shiftKey) {
            searchPrev();
          } else {
            searchNext();
          }
        } else if (e.key === 'Escape') {
          clearSearch();
        }
      });

      searchNextBtn.addEventListener('click', function() {
        if (searchInput.value.trim()) {
          searchNext();
        }
      });

      searchPrevBtn.addEventListener('click', function() {
        if (searchInput.value.trim()) {
          searchPrev();
        }
      });

      searchClearBtn.addEventListener('click', function() {
        clearSearch();
      });

      // ── Clear ───────────────────────────────────────────────────────
      function clearEventsUI() {
        clearSearch();
        eventsTableBody.innerHTML =
          '<tr><td colspan="15" class="no-events">' +
          '<span class="no-events-icon">🔍</span>' +
          'No events captured yet.<br>Configure connection and click <strong>Start</strong> to begin profiling.' +
          '</td></tr>';
        queryPanel.classList.add('hidden');
        selectedEventRow = null;
        errorContainer.classList.add('hidden');
        allEvents = [];
        // Reset incremental stats accumulators
        statsTotal    = 0;
        statsDurSum   = 0.0;
        statsDurMax   = 0.0;
        statsDurCount = 0;
        statsReadsMax = 0;
        updateStats();
        statTotal.textContent = '0';
        eventCount.textContent = '0';
      }

      // ── Error ───────────────────────────────────────────────────────
      function showError(message) {
        errorText.textContent = message;
        errorContainer.classList.remove('hidden');
      }

      // ── Helpers ─────────────────────────────────────────────────────
      function escapeHtml(text) {
        const d = document.createElement('div');
        d.textContent = String(text);
        return d.innerHTML;
      }

      function formatTimestamp(timestamp) {
        if (!timestamp) { return ''; }
        return String(timestamp).replace('T', ' ');
      }

      // ── Webview ready handshake ──────────────────────────────────────
      // Notify the extension host that the webview JS has fully initialised.
      // If importEvents() was called while the panel was closed, the host
      // stored the data in pendingImportEvents and will forward it now.
      vscode.postMessage({ command: 'webviewReady' });

    })();
  </script>
</body>
</html>`;
  }
}
