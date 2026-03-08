import * as vscode from 'vscode';
import { ProfilerClient } from '../services/profiler-client';
import {
  ConnectionSettings,
  validateConnectionSettings,
} from '../models/connection-settings';
import { getAllAuthenticationModes } from '../models/authentication-mode';
import { ProfilerEvent, toEventRow } from '../models/profiler-event';

/**
 * Profiler state discriminated union
 * @remarks Used for state machine implementation in the view provider
 */
enum ProfilerState {
  Stopped = 'stopped',
  Running = 'running',
  Paused = 'paused',
}

/**
 * Base message from webview
 */
interface WebviewMessage {
  readonly command: string;
  readonly data?: unknown;
}

/**
 * Message to update state in the webview
 */
interface UpdateStateMessage {
  readonly command: 'updateState';
  readonly data: {
    readonly state: ProfilerState;
    readonly eventCount: number;
  };
}

/**
 * Message to add events to the webview
 */
interface AddEventsMessage {
  readonly command: 'addEvents';
  readonly data: ReadonlyArray<ReturnType<typeof toEventRow>>;
}

/**
 * Message to clear events in the webview
 */
interface ClearEventsMessage {
  readonly command: 'clearEvents';
}

/**
 * Message to show error in the webview
 */
interface ErrorMessage {
  readonly command: 'error';
  readonly data: string;
}

/**
 * Message to update event count
 */
interface UpdateEventCountMessage {
  readonly command: 'updateEventCount';
  readonly data: number;
}

/**
 * Union of all possible messages to webview
 */
type WebviewOutgoingMessage =
  | UpdateStateMessage
  | AddEventsMessage
  | ClearEventsMessage
  | ErrorMessage
  | UpdateEventCountMessage;

/**
 * Provider for the profiler webview panel
 * @remarks Manages the webview UI lifecycle and communication with the profiler client
 * @example
 * ```typescript
 * const provider = new ProfilerViewProvider(extensionUri, profilerClient);
 * vscode.window.registerWebviewViewProvider('viewId', provider);
 * ```
 */
export class ProfilerViewProvider implements vscode.WebviewViewProvider {
  private view?: vscode.WebviewView;
  private readonly profilerClient: ProfilerClient;
  private readonly extensionUri: vscode.Uri;
  private readonly outputChannel: vscode.OutputChannel;
  private sessionName = 'VSCodeProfilerSession';
  private state: ProfilerState = ProfilerState.Stopped;
  private pollingInterval: NodeJS.Timeout | null = null;
  private readonly pollingIntervalMs = 900; // Match WinForms implementation
  private eventCount = 0;
  private readonly seenEventKeys = new Set<string>();

  constructor(
    extensionUri: vscode.Uri,
    profilerClient: ProfilerClient,
    outputChannel: vscode.OutputChannel,
  ) {
    this.extensionUri = extensionUri;
    this.profilerClient = profilerClient;
    this.outputChannel = outputChannel;
  }

  /**
   * Resolves the webview view
   * @param webviewView - The webview view to resolve
   * @param _context - Resolve context (unused)
   * @param _token - Cancellation token (unused)
   * @remarks Called by VS Code when the webview is first shown
   */
  public resolveWebviewView(
    webviewView: vscode.WebviewView,
    _context: vscode.WebviewViewResolveContext,
    _token: vscode.CancellationToken,
  ): void | Thenable<void> {
    this.view = webviewView;

    webviewView.webview.options = {
      enableScripts: true,
      localResourceRoots: [this.extensionUri],
    };

    webviewView.webview.html = this.getHtmlContent(webviewView.webview);

    // Handle messages from the webview
    webviewView.webview.onDidReceiveMessage(
      async (message: unknown) => {
        await this.handleMessage(message);
      },
      undefined,
      [],
    );

    // Update state when view becomes visible
    webviewView.onDidChangeVisibility(() => {
      if (webviewView.visible) {
        void this.updateState();
      }
    });
  }

  /**
   * Handles messages from the webview
   * @param message - Message received from the webview
   * @remarks Validates message structure before processing commands
   */
  private async handleMessage(message: unknown): Promise<void> {
    if (!this.isValidMessage(message)) {
      this.log('Received invalid message from webview');
      return;
    }

    this.log(`Handling command: ${message.command}`);

    try {
      switch (message.command) {
        case 'start':
          await this.handleStart(message.data);
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
        case 'ready':
          await this.updateState();
          break;
        default:
          this.log(`Unknown command: ${message.command}`);
      }
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : String(error);
      this.logError(`Command '${message.command}' failed: ${errorMessage}`);
      await this.showError(errorMessage);
    }
  }

  /**
   * Type guard for messages from webview
   * @param message - Unknown message to validate
   * @returns True if message has valid structure
   * @remarks Ensures message has required 'command' string property
   */
  private isValidMessage(message: unknown): message is WebviewMessage {
    return (
      typeof message === 'object' &&
      message !== null &&
      'command' in message &&
      typeof (message as { command: unknown }).command === 'string'
    );
  }

  /**
   * Handles start profiling command
   * @param data - Connection settings data from webview
   * @throws Error if connection settings are invalid
   * @remarks Validates settings, starts server if needed, and begins polling
   */
  private async handleStart(data: unknown): Promise<void> {
    this.log('Starting profiling session...');

    if (!this.isConnectionSettings(data)) {
      throw new Error('Invalid connection settings');
    }

    const validationError = validateConnectionSettings(data);
    if (validationError !== undefined) {
      throw new Error(validationError);
    }

    // Start the server if not running
    if (!this.profilerClient.isRunning()) {
      this.log('Starting profiler client...');
      await this.profilerClient.start();
    }

    // Start profiling
    this.log(`Starting profiling session: ${this.sessionName}`);
    await this.profilerClient.startProfiling(this.sessionName, data);

    // Update state
    this.state = ProfilerState.Running;
    this.eventCount = 0;
    this.seenEventKeys.clear();
    await this.updateState();

    // Start polling for events
    this.startPolling();

    this.log('Profiling started successfully');
    await vscode.window.showInformationMessage(
      'Profiling started successfully',
    );
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
    this.eventCount = 0;
    this.seenEventKeys.clear();
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
    this.seenEventKeys.clear();
    await this.postMessage({
      command: 'clearEvents',
    });
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
   * @remarks Filters out previously seen events using Set-based deduplication
   */
  private async pollEvents(): Promise<void> {
    if (
      this.state !== ProfilerState.Running ||
      !this.profilerClient.isRunning()
    ) {
      return;
    }

    try {
      const events = await this.profilerClient.getLastEvents(this.sessionName);

      // Filter out events we've already seen
      const newEvents = events.filter((event) => {
        const key = this.getEventKey(event);
        if (this.seenEventKeys.has(key)) {
          return false;
        }
        this.seenEventKeys.add(key);
        return true;
      });

      if (newEvents.length > 0) {
        this.eventCount += newEvents.length;

        // Convert to rows
        const rows = newEvents.map((event) => toEventRow(event));

        await this.postMessage({
          command: 'addEvents',
          data: rows,
        });

        await this.updateEventCount();
      }
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : String(error);
      this.logError(`Failed to poll events: ${errorMessage}`);
    }
  }

  /**
   * Gets a unique key for an event
   * @param event - Profiler event to generate key for
   * @returns Unique key string for deduplication
   * @remarks Based on ProfilerEvent.GetEventKey in C#; tries event_sequence, then attach_activity_id, then composite key
   */
  private getEventKey(event: ProfilerEvent): string {
    const actions = event.actions ?? {};

    // Option 1: Use event_sequence
    if (
      typeof actions['event_sequence'] === 'number' ||
      typeof actions['event_sequence'] === 'string'
    ) {
      return `seq:${actions['event_sequence']}`;
    }

    // Option 2: Use attach_activity_id
    if (typeof actions['attach_activity_id'] === 'string') {
      return `activity:${actions['attach_activity_id']}`;
    }

    // Option 3: Fallback
    const timestamp = event.timestamp ?? '';
    const name = event.name ?? '';
    const sessionIdRaw = actions['session_id'];
    const sessionId =
      typeof sessionIdRaw === 'string' || typeof sessionIdRaw === 'number'
        ? String(sessionIdRaw)
        : '';

    return `${timestamp}|${name}|${sessionId}`;
  }

  /**
   * Updates the state in the webview
   * @remarks Sends current state and event count to the webview
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
   * Updates the event count in the webview
   * @remarks Called after new events are added to update the UI counter
   */
  private async updateEventCount(): Promise<void> {
    await this.postMessage({
      command: 'updateEventCount',
      data: this.eventCount,
    });
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
   * Posts a message to the webview
   * @param message - Message to send to the webview
   * @remarks No-op if view is not initialized
   */
  private async postMessage(message: WebviewOutgoingMessage): Promise<void> {
    if (this.view) {
      await this.view.webview.postMessage(message);
    }
  }

  /**
   * Disposes the provider and cleans up resources
   * @remarks Stops polling and profiling session if active
   */
  public async dispose(): Promise<void> {
    this.log('Disposing profiler view provider...');
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

    this.log('Profiler view provider disposed');
  }

  /**
   * Logs an informational message to console
   * @param message - Message to log
   * @remarks Includes timestamp and component prefix for debugging
   */
  private log(message: string): void {
    const timestamp = new Date().toISOString();
    this.outputChannel.appendLine(
      `[${timestamp}] [ProfilerViewProvider] ${message}`,
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
      `[${timestamp}] [ProfilerViewProvider] ERROR: ${message}`,
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

    return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src ${webview.cspSource} 'unsafe-inline'; script-src ${webview.cspSource} 'unsafe-inline';">
  <title>Light Query Profiler</title>
  <style>
    body {
      padding: 10px;
      color: var(--vscode-foreground);
      font-family: var(--vscode-font-family);
      font-size: var(--vscode-font-size);
    }

    .section {
      margin-bottom: 20px;
    }

    .section-title {
      font-weight: bold;
      margin-bottom: 10px;
      padding-bottom: 5px;
      border-bottom: 1px solid var(--vscode-panel-border);
    }

    .form-group {
      margin-bottom: 10px;
    }

    label {
      display: block;
      margin-bottom: 4px;
      font-weight: 500;
    }

    input, select {
      width: 100%;
      padding: 4px 8px;
      background-color: var(--vscode-input-background);
      color: var(--vscode-input-foreground);
      border: 1px solid var(--vscode-input-border);
      border-radius: 2px;
      box-sizing: border-box;
    }

    input:focus, select:focus {
      outline: 1px solid var(--vscode-focusBorder);
    }

    .button-group {
      display: flex;
      gap: 8px;
      margin-top: 10px;
    }

    button {
      padding: 6px 14px;
      background-color: var(--vscode-button-background);
      color: var(--vscode-button-foreground);
      border: none;
      border-radius: 2px;
      cursor: pointer;
      font-size: var(--vscode-font-size);
    }

    button:hover {
      background-color: var(--vscode-button-hoverBackground);
    }

    button:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    button.secondary {
      background-color: var(--vscode-button-secondaryBackground);
      color: var(--vscode-button-secondaryForeground);
    }

    button.secondary:hover {
      background-color: var(--vscode-button-secondaryHoverBackground);
    }

    .status-bar {
      padding: 8px;
      background-color: var(--vscode-statusBar-background);
      color: var(--vscode-statusBar-foreground);
      margin-bottom: 10px;
      border-radius: 2px;
      display: flex;
      justify-content: space-between;
      align-items: center;
    }

    .status-indicator {
      display: inline-block;
      width: 10px;
      height: 10px;
      border-radius: 50%;
      margin-right: 6px;
    }

    .status-indicator.stopped {
      background-color: var(--vscode-testing-iconFailed);
    }

    .status-indicator.running {
      background-color: var(--vscode-testing-iconPassed);
    }

    .status-indicator.paused {
      background-color: var(--vscode-testing-iconQueued);
    }

    .events-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 12px;
      margin-top: 10px;
    }

    .events-table th,
    .events-table td {
      padding: 6px 8px;
      text-align: left;
      border-bottom: 1px solid var(--vscode-panel-border);
    }

    .events-table th {
      background-color: var(--vscode-editor-background);
      font-weight: 600;
      position: sticky;
      top: 0;
    }

    .events-table tr:hover {
      background-color: var(--vscode-list-hoverBackground);
    }

    .events-table tr.selected {
      background-color: var(--vscode-list-activeSelectionBackground);
      color: var(--vscode-list-activeSelectionForeground);
    }

    .events-container {
      max-height: 400px;
      overflow-y: auto;
      border: 1px solid var(--vscode-panel-border);
    }

    .error-message {
      padding: 8px;
      background-color: var(--vscode-inputValidation-errorBackground);
      color: var(--vscode-inputValidation-errorForeground);
      border: 1px solid var(--vscode-inputValidation-errorBorder);
      border-radius: 2px;
      margin-bottom: 10px;
    }

    .hidden {
      display: none;
    }

    .query-details {
      margin-top: 10px;
      padding: 10px;
      background-color: var(--vscode-editor-background);
      border: 1px solid var(--vscode-panel-border);
      border-radius: 2px;
      max-height: 200px;
      overflow-y: auto;
      font-family: var(--vscode-editor-font-family);
      font-size: 12px;
      white-space: pre-wrap;
      word-wrap: break-word;
    }
  </style>
</head>
<body>
  <div class="status-bar">
    <div>
      <span class="status-indicator stopped" id="statusIndicator"></span>
      <span id="statusText">Stopped</span>
    </div>
    <div>
      Events: <span id="eventCount">0</span>
    </div>
  </div>

  <div id="errorContainer" class="error-message hidden"></div>

  <div class="section">
    <div class="section-title">Connection Settings</div>

    <div class="form-group">
      <label for="authMode">Authentication Mode</label>
      <select id="authMode">
        ${authModes.map((mode) => `<option value="${mode.value}">${mode.label}</option>`).join('')}
      </select>
    </div>

    <div class="form-group">
      <label for="server">Server</label>
      <input type="text" id="server" placeholder="localhost" />
    </div>

    <div class="form-group">
      <label for="database">Database</label>
      <input type="text" id="database" placeholder="master" />
    </div>

    <div class="form-group" id="usernameGroup">
      <label for="username">Username</label>
      <input type="text" id="username" />
    </div>

    <div class="form-group" id="passwordGroup">
      <label for="password">Password</label>
      <input type="password" id="password" />
    </div>

    <div class="button-group">
      <button id="startBtn">▶️ Start</button>
      <button id="pauseBtn" disabled>⏸️ Pause</button>
      <button id="resumeBtn" disabled class="hidden">▶️ Resume</button>
      <button id="stopBtn" disabled>⏹️ Stop</button>
      <button id="clearBtn" class="secondary">🗑️ Clear</button>
    </div>
  </div>

  <div class="section">
    <div class="section-title">Events</div>
    <div class="events-container">
      <table class="events-table">
        <thead>
          <tr>
            <th>Event</th>
            <th>Timestamp</th>
            <th>Duration</th>
            <th>CPU (µs)</th>
            <th>Reads</th>
            <th>Database</th>
            <th>Application</th>
          </tr>
        </thead>
        <tbody id="eventsTableBody">
          <tr>
            <td colspan="7" style="text-align: center; color: var(--vscode-descriptionForeground);">
              No events captured yet. Click Start to begin profiling.
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div id="queryDetails" class="query-details hidden"></div>
  </div>

  <script>
    (function() {
      const vscode = acquireVsCodeApi();

      // State
      let currentState = 'stopped';
      let selectedEventRow = null;

      // Elements
      const authMode = document.getElementById('authMode');
      const server = document.getElementById('server');
      const database = document.getElementById('database');
      const username = document.getElementById('username');
      const password = document.getElementById('password');
      const usernameGroup = document.getElementById('usernameGroup');
      const passwordGroup = document.getElementById('passwordGroup');
      const startBtn = document.getElementById('startBtn');
      const pauseBtn = document.getElementById('pauseBtn');
      const resumeBtn = document.getElementById('resumeBtn');
      const stopBtn = document.getElementById('stopBtn');
      const clearBtn = document.getElementById('clearBtn');
      const statusIndicator = document.getElementById('statusIndicator');
      const statusText = document.getElementById('statusText');
      const eventCount = document.getElementById('eventCount');
      const eventsTableBody = document.getElementById('eventsTableBody');
      const errorContainer = document.getElementById('errorContainer');
      const queryDetails = document.getElementById('queryDetails');

      // Update credential fields visibility based on auth mode
      authMode.addEventListener('change', () => {
        const mode = parseInt(authMode.value);
        const showCredentials = mode !== 0; // 0 = Windows Auth
        usernameGroup.style.display = showCredentials ? 'block' : 'none';
        passwordGroup.style.display = showCredentials ? 'block' : 'none';
      });

      // Button handlers
      startBtn.addEventListener('click', () => {
        hideError();
        const settings = {
          server: server.value.trim(),
          database: database.value.trim(),
          authenticationMode: parseInt(authMode.value),
          username: username.value.trim(),
          password: password.value,
        };

        vscode.postMessage({
          command: 'start',
          data: settings,
        });
      });

      pauseBtn.addEventListener('click', () => {
        vscode.postMessage({ command: 'pause' });
      });

      resumeBtn.addEventListener('click', () => {
        vscode.postMessage({ command: 'resume' });
      });

      stopBtn.addEventListener('click', () => {
        vscode.postMessage({ command: 'stop' });
      });

      clearBtn.addEventListener('click', () => {
        vscode.postMessage({ command: 'clear' });
      });

      // Handle messages from extension
      window.addEventListener('message', (event) => {
        const message = event.data;

        switch (message.command) {
          case 'updateState':
            updateState(message.data.state);
            eventCount.textContent = message.data.eventCount;
            break;
          case 'updateEventCount':
            eventCount.textContent = message.data;
            break;
          case 'addEvents':
            addEvents(message.data);
            break;
          case 'clearEvents':
            clearEvents();
            break;
          case 'error':
            showError(message.data);
            break;
        }
      });

      function updateState(state) {
        currentState = state;
        statusIndicator.className = 'status-indicator ' + state;

        switch (state) {
          case 'stopped':
            statusText.textContent = 'Stopped';
            startBtn.disabled = false;
            pauseBtn.disabled = true;
            resumeBtn.classList.add('hidden');
            stopBtn.disabled = true;
            break;
          case 'running':
            statusText.textContent = 'Running';
            startBtn.disabled = true;
            pauseBtn.disabled = false;
            resumeBtn.classList.add('hidden');
            stopBtn.disabled = false;
            break;
          case 'paused':
            statusText.textContent = 'Paused';
            startBtn.disabled = true;
            pauseBtn.classList.add('hidden');
            resumeBtn.classList.remove('hidden');
            resumeBtn.disabled = false;
            stopBtn.disabled = false;
            break;
        }
      }

      function addEvents(events) {
        // Remove "no events" message if present
        if (eventsTableBody.children.length === 1 &&
            eventsTableBody.children[0].children.length === 1 &&
            eventsTableBody.children[0].children[0].colSpan === 7) {
          eventsTableBody.innerHTML = '';
        }

        events.forEach(event => {
          const row = document.createElement('tr');
          row.innerHTML = '<td>' + escapeHtml(event.eventName) + '</td>' +
            '<td>' + formatTimestamp(event.timestamp) + '</td>' +
            '<td>' + formatDuration(event.duration) + '</td>' +
            '<td>' + formatNumber(event.cpuTime) + '</td>' +
            '<td>' + formatNumber(event.reads) + '</td>' +
            '<td>' + escapeHtml(event.databaseName || '-') + '</td>' +
            '<td>' + escapeHtml(event.applicationName || '-') + '</td>';

          row.addEventListener('click', () => {
            selectRow(row, event);
          });

          eventsTableBody.insertBefore(row, eventsTableBody.firstChild);
        });
      }

      function selectRow(row, event) {
        if (selectedEventRow) {
          selectedEventRow.classList.remove('selected');
        }

        row.classList.add('selected');
        selectedEventRow = row;

        if (event.queryText) {
          queryDetails.textContent = event.queryText;
          queryDetails.classList.remove('hidden');
        } else {
          queryDetails.classList.add('hidden');
        }
      }

      function clearEvents() {
        eventsTableBody.innerHTML = '<tr>' +
          '<td colspan="7" style="text-align: center; color: var(--vscode-descriptionForeground);">' +
          'No events captured yet. Click Start to begin profiling.' +
          '</td>' +
          '</tr>';
        queryDetails.classList.add('hidden');
        selectedEventRow = null;
      }

      function showError(message) {
        errorContainer.textContent = message;
        errorContainer.classList.remove('hidden');
      }

      function hideError() {
        errorContainer.classList.add('hidden');
      }

      function formatTimestamp(timestamp) {
        if (!timestamp) return '-';
        const date = new Date(timestamp);
        return date.toLocaleTimeString();
      }

      function formatDuration(microseconds) {
        if (microseconds === undefined) return '-';
        if (microseconds < 1000) return microseconds.toFixed(0) + ' µs';
        if (microseconds < 1000000) return (microseconds / 1000).toFixed(2) + ' ms';
        return (microseconds / 1000000).toFixed(2) + ' s';
      }

      function formatNumber(value) {
        if (value === undefined) return '-';
        return value.toLocaleString();
      }

      function escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
      }

      // Initialize
      authMode.dispatchEvent(new Event('change'));
      vscode.postMessage({ command: 'ready' });
    })();
  </script>
</body>
</html>`;
  }
}
