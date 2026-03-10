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
      // Ensure the .NET server process is running before calling startProfiling
      if (!this.profilerClient.isRunning()) {
        this.log("Server not running, starting server process...");
        await this.profilerClient.start();
      }

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
        eventClass: string;
        textData: string;
        applicationName: string;
        hostName: string;
        ntUserName: string;
        loginName: string;
        clientProcessId: string;
        spid: string;
        startTime: string;
        cpu: string;
        reads: string;
        writes: string;
        duration: string;
        databaseId: string;
        databaseName: string;
      }> = [];

      // Helper to get a string value from fields or actions (all values come as strings from the XML parser)
      const str = (obj: Record<string, unknown> | undefined, ...keys: string[]): string => {
        if (!obj) { return ""; }
        for (const k of keys) {
          const v = obj[k];
          if (v !== undefined && v !== null && String(v).length > 0) { return String(v); }
        }
        return "";
      };

      for (const event of events) {
        const f = event.fields;
        const a = event.actions;

        // TextData: options_text (login/logout), batch_text (sql_batch_*), statement (rpc_*)
        const textData = str(f, "options_text", "batch_text", "statement");

        const displayEvent = {
          eventClass:      event.name ?? "Unknown",
          textData,
          applicationName: str(a, "client_app_name"),
          hostName:        str(a, "client_hostname"),
          ntUserName:      str(a, "nt_username"),
          loginName:       str(a, "server_principal_name", "username"),
          clientProcessId: str(a, "client_pid"),
          spid:            str(a, "session_id"),
          startTime:       event.timestamp ?? "",
          cpu:             str(f, "cpu_time"),
          reads:           str(f, "logical_reads"),
          writes:          str(f, "writes"),
          duration:        str(f, "duration"),
          databaseId:      str(f, "database_id"),
          databaseName:    str(a, "database_name"),
        };

        // Dedup key — mirrors ProfilerEvent.GetEventKey() priority exactly:
        //   1. event_sequence  (unique counter per session, most reliable)
        //   2. attach_activity_id (GUID, unique per activity)
        //   3. timestamp|name|session_id  (weakest, same format as C# fallback)
        const seqKey      = str(a, "event_sequence");
        const activityKey = str(a, "attach_activity_id");
        const sessionId   = str(a, "session_id");
        const eventKey = seqKey
          ? `seq:${seqKey}`
          : activityKey
            ? `activity:${activityKey}`
            : `${displayEvent.startTime}|${displayEvent.eventClass}|${sessionId}`;

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

    .app-title {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 13px;
      font-weight: 600;
      color: var(--vscode-titleBar-activeForeground, var(--vscode-foreground));
      letter-spacing: 0.3px;
      white-space: nowrap;
    }

    .app-title-icon {
      width: 16px;
      height: 16px;
      opacity: 0.85;
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
    }

    .events-table th.sortable {
      cursor: pointer;
      user-select: none;
    }
    .events-table th.sortable:hover {
      color: var(--vscode-foreground);
      background-color: var(--vscode-list-hoverBackground);
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
    }

    /* Tab bar */
    .details-tab-bar {
      display: flex;
      align-items: stretch;
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
    .details-tab-content.active { display: block; }

    /* Text tab */
    .query-code {
      padding: 12px 14px;
      font-family: var(--vscode-editor-font-family, 'Courier New', monospace);
      font-size: 12px;
      line-height: 1.6;
      white-space: pre-wrap;
      word-break: break-word;
      max-height: 200px;
      overflow-y: auto;
      color: var(--vscode-editor-foreground, var(--vscode-foreground));
    }

    /* Basic SQL keyword highlighting */
    .sql-keyword { color: var(--vscode-symbolIcon-keywordForeground, #569cd6); font-weight: 600; }
    .sql-string  { color: var(--vscode-symbolIcon-stringForeground, #ce9178); }
    .sql-number  { color: var(--vscode-symbolIcon-numberForeground, #b5cea8); }
    .sql-comment { color: var(--vscode-symbolIcon-operatorForeground, #6a9955); font-style: italic; }

    /* Details tab — key/value table */
    .details-kv-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 12px;
      max-height: 200px;
      display: block;
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
  </style>
</head>
<body>

  <!-- ── Top header bar ──────────────────────────────────────────── -->
  <div class="app-header">
    <div class="app-title">
      <svg class="app-title-icon" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
        <ellipse cx="8" cy="4" rx="6" ry="2.5" fill="var(--vscode-button-background,#007acc)" opacity="0.9"/>
        <path d="M2 4v4c0 1.38 2.69 2.5 6 2.5s6-1.12 6-2.5V4" stroke="var(--vscode-button-background,#007acc)" stroke-width="1.2" fill="none"/>
        <path d="M2 8v4c0 1.38 2.69 2.5 6 2.5s6-1.12 6-2.5V8" stroke="var(--vscode-button-background,#007acc)" stroke-width="1.2" fill="none" opacity="0.6"/>
        <rect x="9.5" y="8" width="1" height="4" rx="0.5" fill="#f0b429"/>
        <rect x="11.5" y="6" width="1" height="6" rx="0.5" fill="#f0b429"/>
        <rect x="13.5" y="9.5" width="1" height="2.5" rx="0.5" fill="#f0b429"/>
      </svg>
      SQL Server Query Profiler
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
              ${authModes.map((mode) => '<option value="' + mode.value + '">' + mode.label + "</option>").join("")}
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

      <div class="events-container" id="eventsContainer">
        <table class="events-table">
          <colgroup>
            <col/><col/><col/><col/><col/><col/><col/><col/><col/><col/><col/><col/><col/><col/><col/>
          </colgroup>
          <thead>
            <tr>
              <th>EventClass</th>
              <th>TextData</th>
              <th>ApplicationName</th>
              <th>HostName</th>
              <th>NTUserName</th>
              <th>LoginName</th>
              <th>ClientProcessID</th>
              <th>SPID</th>
              <th>StartTime</th>
              <th>CPU</th>
              <th>Reads</th>
              <th>Writes</th>
              <th class="sortable" id="thDuration" title="Sort by Duration">Duration (ms) ↕</th>
              <th>DatabaseID</th>
              <th>DatabaseName</th>
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
      const thDuration       = document.getElementById('thDuration');

      // ── State ───────────────────────────────────────────────────────
      let currentState        = 'stopped';
      let selectedEventRow    = null;
      let allEvents           = [];        // flat array of event objects for stats
      let sortDurDesc         = true;      // sort direction for duration column
      let isStarting          = false;

      // ── Auth mode visibility ────────────────────────────────────────
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
        const settings = {
          server: serverInput.value.trim(),
          database: databaseInput.value.trim() || 'master',
          authenticationMode: parseInt(authMode.value),
          username: usernameInput.value.trim() || undefined,
          password: passwordInput.value || undefined,
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

      errorClose.addEventListener('click', () => errorContainer.classList.add('hidden'));
      queryPanelClose.addEventListener('click', () => {
        queryPanel.classList.add('hidden');
        if (selectedEventRow) {
          selectedEventRow.classList.remove('selected');
          selectedEventRow = null;
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

      // Sort by duration
      thDuration.addEventListener('click', () => {
        sortDurDesc = !sortDurDesc;
        thDuration.textContent = 'Duration (ms) ' + (sortDurDesc ? '↓' : '↑');
        const rows = Array.from(eventsTableBody.querySelectorAll('tr[data-duration]'));
        rows.sort((a, b) => {
          const va = parseFloat(a.dataset.duration || '0');
          const vb = parseFloat(b.dataset.duration || '0');
          return sortDurDesc ? vb - va : va - vb;
        });
        rows.forEach(r => eventsTableBody.appendChild(r));
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
          case 'error':
            setStarting(false);
            showError(msg.data);
            break;
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
      }

      // ── Add events ──────────────────────────────────────────────────
      function addEvents(events) {
        // Remove placeholder row
        const placeholder = eventsTableBody.querySelector('td[colspan]');
        if (placeholder) { eventsTableBody.innerHTML = ''; }

        events.forEach(event => {
          allEvents.push(event);

          // duration arrives as string in microseconds; convert to ms
          const durUs = parseFloat(event.duration || '');
          const durMs = isNaN(durUs) ? null : durUs / 1000;
          const durClass = durMs === null ? '' :
            durMs < 100  ? 'dur-fast' :
            durMs < 1000 ? 'dur-medium' : 'dur-slow';
          const durText = durMs !== null ? durMs.toFixed(2) : '—';

          // TextData: truncate for display, full value stored on event object
          const textDisplay = event.textData
            ? (event.textData.length > 60 ? event.textData.substring(0, 60) + '…' : event.textData)
            : '—';

          const row = document.createElement('tr');
          row.dataset.duration = durMs !== null ? String(durMs) : '0';

          row.innerHTML =
            '<td><span class="event-badge">' + escapeHtml(event.eventClass || '—') + '</span></td>' +
            '<td title="' + escapeHtml(event.textData || '') + '">' + escapeHtml(textDisplay) + '</td>' +
            '<td>' + escapeHtml(event.applicationName || '—') + '</td>' +
            '<td>' + escapeHtml(event.hostName || '—') + '</td>' +
            '<td>' + escapeHtml(event.ntUserName || '—') + '</td>' +
            '<td>' + escapeHtml(event.loginName || '—') + '</td>' +
            '<td>' + escapeHtml(event.clientProcessId || '—') + '</td>' +
            '<td>' + escapeHtml(event.spid || '—') + '</td>' +
            '<td>' + formatTimestamp(event.startTime) + '</td>' +
            '<td>' + escapeHtml(event.cpu || '—') + '</td>' +
            '<td>' + escapeHtml(event.reads || '—') + '</td>' +
            '<td>' + escapeHtml(event.writes || '—') + '</td>' +
            '<td class="' + durClass + '">' + durText + '</td>' +
            '<td>' + escapeHtml(event.databaseId || '—') + '</td>' +
            '<td>' + escapeHtml(event.databaseName || '—') + '</td>';

          row.addEventListener('click', () => selectRow(row, event));
          eventsTableBody.insertBefore(row, eventsTableBody.firstChild);
        });

        updateStats();
      }

      // ── Stats ───────────────────────────────────────────────────────
      function updateStats() {
        // duration is a string in microseconds; convert to ms for display
        const durations = allEvents
          .map(e => { const v = parseFloat(e.duration || ''); return isNaN(v) ? null : v / 1000; })
          .filter(d => d !== null);
        const reads = allEvents
          .map(e => { const v = parseFloat(e.reads || ''); return isNaN(v) ? null : v; })
          .filter(r => r !== null);

        statTotal.textContent = allEvents.length;

        if (durations.length > 0) {
          const avg = durations.reduce((a, b) => a + b, 0) / durations.length;
          const max = Math.max(...durations);
          statAvg.textContent = avg.toFixed(1) + ' ms';
          statMax.textContent = max.toFixed(1) + ' ms';
          statMax.className = 'stat-value ' +
            (max < 100 ? 'dur-fast' : max < 1000 ? 'dur-medium' : 'dur-slow');
        } else {
          statAvg.textContent = '—';
          statMax.textContent = '—';
          statMax.className   = 'stat-value';
        }

        if (reads.length > 0) {
          statReads.textContent = Math.max(...reads).toLocaleString();
        } else {
          statReads.textContent = '—';
        }
      }

      // ── Select row ──────────────────────────────────────────────────
      function selectRow(row, event) {
        if (selectedEventRow) { selectedEventRow.classList.remove('selected'); }
        row.classList.add('selected');
        selectedEventRow = row;

        // ── Text tab: highlighted SQL ────────────────────────────────
        queryCode.innerHTML = event.textData ? highlightSql(event.textData) : '<span style="color:var(--vscode-descriptionForeground);font-style:italic">No text data</span>';

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

      // ── Clear ───────────────────────────────────────────────────────
      function clearEventsUI() {
        eventsTableBody.innerHTML =
          '<tr><td colspan="15" class="no-events">' +
          '<span class="no-events-icon">🔍</span>' +
          'No events captured yet.<br>Configure connection and click <strong>Start</strong> to begin profiling.' +
          '</td></tr>';
        queryPanel.classList.add('hidden');
        selectedEventRow = null;
        errorContainer.classList.add('hidden');
        allEvents = [];
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
        if (!timestamp) { return '—'; }
        // The server already provides an ISO 8601 string (e.g. 2026-03-10T00:26:04.132Z).
        // Display it as-is, replacing the T separator with a space for readability.
        return String(timestamp).replace('T', ' ');
      }

      function formatNumber(num) {
        if (num == null || isNaN(num)) { return '—'; }
        return Number(num).toLocaleString();
      }

      // Basic SQL keyword highlighting (no external deps)
      function highlightSql(sql) {
        const keywords = /\\b(SELECT|FROM|WHERE|JOIN|LEFT|RIGHT|INNER|OUTER|FULL|CROSS|ON|AS|AND|OR|NOT|IN|EXISTS|LIKE|BETWEEN|IS|NULL|ORDER|BY|GROUP|HAVING|DISTINCT|TOP|INTO|INSERT|UPDATE|DELETE|SET|VALUES|CREATE|ALTER|DROP|TABLE|INDEX|VIEW|PROC|PROCEDURE|FUNCTION|EXEC|EXECUTE|DECLARE|BEGIN|END|IF|ELSE|WHILE|RETURN|CAST|CONVERT|CASE|WHEN|THEN|WITH|CTE|UNION|ALL|EXCEPT|INTERSECT|LIMIT|OFFSET|ASC|DESC)\\b/gi;
        const strings  = /('(?:[^']|'')*')/g;
        const comments = /(--[^\\n]*|[/][*][\\s\\S]*?[*][/])/g;
        const numbers  = /\\b(\\d+(?:\\.\\d+)?)\\b/g;

        const escaped = escapeHtml(sql);
        // Order matters: comments first, then strings, then keywords, then numbers
        return escaped
          .replace(comments, '<span class="sql-comment">$1</span>')
          .replace(strings,  '<span class="sql-string">$1</span>')
          .replace(keywords, '<span class="sql-keyword">$&</span>')
          .replace(numbers,  '<span class="sql-number">$1</span>');
      }

    })();
  </script>
</body>
</html>`;
  }
}
