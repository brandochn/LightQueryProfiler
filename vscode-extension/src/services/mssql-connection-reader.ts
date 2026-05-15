import * as vscode from "vscode";
import { ConnectionProfile } from "../models/connection-profile";
import { AuthenticationMode } from "../models/authentication-mode";

/**
 * Represents a raw connection profile from the MSSQL extension's settings.
 * @remarks Only the fields relevant for import are modeled.
 */
interface MssqlConnectionRaw {
  profileName?: string;
  server?: string;
  database?: string;
  authenticationType?: string;
  user?: string;
  savePassword?: boolean;
  connectionString?: string;
  port?: number;
  groupName?: string;
  databaseDisplayName?: string;
}

/**
 * Service for reading connection profiles from the official MSSQL extension (ms-mssql.mssql).
 * @remarks SECURITY: Never log the raw connection objects returned by `readMssqlConnections()`
 * or the mapped `ConnectionProfile` objects. Connection strings and user identities from
 * MSSQL settings may contain sensitive data.
 */
export class MssqlConnectionReader {
  private readonly mssqlExtensionId = "ms-mssql.mssql";

  /**
   * Checks whether the MSSQL extension is installed and active.
   * @returns true if the extension is present and active.
   */
  public isMssqlExtensionAvailable(): boolean {
    const extension = vscode.extensions.getExtension(this.mssqlExtensionId);
    return extension !== undefined && extension.isActive;
  }

  /**
   * Reads all connection profiles from the MSSQL extension's settings.
   * @returns Array of raw MSSQL connection objects, or empty array if none found.
   * @remarks Uses `vscode.workspace.getConfiguration('mssql')` which reads from settings.json
   * regardless of whether the MSSQL extension is currently active.
   */
  public readMssqlConnections(): MssqlConnectionRaw[] {
    try {
      const config = vscode.workspace.getConfiguration("mssql");
      const connections = config.get<MssqlConnectionRaw[]>("connections", []);
      return Array.isArray(connections) ? connections : [];
    } catch (error) {
      // If the mssql section doesn't exist or is malformed, return empty
      return [];
    }
  }

  /**
   * Maps MSSQL authentication type string to Light Query Profiler AuthenticationMode enum.
   * @param authType - Authentication type string from MSSQL extension
   * @returns Corresponding AuthenticationMode value
   */
  public mapAuthenticationType(
    authType: string | undefined,
  ): AuthenticationMode {
    if (!authType) {
      return AuthenticationMode.WindowsAuth;
    }

    switch (authType) {
      case "SqlLogin":
        return AuthenticationMode.SqlServerAuth;
      case "Integrated":
        return AuthenticationMode.WindowsAuth;
      case "AzureMFA":
      case "AzureMFAAndUser":
        return AuthenticationMode.AzureSqlDatabase;
      case "dstsAuth":
        return AuthenticationMode.ConnectionString;
      default:
        return AuthenticationMode.WindowsAuth;
    }
  }

  /**
   * Detects the database engine type from the server address.
   * @param server - Server hostname or address
   * @returns 1 for SQL Server, 2 for Azure SQL Database
   */
  public detectEngineType(server: string | undefined): number {
    if (!server) {
      return 1; // Default to SQL Server
    }
    // Azure SQL Database servers end with database.windows.net
    if (server.toLowerCase().includes("database.windows.net")) {
      return 2; // AzureSqlDatabase
    }
    return 1; // SqlServer
  }

  /**
   * Generates a descriptive profile name from MSSQL connection data.
   * @param raw - Raw MSSQL connection profile
   * @returns Human-readable profile name
   */
  public generateProfileName(raw: MssqlConnectionRaw): string {
    // Use the MSSQL profileName if present
    if (raw.profileName && raw.profileName.trim().length > 0) {
      return raw.profileName.trim();
    }

    // Use groupName + databaseDisplayName if available
    if (raw.groupName && raw.databaseDisplayName) {
      return `${raw.groupName} - ${raw.databaseDisplayName}`;
    }

    // Fallback: server - database
    const server = raw.server || "Unknown Server";
    const database = raw.database || "Unknown Database";
    return `${server} - ${database}`;
  }

  /**
   * Converts a raw MSSQL connection profile to a Light Query Profiler ConnectionProfile.
   * @param raw - Raw MSSQL connection profile from settings
   * @returns Mapped ConnectionProfile (password is always undefined)
   * @remarks Password is intentionally NOT imported -- MSSQL stores passwords in the OS
   * credential manager which is not accessible from third-party extensions.
   */
  public mapToConnectionProfile(raw: MssqlConnectionRaw): ConnectionProfile {
    const server = this.buildServerAddress(raw.server, raw.port);
    const authMode = this.mapAuthenticationType(raw.authenticationType);
    const integratedSecurity = raw.authenticationType === "Integrated";

    // Default database to 'master' for Windows and SQL Server auth when the
    // MSSQL connection has no database set — mirrors the webview Add-Connection
    // dialog behaviour so the backend validation (which requires InitialCatalog)
    // does not reject the imported profile.
    let database = raw.database || "";
    if (
      !database &&
      authMode !== AuthenticationMode.AzureSqlDatabase &&
      authMode !== AuthenticationMode.ConnectionString
    ) {
      database = "master";
    }

    return {
      id: 0, // New connection -- backend will assign the real id
      dataSource: server,
      initialCatalog: database,
      userId: raw.user || undefined,
      password: undefined, // Intentionally NOT imported
      integratedSecurity,
      engineType: this.detectEngineType(raw.server),
      authenticationMode: authMode,
      connectionString:
        authMode === AuthenticationMode.ConnectionString
          ? raw.connectionString || undefined
          : undefined,
      profileName: this.generateProfileName(raw),
    };
  }

  /**
   * Reads all MSSQL connections and maps them to Light Query Profiler format.
   * @returns Array of ConnectionProfile objects ready for import.
   * @remarks Does NOT check `isMssqlExtensionAvailable()` because
   * `vscode.workspace.getConfiguration('mssql')` reads from settings.json
   * regardless of whether the MSSQL extension is currently active (Issue 5 fix).
   */
  public getImportableConnections(): ConnectionProfile[] {
    const rawConnections = this.readMssqlConnections();
    return rawConnections
      .filter((raw) => {
        // Filter out entries without at least a server
        return !!(raw.server && raw.server.trim().length > 0);
      })
      .map((raw) => this.mapToConnectionProfile(raw));
  }

  /**
   * Builds a full server address from hostname and optional port.
   * @param server - Server hostname
   * @param port - Optional port number
   * @returns Server address string (e.g., "localhost,1433" or "myserver.database.windows.net")
   */
  private buildServerAddress(
    server: string | undefined,
    port: number | undefined,
  ): string {
    if (!server) {
      return "";
    }

    if (port && port !== 1433) {
      // Only append port if it's non-default
      return `${server},${port}`;
    }

    return server;
  }
}
