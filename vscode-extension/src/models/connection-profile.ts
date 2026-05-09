/**
 * Represents a saved connection profile returned by the backend.
 * Password is plain-text — decrypted by the backend repository layer.
 */
export interface ConnectionProfile {
  id: number;
  dataSource: string;
  initialCatalog: string;
  userId?: string;
  /** Plain-text password — decrypted by the backend before sending. */
  password?: string;
  integratedSecurity: boolean;
  engineType?: number;
  authenticationMode?: number;
  /**
   * Plain-text connection string — decrypted by the backend before sending.
   * Only set when authenticationMode === 3 (ConnectionString).
   * @remarks Never log this value.
   */
  connectionString?: string;
  /** User-assigned descriptive name for this connection profile (e.g., "Production", "Dev Local"). */
  profileName?: string;
}
