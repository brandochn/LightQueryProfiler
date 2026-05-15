import * as crypto from "crypto";
import * as vscode from "vscode";
import { ProfilerClient } from "../services/profiler-client";
import { ConnectionProfile } from "../models/connection-profile";

// Messages the extension HOST receives FROM the webview
type WebviewIncomingMessage =
  | { command: "webviewReady" }
  | { command: "refresh" }
  | { command: "connectionSelected"; data: ConnectionProfile }
  | { command: "startProfiling"; data: ConnectionProfile }
  | { command: "deleteConnection"; data: { id: number; name: string } }
  | { command: "addConnection"; data: ConnectionProfile }
  | { command: "updateConnection"; data: ConnectionProfile }
  | { command: "importFromMssql" }
  | { command: "error"; data: string };

// Messages the extension HOST sends TO the webview
type WebviewOutgoingMessage =
  | { command: "updateConnections"; data: ConnectionProfile[] }
  | { command: "error"; data: string };

/**
 * Manages the "Connections" webview panel.
 * Shows a searchable list of saved connection profiles with Add, Edit, and Delete.
 * Double-clicking a row fires the `onConnectionSelected` callback and closes the panel.
 */
export class ConnectionsPanelProvider implements vscode.Disposable {
  private panel: vscode.WebviewPanel | undefined;

  constructor(
    private readonly extensionUri: vscode.Uri,
    private readonly profilerClient: ProfilerClient,
    private readonly outputChannel: vscode.OutputChannel,
    private readonly onConnectionSelected: (
      connection: ConnectionProfile,
    ) => void,
    private readonly onStartProfiling: (connection: ConnectionProfile) => void,
  ) {}

  /**
   * Opens (or reveals) the Connections panel.
   * Data is loaded once the webview signals it is ready via `webviewReady`.
   */
  public show(): void {
    if (this.panel) {
      this.panel.reveal(vscode.ViewColumn.One);
      void this.loadConnections();
      return;
    }

    this.panel = vscode.window.createWebviewPanel(
      "connections",
      "Connections",
      vscode.ViewColumn.One,
      {
        enableScripts: true,
        localResourceRoots: [this.extensionUri],
      },
    );

    this.panel.webview.html = this.getHtmlContent(this.panel.webview);

    this.panel.webview.onDidReceiveMessage(
      async (message: WebviewIncomingMessage) => {
        await this.handleMessage(message);
      },
    );

    this.panel.onDidDispose(() => {
      this.panel = undefined;
    });
  }

  /**
   * Loads connections from the backend and posts them to the webview.
   * Starts the JSON-RPC server first if it is not yet running.
   */
  public async loadConnections(): Promise<void> {
    try {
      if (!this.profilerClient.isRunning()) {
        this.log("Server not running, starting server process...");
        await this.profilerClient.start();
      }

      const connections = await this.profilerClient.getConnections();
      await this.postMessage({
        command: "updateConnections",
        data: connections,
      });
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : String(error);
      this.logError(`Failed to load connections: ${errorMessage}`);
      await this.postMessage({ command: "error", data: errorMessage });
    }
  }

  /** Disposes the panel if it is open. */
  public dispose(): void {
    this.panel?.dispose();
    this.panel = undefined;
  }

  // ─── Private helpers ──────────────────────────────────────────────────────

  private async handleMessage(message: WebviewIncomingMessage): Promise<void> {
    switch (message.command) {
      case "webviewReady":
        await this.loadConnections();
        break;

      case "refresh":
        await this.loadConnections();
        break;

      case "connectionSelected":
        this.onConnectionSelected(message.data);
        this.panel?.dispose();
        break;

      case "startProfiling":
        this.onStartProfiling(message.data);
        this.panel?.dispose();
        break;

      case "deleteConnection":
        await this.deleteConnection(message.data.id, message.data.name);
        break;

      case "addConnection":
        await this.saveConnection(message.data);
        break;

      case "updateConnection":
        await this.updateConnection(message.data);
        break;

      case "importFromMssql":
        // Forward to the extension host via the command, which handles
        // the full flow: check availability -> count connections -> confirm -> import
        await vscode.commands.executeCommand(
          "lightQueryProfiler.importFromMssql",
        );
        // After import (success or cancel), refresh the connection list
        await this.loadConnections();
        break;

      case "error":
        this.logError(`Webview error: ${message.data}`);
        break;

      default:
        break;
    }
  }

  /**
   * Saves a new connection profile and refreshes the list.
   */
  private async saveConnection(profile: ConnectionProfile): Promise<void> {
    try {
      if (!this.profilerClient.isRunning()) {
        this.log("Server not running, starting server process...");
        await this.profilerClient.start();
      }
      await this.profilerClient.saveConnection(profile);
      await this.loadConnections();
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : String(error);
      this.logError(`Failed to save connection: ${errorMessage}`);
      await this.postMessage({ command: "error", data: errorMessage });
    }
  }

  /**
   * Updates an existing connection profile and refreshes the list.
   */
  private async updateConnection(profile: ConnectionProfile): Promise<void> {
    try {
      if (!this.profilerClient.isRunning()) {
        this.log("Server not running, starting server process...");
        await this.profilerClient.start();
      }
      await this.profilerClient.updateConnection(profile);
      await this.loadConnections();
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : String(error);
      this.logError(`Failed to update connection: ${errorMessage}`);
      await this.postMessage({ command: "error", data: errorMessage });
    }
  }

  /**
   * Deletes a connection by its id after user confirmation, then refreshes the list.
   */
  private async deleteConnection(id: number, name: string): Promise<void> {
    const confirm = await vscode.window.showWarningMessage(
      `Are you sure you want to delete the connection "${name}"?`,
      { modal: false },
      "Delete",
    );

    if (confirm !== "Delete") {
      return;
    }

    try {
      if (!this.profilerClient.isRunning()) {
        this.log("Server not running, starting server process...");
        await this.profilerClient.start();
      }

      await this.profilerClient.deleteConnection(id);
      await this.loadConnections();
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : String(error);
      this.logError(`Failed to delete connection ${id}: ${errorMessage}`);
      await this.postMessage({ command: "error", data: errorMessage });
    }
  }

  private async postMessage(message: WebviewOutgoingMessage): Promise<void> {
    if (this.panel) {
      await this.panel.webview.postMessage(message);
    }
  }

  private log(message: string): void {
    const timestamp = new Date().toISOString();
    this.outputChannel.appendLine(
      `[${timestamp}] [ConnectionsPanelProvider] ${message}`,
    );
  }

  private logError(message: string): void {
    const timestamp = new Date().toISOString();
    this.outputChannel.appendLine(
      `[${timestamp}] [ConnectionsPanelProvider] ERROR: ${message}`,
    );
  }

  private getHtmlContent(_webview: vscode.Webview): string {
    const nonce = crypto.randomBytes(16).toString("hex");

    return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta http-equiv="Content-Security-Policy"
        content="default-src 'none'; style-src 'unsafe-inline'; script-src 'nonce-${nonce}';" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>Connections</title>
  <style>
    *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

    body {
      font-family: var(--vscode-font-family);
      font-size: var(--vscode-font-size);
      color: var(--vscode-editor-foreground);
      background-color: var(--vscode-editor-background);
      display: flex;
      flex-direction: column;
      height: 100vh;
      overflow: hidden;
    }

    .toolbar {
      padding: 8px;
      display: flex;
      gap: 6px;
      align-items: center;
      border-bottom: 1px solid var(--vscode-input-border, #555);
      flex-shrink: 0;
    }

    #searchInput {
      flex: 1;
      padding: 4px 8px;
      background-color: var(--vscode-input-background);
      color: var(--vscode-input-foreground);
      border: 1px solid var(--vscode-input-border, #555);
      outline: none;
      font-size: inherit;
    }

    #searchInput:focus {
      border-color: var(--vscode-focusBorder, #007fd4);
    }

    #refreshBtn, #addBtn, #importMssqlBtn {
      flex-shrink: 0;
      padding: 3px 8px;
      background: var(--vscode-button-secondaryBackground, transparent);
      color: var(--vscode-button-secondaryForeground, var(--vscode-editor-foreground));
      border: 1px solid var(--vscode-input-border, #555);
      cursor: pointer;
      font-size: inherit;
      border-radius: 2px;
      white-space: nowrap;
    }

    #refreshBtn:hover, #addBtn:hover, #importMssqlBtn:hover {
      background: var(--vscode-button-secondaryHoverBackground, var(--vscode-list-hoverBackground));
    }

    #refreshBtn:disabled {
      opacity: 0.5;
      cursor: default;
    }

    .list-container {
      flex: 1;
      overflow-y: auto;
    }

    .connections-table {
      width: 100%;
      border-collapse: collapse;
      table-layout: fixed;
    }

    .connections-table thead {
      position: sticky;
      top: 0;
      z-index: 10;
    }

    .connections-table th {
      font-size: 0.78em;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.5px;
      opacity: 0.7;
      text-align: left;
      padding: 6px 12px;
      background: var(--vscode-editor-background);
      border-bottom: 1px solid var(--vscode-input-border, #555);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .connections-table th:first-child,
    .connections-table td:first-child {
      padding-left: 12px;
    }

    .connections-table th:last-child,
    .connections-table td:last-child {
      padding-right: 12px;
    }

    .col-profile { width: 22%; }
    .col-server  { width: 22%; }
    .col-database { width: 18%; }
    .col-auth    { width: 14%; }
    .col-actions { width: 24%; }

    .connection-row {
      cursor: pointer;
      user-select: none;
      outline: none;
    }

    .connection-row td {
      padding: 6px 12px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .connection-row:hover td {
      background-color: var(--vscode-list-hoverBackground);
    }

    .connection-row.selected td,
    .connection-row:focus td {
      background-color: var(--vscode-list-activeSelectionBackground);
      color: var(--vscode-list-activeSelectionForeground, inherit);
    }

    .item-actions {
      display: flex;
      gap: 4px;
      align-items: center;
    }

    .action-btn {
      padding: 2px 8px;
      border: 1px solid var(--vscode-input-border, #555);
      cursor: pointer;
      font-size: 0.8em;
      border-radius: 2px;
      white-space: nowrap;
      line-height: 1.5;
      font-family: inherit;
    }

    .action-btn:focus {
      outline: 1px solid var(--vscode-focusBorder, #007fd4);
      outline-offset: 1px;
    }

    .start-btn {
      background: var(--vscode-button-background, #0e639c);
      color: var(--vscode-button-foreground, #fff);
    }

    .start-btn:hover {
      background: var(--vscode-button-hoverBackground, #1177bb);
    }

    .delete-btn {
      background: var(--vscode-button-secondaryBackground, transparent);
      color: var(--vscode-button-secondaryForeground, var(--vscode-editor-foreground));
    }

    .delete-btn:hover {
      background: var(--vscode-button-secondaryHoverBackground, var(--vscode-list-hoverBackground));
      color: var(--vscode-errorForeground, #f48771);
      border-color: var(--vscode-errorForeground, #f48771);
    }

    .edit-btn {
      background: var(--vscode-button-secondaryBackground, transparent);
      color: var(--vscode-button-secondaryForeground, var(--vscode-editor-foreground));
    }

    .edit-btn:hover {
      background: var(--vscode-button-secondaryHoverBackground, var(--vscode-list-hoverBackground));
    }

    .profile-name {
      font-weight: 600;
    }

    .catalog-name {
      opacity: 0.85;
    }

    .auth-badge {
      background-color: transparent;
      color: var(--vscode-terminal-ansiCyan, #11a8cd);
      border: 1px solid var(--vscode-terminal-ansiCyan, #11a8cd);
      border-radius: 10px;
      padding: 1px 8px;
      font-size: 0.8em;
      white-space: nowrap;
    }

    .empty-state {
      padding: 24px;
      text-align: center;
      opacity: 0.6;
    }

    /* ── Dialog styles ────────────────────────────────────────────────── */
    .dialog-overlay {
      position: fixed;
      top: 0; left: 0; right: 0; bottom: 0;
      background: rgba(0,0,0,0.5);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 100;
    }
    .dialog-overlay.hidden { display: none; }
    .hidden { display: none; }
    .dialog {
      background: var(--vscode-editor-background);
      border: 1px solid var(--vscode-input-border);
      border-radius: 4px;
      padding: 20px;
      max-width: 480px;
      width: 90%;
    }
    .dialog h2 {
      margin: 0 0 16px 0;
      font-size: 1.1em;
    }
    .form-group {
      margin-bottom: 12px;
    }
    .form-group label {
      display: block;
      margin-bottom: 4px;
      font-size: 0.9em;
      opacity: 0.85;
    }
    .form-group input,
    .form-group select {
      width: 100%;
      padding: 4px 8px;
      background: var(--vscode-input-background);
      color: var(--vscode-input-foreground);
      border: 1px solid var(--vscode-input-border);
      font-family: inherit;
      font-size: inherit;
      box-sizing: border-box;
    }
    .form-group input:focus,
    .form-group select:focus {
      border-color: var(--vscode-focusBorder);
      outline: none;
    }
    .dialog-actions {
      display: flex;
      justify-content: flex-end;
      gap: 8px;
      margin-top: 16px;
    }
  </style>
</head>
<body>
  <div class="toolbar">
    <input type="text" id="searchInput" placeholder="Search by profile, server or database..." autocomplete="off" />
    <button id="addBtn" title="Add a new connection profile" aria-label="Add Connection">+ Add</button>
    <button id="refreshBtn" title="Refresh connections list" aria-label="Refresh">&#8635; Refresh</button>
    <button id="importMssqlBtn" title="Import connections from the MSSQL extension">Import from MSSQL</button>
  </div>
  <div class="list-container">
    <table class="connections-table">
      <thead>
        <tr>
          <th class="col-profile">Profile</th>
          <th class="col-server">Server</th>
          <th class="col-database">Database</th>
          <th class="col-auth">Auth</th>
          <th class="col-actions">Actions</th>
        </tr>
      </thead>
      <tbody id="connectionList">
        <tr><td colspan="5" class="empty-state">Loading...</td></tr>
      </tbody>
    </table>
  </div>

  <!-- Add/Edit Connection Dialog (hidden by default) -->
  <div id="dialogOverlay" class="dialog-overlay hidden">
    <div class="dialog">
      <h2 id="dialogTitle">Add Connection</h2>
      <form id="connectionForm">
        <div class="form-group">
          <label for="profileName">Profile Name</label>
          <input type="text" id="profileName" placeholder="e.g., Production, Dev Local" maxlength="200" autocomplete="off" />
        </div>
        <div class="form-group">
          <label for="dialogAuthMode">Authentication Mode</label>
          <select id="dialogAuthMode">
            <option value="0">Windows Authentication</option>
            <option value="1">SQL Server Authentication</option>
            <option value="2">Azure SQL Database</option>
            <option value="3">Connection String</option>
          </select>
        </div>
        <div id="dialogServerGroup" class="form-group">
          <label for="dialogServer">Server</label>
          <input type="text" id="dialogServer" placeholder="localhost or server.database.windows.net" autocomplete="off" />
        </div>
        <div id="dialogDatabaseGroup" class="form-group hidden">
          <label for="dialogDatabase">Database</label>
          <input type="text" id="dialogDatabase" placeholder="Database name" autocomplete="off" />
        </div>
        <div id="dialogUserGroup" class="form-group hidden">
          <label for="dialogUser">Username</label>
          <input type="text" id="dialogUser" placeholder="Username" autocomplete="off" />
        </div>
        <div id="dialogPasswordGroup" class="form-group hidden">
          <label for="dialogPassword">Password</label>
          <input type="password" id="dialogPassword" placeholder="Password" autocomplete="off" />
        </div>
        <div id="dialogConnStringGroup" class="form-group hidden">
          <label for="dialogConnString">Connection String</label>
          <input type="text" id="dialogConnString" placeholder="Server=...;Database=...;..." autocomplete="off" />
        </div>
        <div class="dialog-actions">
          <button type="button" id="dialogCancelBtn" class="action-btn">Cancel</button>
          <button type="submit" id="dialogSaveBtn" class="action-btn start-btn">Save</button>
        </div>
      </form>
    </div>
  </div>

  <script nonce="${nonce}">
    (function () {
      const vscode = acquireVsCodeApi();
      let allConnections = [];
      let selectedIndex = -1;

      function getAuthLabel(authenticationMode) {
        switch (authenticationMode) {
          case 1: return 'SQL Server';
          case 2: return 'Azure AD';
          case 3: return 'Conn. String';
          default: return 'Windows';
        }
      }

      function renderList(connections) {
        const list = document.getElementById('connectionList');
        if (connections.length === 0) {
          list.innerHTML = '<tr><td colspan="5" class="empty-state">No connections found.</td></tr>';
          selectedIndex = -1;
          return;
        }

        list.innerHTML = connections
          .map(
            (conn, i) =>
              '<tr class="connection-row" data-index="' + i + '" data-id="' + parseInt(String(conn.id), 10) + '" tabindex="0" role="option" aria-selected="false">' +
              '  <td class="profile-name">' + escapeHtml(conn.profileName || conn.dataSource) + '</td>' +
              '  <td class="server-name">' + escapeHtml(conn.dataSource) + '</td>' +
              '  <td class="catalog-name">' + escapeHtml(conn.initialCatalog) + '</td>' +
              '  <td><span class="auth-badge">' + escapeHtml(getAuthLabel(conn.authenticationMode)) + '</span></td>' +
              '  <td><div class="item-actions">' +
              '    <button class="action-btn edit-btn" data-index="' + i + '"' +
              '      title="Edit this connection"' +
              '      aria-label="Edit ' + escapeHtml(conn.profileName || conn.dataSource) + '">' +
              '      &#9998; Edit' +
              '    </button>' +
              '    <button class="action-btn start-btn" data-index="' + i + '"' +
              '      title="Start profiling with this connection"' +
              '      aria-label="Start profiling ' + escapeHtml(conn.profileName || conn.dataSource) + '">' +
              '      &#9654; Start' +
              '    </button>' +
              '    <button class="action-btn delete-btn" data-index="' + i + '" data-id="' + parseInt(String(conn.id), 10) + '"' +
              '      title="Remove this connection"' +
              '      aria-label="Delete ' + escapeHtml(conn.profileName || conn.dataSource) + '">' +
              '      &#x2715; Delete' +
              '    </button>' +
              '  </div></td>' +
              '</tr>'
          )
          .join('');

        // Attach event listeners
        list.querySelectorAll('.connection-row').forEach(function (item) {
          item.addEventListener('dblclick', function () {
            selectItem(item, connections);
          });

          item.addEventListener('click', function () {
            highlightItem(item);
          });

          item.addEventListener('keydown', function (e) {
            if (e.key === 'Enter') {
              selectItem(item, connections);
            } else if (e.key === 'ArrowDown') {
              e.preventDefault();
              moveFocus(1);
            } else if (e.key === 'ArrowUp') {
              e.preventDefault();
              moveFocus(-1);
            }
          });

          // --- action buttons ---
          var startBtn = item.querySelector('.start-btn');
          if (startBtn) {
            startBtn.addEventListener('click', function (e) {
              e.stopPropagation();
              var idx = parseInt(startBtn.getAttribute('data-index') || '0', 10);
              var conn = connections[idx];
              if (conn) {
                vscode.postMessage({ command: 'startProfiling', data: conn });
              }
            });
            startBtn.addEventListener('dblclick', function (e) {
              e.stopPropagation();
            });
          }

          var deleteBtn = item.querySelector('.delete-btn');
          if (deleteBtn) {
            deleteBtn.addEventListener('click', function (e) {
              e.stopPropagation();
              var id = parseInt(deleteBtn.getAttribute('data-id') || '0', 10);
              let idx = parseInt(deleteBtn.getAttribute('data-index') || '0', 10);
              let conn = connections[idx];
              let name = conn ? (conn.profileName || conn.dataSource || 'this connection') : 'this connection';
              vscode.postMessage({ command: 'deleteConnection', data: { id: id, name: name } });
            });
            deleteBtn.addEventListener('dblclick', function (e) {
              e.stopPropagation();
            });
          }

          var editBtn = item.querySelector('.edit-btn');
          if (editBtn) {
            editBtn.addEventListener('click', function (e) {
              e.stopPropagation();
              var idx = parseInt(editBtn.getAttribute('data-index') || '0', 10);
              var conn = connections[idx];
              if (conn) {
                openEditDialog(conn);
              }
            });
            editBtn.addEventListener('dblclick', function (e) {
              e.stopPropagation();
            });
          }
        });
      }

      function highlightItem(item) {
        document.querySelectorAll('.connection-row').forEach(function (el) {
          el.classList.remove('selected');
          el.setAttribute('aria-selected', 'false');
        });
        item.classList.add('selected');
        item.setAttribute('aria-selected', 'true');
        selectedIndex = parseInt(item.getAttribute('data-index') || '0', 10);
      }

      function moveFocus(delta) {
        const items = Array.from(document.querySelectorAll('.connection-row'));
        if (items.length === 0) return;
        const next = Math.max(0, Math.min(items.length - 1, selectedIndex + delta));
        const target = items[next];
        if (target) {
          target.focus();
          highlightItem(target);
        }
      }

      function selectItem(item, connections) {
        const index = parseInt(item.getAttribute('data-index') || '0', 10);
        const conn = connections[index];
        if (conn) {
          vscode.postMessage({ command: 'connectionSelected', data: conn });
        }
      }

      function escapeHtml(str) {
        return String(str)
          .replace(/&/g, '&amp;')
          .replace(/</g, '&lt;')
          .replace(/>/g, '&gt;')
          .replace(/"/g, '&quot;');
      }

      function applyFilter(query) {
        const q = query.toLowerCase();
        if (!q) {
          renderList(allConnections);
          return;
        }
        const filtered = allConnections.filter(function (c) {
          return (
            c.dataSource.toLowerCase().includes(q) ||
            c.initialCatalog.toLowerCase().includes(q) ||
            (c.profileName && c.profileName.toLowerCase().includes(q))
          );
        });
        renderList(filtered);
      }

      // ── Dialog references ─────────────────────────────────────────────────
      const dialogOverlay = document.getElementById('dialogOverlay');
      const dialogTitle = document.getElementById('dialogTitle');
      const connectionForm = document.getElementById('connectionForm');
      const dialogAuthMode = document.getElementById('dialogAuthMode');
      const dialogServerGroup = document.getElementById('dialogServerGroup');
      const dialogDatabaseGroup = document.getElementById('dialogDatabaseGroup');
      const dialogUserGroup = document.getElementById('dialogUserGroup');
      const dialogPasswordGroup = document.getElementById('dialogPasswordGroup');
      const dialogConnStringGroup = document.getElementById('dialogConnStringGroup');
      let editingConnectionId = null; // null = add mode, number = edit mode

      // Toggle fields visibility based on authentication mode
      function updateDialogFields() {
        var mode = parseInt(dialogAuthMode.value, 10);
        var isWindows = mode === 0;
        var needsCreds = mode === 1 || mode === 2;
        var isConnString = mode === 3;
        dialogServerGroup.classList.toggle('hidden', isConnString);
        dialogDatabaseGroup.classList.toggle('hidden', isWindows || isConnString);
        dialogUserGroup.classList.toggle('hidden', !needsCreds);
        dialogPasswordGroup.classList.toggle('hidden', !needsCreds);
        dialogConnStringGroup.classList.toggle('hidden', !isConnString);

        if (isWindows) { document.getElementById('dialogDatabase').value = ''; }
        if (!isConnString) { document.getElementById('dialogConnString').value = ''; }
      }

      dialogAuthMode.addEventListener('change', updateDialogFields);

      // Open dialog in "Add" mode
      function openAddDialog() {
        editingConnectionId = null;
        dialogTitle.textContent = 'Add Connection';
        connectionForm.reset();
        dialogAuthMode.value = '0';
        updateDialogFields();
        dialogOverlay.classList.remove('hidden');
        document.getElementById('profileName').focus();
      }

      // Open dialog in "Edit" mode
      function openEditDialog(connection) {
        editingConnectionId = connection.id;
        dialogTitle.textContent = 'Edit Connection';
        document.getElementById('profileName').value = connection.profileName || '';
        dialogAuthMode.value = String(connection.authenticationMode || 0);
        document.getElementById('dialogServer').value = connection.dataSource || '';
        document.getElementById('dialogDatabase').value = connection.initialCatalog || '';
        document.getElementById('dialogUser').value = connection.userId || '';
        document.getElementById('dialogPassword').value = connection.password || '';
        document.getElementById('dialogConnString').value = connection.connectionString || '';
        updateDialogFields();
        dialogOverlay.classList.remove('hidden');
        document.getElementById('profileName').focus();
      }

      // Close dialog — also clears sensitive fields as defense-in-depth
      function closeDialog() {
        dialogOverlay.classList.add('hidden');
        editingConnectionId = null;
        document.getElementById('dialogPassword').value = '';
        document.getElementById('dialogConnString').value = '';
      }

      document.getElementById('dialogCancelBtn').addEventListener('click', closeDialog);
      document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && !dialogOverlay.classList.contains('hidden')) {
          closeDialog();
        }
      });
      dialogOverlay.addEventListener('click', function (e) {
        if (e.target === dialogOverlay) { closeDialog(); }
      });

      // Handle form submission
      connectionForm.addEventListener('submit', function (e) {
        e.preventDefault();

        var mode = parseInt(dialogAuthMode.value, 10);
        var profileName = document.getElementById('profileName').value.trim();
        var server = document.getElementById('dialogServer').value.trim();
        var database = document.getElementById('dialogDatabase').value.trim();
        var user = document.getElementById('dialogUser').value.trim();
        var password = document.getElementById('dialogPassword').value;
        var connString = document.getElementById('dialogConnString').value.trim();

        // Validation
        if (mode !== 3) {
          if (!server) { alert('Server is required.'); return; }
          if (mode === 2 && !database) { alert('Database is required for Azure SQL Database.'); return; }
          if (mode === 1 || mode === 2) {
            if (!user) { alert('Username is required.'); return; }
            if (!password) { alert('Password is required.'); return; }
          }
        } else {
          if (!connString) { alert('Connection String is required.'); return; }
        }

        // Default database to 'master' for Windows and SQL Server auth (matching main panel)
        var resolvedDatabase = database;
        if (mode !== 2 && mode !== 3 && !database) {
          resolvedDatabase = 'master';
        }

        var connectionData = {
          profileName: profileName || null,
          dataSource: server,
          initialCatalog: resolvedDatabase,
          userId: user || null,
          password: password || null,
          integratedSecurity: mode === 0,
          authenticationMode: mode,
          engineType: undefined,
          connectionString: connString || null
        };

        if (editingConnectionId !== null) {
          connectionData.id = editingConnectionId;
          vscode.postMessage({ command: 'updateConnection', data: connectionData });
        } else {
          vscode.postMessage({ command: 'addConnection', data: connectionData });
        }

        closeDialog();
      });

      // Messages from the extension host
      window.addEventListener('message', function (event) {
        const message = event.data;
        switch (message.command) {
          case 'updateConnections':
            allConnections = message.data || [];
            document.getElementById('refreshBtn').disabled = false;
            applyFilter(document.getElementById('searchInput').value);
            break;
          case 'error':
            document.getElementById('refreshBtn').disabled = false;
            document.getElementById('connectionList').innerHTML =
              '<tr><td colspan="5" class="empty-state">Error: ' + escapeHtml(message.data) + '</td></tr>';
            break;
        }
      });

      // Search input
      document.getElementById('searchInput').addEventListener('input', function (e) {
        applyFilter(e.target.value);
      });

      // Refresh button
      document.getElementById('refreshBtn').addEventListener('click', function () {
        document.getElementById('refreshBtn').disabled = true;
        document.getElementById('connectionList').innerHTML = '<tr><td colspan="5" class="empty-state">Loading...</td></tr>';
        vscode.postMessage({ command: 'refresh' });
      });

      // Add button
      document.getElementById('addBtn').addEventListener('click', function () {
        openAddDialog();
      });

      // Import from MSSQL button
      document.getElementById('importMssqlBtn').addEventListener('click', function () {
        vscode.postMessage({ command: 'importFromMssql' });
      });

      // Signal readiness
      vscode.postMessage({ command: 'webviewReady' });
    }());
  </script>
</body>
</html>`;
  }
}
