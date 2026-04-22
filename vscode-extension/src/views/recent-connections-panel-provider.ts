import * as crypto from 'crypto';
import * as vscode from 'vscode';
import { ProfilerClient } from '../services/profiler-client';
import { RecentConnection } from '../models/recent-connection';

// Messages the extension HOST receives FROM the webview
type WebviewIncomingMessage =
  | { command: 'webviewReady' }
  | { command: 'refresh' }
  | { command: 'connectionSelected'; data: RecentConnection }
  | { command: 'startProfiling'; data: RecentConnection }
  | { command: 'deleteConnection'; data: number }
  | { command: 'error'; data: string };

// Messages the extension HOST sends TO the webview
type WebviewOutgoingMessage =
  | { command: 'updateConnections'; data: RecentConnection[] }
  | { command: 'error'; data: string };

/**
 * Manages the "Recent Connections" webview panel.
 * Shows a searchable list of saved connections. Double-clicking a row fires
 * the `onConnectionSelected` callback and closes the panel.
 */
export class RecentConnectionsPanelProvider implements vscode.Disposable {
  private panel: vscode.WebviewPanel | undefined;

  constructor(
    private readonly extensionUri: vscode.Uri,
    private readonly profilerClient: ProfilerClient,
    private readonly outputChannel: vscode.OutputChannel,
    private readonly onConnectionSelected: (
      connection: RecentConnection,
    ) => void,
    private readonly onStartProfiling: (connection: RecentConnection) => void,
  ) {}

  /**
   * Opens (or reveals) the Recent Connections panel.
   * Data is loaded once the webview signals it is ready via `webviewReady`.
   */
  public show(): void {
    if (this.panel) {
      this.panel.reveal(vscode.ViewColumn.One);
      // Always refresh the list — new connections may have been saved since
      // the panel was last opened without being closed.
      void this.loadConnections();
      return;
    }

    this.panel = vscode.window.createWebviewPanel(
      'recentConnections',
      'Recent Connections',
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
        this.log('Server not running, starting server process...');
        await this.profilerClient.start();
      }

      const connections = await this.profilerClient.getRecentConnections();
      await this.postMessage({
        command: 'updateConnections',
        data: connections,
      });
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : String(error);
      this.logError(`Failed to load recent connections: ${errorMessage}`);
      await this.postMessage({ command: 'error', data: errorMessage });
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
      case 'webviewReady':
        await this.loadConnections();
        break;

      case 'refresh':
        await this.loadConnections();
        break;

      case 'connectionSelected':
        this.onConnectionSelected(message.data);
        this.panel?.dispose();
        break;

      case 'startProfiling':
        this.onStartProfiling(message.data);
        this.panel?.dispose();
        break;

      case 'deleteConnection':
        await this.deleteConnection(message.data);
        break;

      case 'error':
        this.logError(`Webview error: ${message.data}`);
        break;

      default:
        break;
    }
  }

  /**
   * Deletes a recent connection by its id and refreshes the list.
   * Starts the JSON-RPC server first if it is not yet running.
   */
  private async deleteConnection(id: number): Promise<void> {
    try {
      if (!this.profilerClient.isRunning()) {
        this.log('Server not running, starting server process...');
        await this.profilerClient.start();
      }

      await this.profilerClient.deleteRecentConnection(id);
      await this.loadConnections();
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : String(error);
      this.logError(`Failed to delete connection ${id}: ${errorMessage}`);
      await this.postMessage({ command: 'error', data: errorMessage });
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
      `[${timestamp}] [RecentConnectionsPanelProvider] ${message}`,
    );
  }

  private logError(message: string): void {
    const timestamp = new Date().toISOString();
    this.outputChannel.appendLine(
      `[${timestamp}] [RecentConnectionsPanelProvider] ERROR: ${message}`,
    );
  }

  private getHtmlContent(_webview: vscode.Webview): string {
    const nonce = crypto.randomBytes(16).toString('hex');

    return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta http-equiv="Content-Security-Policy"
        content="default-src 'none'; style-src 'unsafe-inline'; script-src 'nonce-${nonce}';" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>Recent Connections</title>
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

    #refreshBtn {
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

    #refreshBtn:hover {
      background: var(--vscode-button-secondaryHoverBackground, var(--vscode-list-hoverBackground));
    }

    #refreshBtn:disabled {
      opacity: 0.5;
      cursor: default;
    }

    .connection-list {
      flex: 1;
      overflow-y: auto;
    }

    .connection-item {
      display: grid;
      grid-template-columns: 1fr 1fr auto auto;
      align-items: center;
      gap: 8px;
      padding: 6px 12px;
      cursor: pointer;
      user-select: none;
      outline: none;
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

    .connection-item:hover {
      background-color: var(--vscode-list-hoverBackground);
    }

    .connection-item.selected,
    .connection-item:focus {
      background-color: var(--vscode-list-activeSelectionBackground);
      color: var(--vscode-list-activeSelectionForeground, inherit);
    }

    .server-name {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .catalog-name {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
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
  </style>
</head>
<body>
  <div class="toolbar">
    <input type="text" id="searchInput" placeholder="Search by server or database..." autocomplete="off" />
    <button id="refreshBtn" title="Refresh connections list" aria-label="Refresh">&#8635; Refresh</button>
  </div>
  <div id="connectionList" class="connection-list">
    <div class="empty-state">Loading...</div>
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
          list.innerHTML = '<div class="empty-state">No recent connections found.</div>';
          selectedIndex = -1;
          return;
        }

        list.innerHTML = connections
          .map(
            (conn, i) =>
              '<div class="connection-item" data-index="' + i + '" data-id="' + parseInt(String(conn.id), 10) + '" tabindex="0" role="option" aria-selected="false">' +
              '  <span class="server-name">' + escapeHtml(conn.dataSource) + '</span>' +
              '  <span class="catalog-name">' + escapeHtml(conn.initialCatalog) + '</span>' +
              '  <span class="auth-badge">' + escapeHtml(getAuthLabel(conn.authenticationMode)) + '</span>' +
              '  <div class="item-actions">' +
              '    <button class="action-btn start-btn" data-index="' + i + '"' +
              '      title="Start profiling with this connection"' +
              '      aria-label="Start profiling ' + escapeHtml(conn.dataSource) + '">' +
              '      &#9654; Start' +
              '    </button>' +
              '    <button class="action-btn delete-btn" data-index="' + i + '" data-id="' + parseInt(String(conn.id), 10) + '"' +
              '      title="Remove this connection from the list"' +
              '      aria-label="Delete ' + escapeHtml(conn.dataSource) + '">' +
              '      &#x2715; Delete' +
              '    </button>' +
              '  </div>' +
              '</div>'
          )
          .join('');

        // Attach event listeners
        list.querySelectorAll('.connection-item').forEach(function (item) {
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
              vscode.postMessage({ command: 'deleteConnection', data: id });
            });
            deleteBtn.addEventListener('dblclick', function (e) {
              e.stopPropagation();
            });
          }
        });
      }

      function highlightItem(item) {
        document.querySelectorAll('.connection-item').forEach(function (el) {
          el.classList.remove('selected');
          el.setAttribute('aria-selected', 'false');
        });
        item.classList.add('selected');
        item.setAttribute('aria-selected', 'true');
        selectedIndex = parseInt(item.getAttribute('data-index') || '0', 10);
      }

      function moveFocus(delta) {
        const items = Array.from(document.querySelectorAll('.connection-item'));
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
            c.initialCatalog.toLowerCase().includes(q)
          );
        });
        renderList(filtered);
      }

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
              '<div class="empty-state">Error: ' + escapeHtml(message.data) + '</div>';
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
        document.getElementById('connectionList').innerHTML = '<div class="empty-state">Loading...</div>';
        vscode.postMessage({ command: 'refresh' });
      });

      // Signal readiness — fires once after DOMContentLoaded
      vscode.postMessage({ command: 'webviewReady' });
    }());
  </script>
</body>
</html>`;
  }
}
