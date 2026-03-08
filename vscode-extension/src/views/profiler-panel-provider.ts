import * as vscode from "vscode";
import { ProfilerClient } from "../services/profiler-client";
import {
  AuthenticationMode,
  getAllAuthenticationModes,
} from "../models/authentication-mode";
import { ProfilerEvent } from "../models/profiler-event";

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
  Stopped = "stopped",
  Running = "running",
  Paused = "paused",
}

/**
 * Message types sent from webview to extension
 */
interface WebviewIncomingMessage {
  command: "start" | "stop" | "pause" | "resume" | "clear";
  data?: ConnectionSettings;
}

/**
 * Message types sent from extension to webview
 */
interface WebviewOutgoingMessage {
  command:
    | "updateState"
    | "updateEventCount"
    | "addEvents"
    | "clearEvents"
    | "error";
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
  private sessionName = "VSCodeProfilerSession";
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
      "lightQueryProfiler",
      "Light Query Profiler",
      column,
      {
        enableScripts: true,
        retainContextWhenHidden: true,
        localResourceRoots: [this.extensionUri],
      },
    );

    // Set HTML content
    this.panel.webview.html = this.getHtmlContent(this.panel.webview);

    // Set icon
    this.panel.iconPath = {
      light: vscode.Uri.joinPath(this.extensionUri, "media", "icon.svg"),
      dark: vscode.Uri.joinPath(this.extensionUri, "media", "icon.svg"),
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
      this.log("Panel disposed");
      this.stopPolling();
      this.panel = undefined;
    }, undefined);

    this.log("Panel created and shown");
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
        case "start":
          if (message.data && this.isConnectionSettings(message.data)) {
            await this.handleStart(message.data);
          } else {
            await this.showError("Invalid connection settings");
          }
          break;
        case "stop":
          await this.handleStop();
          break;
        case "pause":
          await this.handlePause();
          break;
        case "resume":
          await this.handleResume();
          break;
        case "clear":
          await this.handleClear();
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
    this.log("Starting profiling session...");

    try {
      // Start profiling
      await this.profilerClient.startProfiling(this.sessionName, settings);

      // Update state
      this.state = ProfilerState.Running;
      this.eventCount = 0;
      this.seenEventKeys.clear();
      await this.updateState();

      // Start polling for events
      this.startPolling();

      this.log("Profiling started successfully");
      await vscode.window.showInformationMessage("Profiling started");
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : String(error);
      this.logError(`Failed to start profiling: ${errorMessage}`);
      await this.showError(`Failed to start profiling: ${errorMessage}`);
      throw error;
    }
  }

  /**
   * Updates the webview state
   * @remarks Sends current state and event count to webview
   */
  private async updateState(): Promise<void> {
    await this.postMessage({
      command: "updateState",
      data: {
        state: this.state,
        eventCount: this.eventCount,
      },
    });
  }

  /**
   * Type guard for connection settings
   * @param data - Unknown data to validate
   * @returns True if data has ConnectionSettings structure
   * @remarks Validates required properties: server, database, authenticationMode
   */
  private isConnectionSettings(data: unknown): data is ConnectionSettings {
    if (typeof data !== "object" || data === null) {
      return false;
    }

    const obj = data as Record<string, unknown>;
    return (
      typeof obj.server === "string" &&
      typeof obj.database === "string" &&
      typeof obj.authenticationMode === "number"
    );
  }

  /**
   * Handles stop profiling command
   * @remarks Stops polling, terminates server session, and resets state
   */
  private async handleStop(): Promise<void> {
    this.log("Stopping profiling session...");
    this.stopPolling();

    if (this.profilerClient.isRunning()) {
      await this.profilerClient.stopProfiling(this.sessionName);
    }

    this.state = ProfilerState.Stopped;
    this.eventCount = 0;
    this.seenEventKeys.clear();
    await this.updateState();

    this.log("Profiling stopped");
    await vscode.window.showInformationMessage("Profiling stopped");
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
    this.log("Clearing events");
    this.eventCount = 0;
    this.seenEventKeys.clear();
    await this.postMessage({
      command: "clearEvents",
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
    try {
      const events: ProfilerEvent[] = await this.profilerClient.getLastEvents(
        this.sessionName,
      );

      if (!events || events.length === 0) {
        return;
      }

      const newEvents: Array<{
        eventName: string;
        timestamp: string;
        duration: number;
        cpuTime: number;
        reads: number;
        databaseName?: string;
        applicationName?: string;
        hostname?: string;
        queryText?: string;
      }> = [];

      for (const event of events) {
        // Convert ProfilerEvent to display format
        const displayEvent = {
          eventName: event.name || "Unknown",
          timestamp: event.timestamp || new Date().toISOString(),
          duration: (event.fields?.duration as number) || 0,
          cpuTime: (event.fields?.cpu_time as number) || 0,
          reads: (event.fields?.logical_reads as number) || 0,
          databaseName: event.actions?.database_name as string | undefined,
          applicationName: event.actions?.client_app_name as string | undefined,
          hostname: event.actions?.client_hostname as string | undefined,
          queryText: event.fields?.statement as string | undefined,
        };

        const eventKey = this.createEventKey(displayEvent);

        if (!this.seenEventKeys.has(eventKey)) {
          this.seenEventKeys.add(eventKey);
          newEvents.push(displayEvent);
        }
      }

      if (newEvents.length > 0) {
        this.eventCount += newEvents.length;

        await this.postMessage({
          command: "addEvents",
          data: newEvents,
        });

        await this.postMessage({
          command: "updateEventCount",
          data: this.eventCount,
        });
      }
    } catch (error) {
      this.logError(`Error polling events: ${String(error)}`);
    }
  }

  /**
   * Creates a unique key for an event
   * @param event - Profiler event
   * @returns Unique key string
   * @remarks Uses timestamp, event name, and query text hash for uniqueness
   */
  private createEventKey(event: {
    timestamp: string;
    eventName: string;
    queryText?: string;
  }): string {
    return `${event.timestamp}-${event.eventName}-${event.queryText?.substring(0, 100) || ""}`;
  }

  /**
   * Shows an error message in the webview and as a VS Code notification
   * @param message - Error message to display
   * @remarks Logs error and shows both in webview and VS Code UI
   */
  private async showError(message: string): Promise<void> {
    this.logError(message);
    await this.postMessage({
      command: "error",
      data: message,
    });
    await vscode.window.showErrorMessage(`Light Query Profiler: ${message}`);
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
    this.log("Disposing profiler panel provider...");
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

    this.log("Profiler panel provider disposed");
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

    return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src ${webview.cspSource} 'unsafe-inline'; script-src ${webview.cspSource} 'unsafe-inline';">
  <title>Light Query Profiler</title>
  <style>
    body {
      padding: 20px;
      color: var(--vscode-foreground);
      font-family: var(--vscode-font-family);
      font-size: var(--vscode-font-size);
      max-width: 1400px;
      margin: 0 auto;
    }

    .header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 20px;
      padding-bottom: 15px;
      border-bottom: 1px solid var(--vscode-panel-border);
    }

    .title {
      font-size: 20px;
      font-weight: 600;
      color: var(--vscode-foreground);
    }

    .status-bar {
      display: flex;
      gap: 20px;
      align-items: center;
      padding: 12px 16px;
      background-color: var(--vscode-editor-background);
      border: 1px solid var(--vscode-panel-border);
      border-radius: 4px;
      margin-bottom: 20px;
    }

    .status-indicator {
      width: 12px;
      height: 12px;
      border-radius: 50%;
      display: inline-block;
      margin-right: 8px;
    }

    .status-indicator.stopped {
      background-color: var(--vscode-testing-iconFailed);
    }

    .status-indicator.running {
      background-color: var(--vscode-testing-iconPassed);
      animation: pulse 1.5s ease-in-out infinite;
    }

    .status-indicator.paused {
      background-color: var(--vscode-notificationsWarningIcon-foreground);
    }

    @keyframes pulse {
      0%, 100% { opacity: 1; }
      50% { opacity: 0.5; }
    }

    .section {
      margin-bottom: 25px;
      padding: 16px;
      background-color: var(--vscode-editor-background);
      border: 1px solid var(--vscode-panel-border);
      border-radius: 4px;
    }

    .section-title {
      font-weight: 600;
      font-size: 14px;
      margin-bottom: 12px;
      color: var(--vscode-foreground);
    }

    .form-group {
      margin-bottom: 12px;
    }

    label {
      display: block;
      margin-bottom: 4px;
      font-weight: 500;
      font-size: 13px;
    }

    input[type="text"],
    input[type="password"],
    select {
      width: 100%;
      padding: 6px 8px;
      background-color: var(--vscode-input-background);
      color: var(--vscode-input-foreground);
      border: 1px solid var(--vscode-input-border);
      border-radius: 2px;
      font-family: var(--vscode-font-family);
      font-size: var(--vscode-font-size);
      box-sizing: border-box;
    }

    input:focus,
    select:focus {
      outline: 1px solid var(--vscode-focusBorder);
      outline-offset: -1px;
    }

    .button-group {
      display: flex;
      gap: 8px;
      margin-top: 12px;
    }

    button {
      padding: 6px 14px;
      background-color: var(--vscode-button-background);
      color: var(--vscode-button-foreground);
      border: none;
      border-radius: 2px;
      cursor: pointer;
      font-family: var(--vscode-font-family);
      font-size: var(--vscode-font-size);
      font-weight: 500;
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

    .hidden {
      display: none;
    }

    .events-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 13px;
    }

    .events-table th,
    .events-table td {
      padding: 8px 10px;
      text-align: left;
      border-bottom: 1px solid var(--vscode-panel-border);
    }

    .events-table th {
      background-color: var(--vscode-editor-background);
      font-weight: 600;
      position: sticky;
      top: 0;
      z-index: 10;
    }

    .events-table tr:hover {
      background-color: var(--vscode-list-hoverBackground);
      cursor: pointer;
    }

    .events-table tr.selected {
      background-color: var(--vscode-list-activeSelectionBackground);
      color: var(--vscode-list-activeSelectionForeground);
    }

    .events-container {
      max-height: 500px;
      overflow-y: auto;
      border: 1px solid var(--vscode-panel-border);
      border-radius: 4px;
    }

    .error-message {
      padding: 10px 12px;
      background-color: var(--vscode-inputValidation-errorBackground);
      color: var(--vscode-inputValidation-errorForeground);
      border: 1px solid var(--vscode-inputValidation-errorBorder);
      border-radius: 4px;
      margin-bottom: 15px;
    }

    .query-details {
      margin-top: 15px;
      padding: 12px;
      background-color: var(--vscode-editor-background);
      border: 1px solid var(--vscode-panel-border);
      border-radius: 4px;
      max-height: 300px;
      overflow-y: auto;
      font-family: var(--vscode-editor-font-family);
      font-size: 13px;
      white-space: pre-wrap;
      word-wrap: break-word;
      line-height: 1.5;
    }

    .no-events {
      padding: 40px;
      text-align: center;
      color: var(--vscode-descriptionForeground);
      font-style: italic;
    }
  </style>
</head>
<body>
  <div class="header">
    <div class="title">SQL Server Query Profiler</div>
    <div class="status-bar">
      <div>
        <span class="status-indicator stopped" id="statusIndicator"></span>
        <span id="statusText">Stopped</span>
      </div>
      <div>
        Events: <span id="eventCount">0</span>
      </div>
    </div>
  </div>

  <div id="errorContainer" class="error-message hidden"></div>

  <div class="section">
    <div class="section-title">Connection Settings</div>

    <div class="form-group">
      <label for="authMode">Authentication Mode</label>
      <select id="authMode">
        ${authModes.map((mode) => '<option value="' + mode.value + '">' + mode.label + "</option>").join("")}
      </select>
    </div>

    <div class="form-group">
      <label for="server">Server</label>
      <input type="text" id="server" placeholder="localhost" value="localhost" />
    </div>

    <div class="form-group">
      <label for="database">Database</label>
      <input type="text" id="database" placeholder="master" value="master" />
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
      <button id="startBtn">▶ Start</button>
      <button id="pauseBtn" class="secondary" disabled>⏸ Pause</button>
      <button id="resumeBtn" class="secondary hidden">▶ Resume</button>
      <button id="stopBtn" class="secondary" disabled>⏹ Stop</button>
      <button id="clearBtn" class="secondary">🗑 Clear</button>
    </div>
  </div>

  <div class="section">
    <div class="section-title">Captured Events</div>
    <div class="events-container">
      <table class="events-table">
        <thead>
          <tr>
            <th>Event</th>
            <th>Timestamp</th>
            <th>Duration (ms)</th>
            <th>CPU (µs)</th>
            <th>Reads</th>
            <th>Database</th>
            <th>Application</th>
            <th>Host</th>
          </tr>
        </thead>
        <tbody id="eventsTableBody">
          <tr>
            <td colspan="8" class="no-events">No events captured yet. Click Start to begin profiling.</td>
          </tr>
        </tbody>
      </table>
    </div>
    <div id="queryDetails" class="query-details hidden"></div>
  </div>

  <script>
    (function() {
      const vscode = acquireVsCodeApi();

      const authMode = document.getElementById('authMode');
      const serverInput = document.getElementById('server');
      const databaseInput = document.getElementById('database');
      const usernameInput = document.getElementById('username');
      const passwordInput = document.getElementById('password');
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
      const queryDetails = document.getElementById('queryDetails');
      const errorContainer = document.getElementById('errorContainer');

      let currentState = 'stopped';
      let selectedEventRow = null;

      // Update auth mode visibility
      authMode.addEventListener('change', () => {
        const mode = parseInt(authMode.value);
        const requiresCredentials = mode === 1 || mode === 2;

        if (requiresCredentials) {
          usernameGroup.classList.remove('hidden');
          passwordGroup.classList.remove('hidden');
        } else {
          usernameGroup.classList.add('hidden');
          passwordGroup.classList.add('hidden');
        }
      });

      // Trigger initial visibility update
      authMode.dispatchEvent(new Event('change'));

      // Restore previously saved connection settings
      const savedState = vscode.getState();
      if (savedState) {
        if (savedState.server) { serverInput.value = savedState.server; }
        if (savedState.database) { databaseInput.value = savedState.database; }
        if (typeof savedState.authenticationMode === 'number') {
          authMode.value = String(savedState.authenticationMode);
          authMode.dispatchEvent(new Event('change'));
        }
        if (savedState.username) { usernameInput.value = savedState.username; }
      }

      // Button event handlers
      startBtn.addEventListener('click', () => {
        const settings = {
          server: serverInput.value.trim() || 'localhost',
          database: databaseInput.value.trim() || 'master',
          authenticationMode: parseInt(authMode.value),
          username: usernameInput.value.trim() || undefined,
          password: passwordInput.value || undefined
        };

        // Persist connection settings (excluding password for security)
        vscode.setState({
          server: settings.server,
          database: settings.database,
          authenticationMode: settings.authenticationMode,
          username: settings.username,
        });

        vscode.postMessage({ command: 'start', data: settings });
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
            eventsTableBody.children[0].children[0].colSpan === 8) {
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
            '<td>' + escapeHtml(event.applicationName || '-') + '</td>' +
            '<td>' + escapeHtml(event.hostname || '-') + '</td>';

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
        eventsTableBody.innerHTML = '<tr><td colspan="7" class="no-events">No events captured yet. Click Start to begin profiling.</td></tr>';
        queryDetails.classList.add('hidden');
        selectedEventRow = null;
        errorContainer.classList.add('hidden');
      }

      function showError(message) {
        errorContainer.textContent = message;
        errorContainer.classList.remove('hidden');
      }

      function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
      }

      function formatTimestamp(timestamp) {
        try {
          const date = new Date(timestamp);
          return date.toLocaleTimeString() + '.' + date.getMilliseconds().toString().padStart(3, '0');
        } catch {
          return timestamp;
        }
      }

      function formatDuration(duration) {
        if (duration == null || isNaN(duration)) {
          return '-';
        }
        return duration.toFixed(2);
      }

      function formatNumber(num) {
        if (num == null || isNaN(num)) {
          return '-';
        }
        return num.toLocaleString();
      }
    })();
  </script>
</body>
</html>`;
  }
}
