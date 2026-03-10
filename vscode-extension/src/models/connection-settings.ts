import { AuthenticationMode } from './authentication-mode';

/**
 * Connection settings for SQL Server or Azure SQL Database
 */
export interface ConnectionSettings {
  /**
   * Server address (e.g., localhost, myserver.database.windows.net)
   */
  server: string;

  /**
   * Database name
   */
  database: string;

  /**
   * Authentication mode
   */
  authenticationMode: AuthenticationMode;

  /**
   * Username (for SQL Server Auth and Azure SQL)
   */
  username?: string;

  /**
   * Password (for SQL Server Auth and Azure SQL)
   */
  password?: string;
}

/**
 * Validates connection settings
 * @param settings - Connection settings to validate
 * @returns Validation error message or undefined if valid
 * @remarks Validates required fields based on authentication mode
 */
export function validateConnectionSettings(
  settings: ConnectionSettings,
): string | undefined {
  if (!settings.server || settings.server.trim().length === 0) {
    return 'Server is required';
  }

  if (!settings.database || settings.database.trim().length === 0) {
    return 'Database is required';
  }

  // SQL Server Auth and Azure SQL require credentials
  if (
    settings.authenticationMode === AuthenticationMode.SqlServerAuth ||
    settings.authenticationMode === AuthenticationMode.AzureSqlDatabase
  ) {
    if (!settings.username || settings.username.trim().length === 0) {
      return 'Username is required for SQL Server and Azure SQL authentication';
    }

    if (!settings.password || settings.password.trim().length === 0) {
      return 'Password is required for SQL Server and Azure SQL authentication';
    }
  }

  return undefined;
}

/**
 * Converts connection settings to SQL Server connection string
 * @param settings - Connection settings
 * @returns SQL Server connection string
 * @remarks Never log the returned connection string as it may contain passwords
 */
export function toConnectionString(settings: ConnectionSettings): string {
  const parts: string[] = [
    `Server=${settings.server}`,
    `Database=${settings.database}`,
  ];

  if (settings.authenticationMode === AuthenticationMode.WindowsAuth) {
    parts.push('Integrated Security=true');
  } else {
    if (settings.username) {
      parts.push(`User Id=${settings.username}`);
    }
    if (settings.password) {
      parts.push(`Password=${settings.password}`);
    }
  }

  // Tag the connection so the profiler backend can exclude its own queries
  // (mirrors MainPresenter.ConfigureAsync: builder.ApplicationName = "LightQueryProfiler")
  parts.push('Application Name=LightQueryProfiler');

  // Add timeout settings
  parts.push('Connect Timeout=30');
  parts.push('TrustServerCertificate=true');

  return parts.join(';') + ';';
}

/**
 * Gets the database engine type based on authentication mode
 * @param mode - Authentication mode
 * @returns Engine type (1 = SQL Server, 2 = Azure SQL Database)
 * @remarks Maps authentication mode to the engine type expected by the profiler service
 */
export function getEngineType(mode: AuthenticationMode): number {
  return mode === AuthenticationMode.AzureSqlDatabase ? 2 : 1;
}
