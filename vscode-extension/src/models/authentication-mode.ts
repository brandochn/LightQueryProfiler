/**
 * Authentication mode options for SQL Server connections
 */
export enum AuthenticationMode {
  /**
   * Windows Authentication (Integrated Security)
   */
  WindowsAuth = 0,

  /**
   * SQL Server Authentication (username/password)
   */
  SqlServerAuth = 1,

  /**
   * Azure SQL Database Authentication
   */
  AzureSqlDatabase = 2,
}

/**
 * Get display string for authentication mode
 * @param mode - Authentication mode to format
 * @returns Human-readable authentication mode string
 */
export function getAuthenticationModeString(mode: AuthenticationMode): string {
  switch (mode) {
    case AuthenticationMode.WindowsAuth:
      return 'Windows Authentication';
    case AuthenticationMode.SqlServerAuth:
      return 'SQL Server Authentication';
    case AuthenticationMode.AzureSqlDatabase:
      return 'Azure SQL Database';
    default:
      return 'Unknown';
  }
}

/**
 * Authentication mode option with display label
 */
export interface AuthenticationModeOption {
  readonly value: AuthenticationMode;
  readonly label: string;
}

/**
 * Get all authentication modes with their display strings
 * @returns Readonly array of authentication mode options
 * @remarks This returns a new array on each call to prevent mutation
 */
export function getAllAuthenticationModes(): ReadonlyArray<AuthenticationModeOption> {
  return [
    {
      value: AuthenticationMode.WindowsAuth,
      label: getAuthenticationModeString(AuthenticationMode.WindowsAuth),
    },
    {
      value: AuthenticationMode.SqlServerAuth,
      label: getAuthenticationModeString(AuthenticationMode.SqlServerAuth),
    },
    {
      value: AuthenticationMode.AzureSqlDatabase,
      label: getAuthenticationModeString(AuthenticationMode.AzureSqlDatabase),
    },
  ];
}
