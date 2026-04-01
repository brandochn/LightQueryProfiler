/**
 * Represents a saved recent connection entry returned by the backend.
 * Password is plain-text — decrypted by the backend repository layer.
 */
export interface RecentConnection {
  id: number;
  dataSource: string;
  initialCatalog: string;
  userId?: string;
  /** Plain-text password — decrypted by the backend before sending. */
  password?: string;
  integratedSecurity: boolean;
  engineType?: number;
  authenticationMode?: number;
}
