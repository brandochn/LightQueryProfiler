/**
 * Represents a profiler event captured from SQL Server Extended Events
 */
export interface ProfilerEvent {
  /**
   * Event name (e.g., 'sql_batch_completed', 'rpc_completed')
   */
  name?: string;

  /**
   * Event timestamp in ISO 8601 format
   */
  timestamp?: string;

  /**
   * Event fields containing query-specific data
   */
  fields?: Record<string, unknown>;

  /**
   * Event actions containing session and context data
   */
  actions?: Record<string, unknown>;
}

/**
 * Represents a formatted row for display in the events grid
 */
export interface ProfilerEventRow {
  /**
   * Event name
   */
  eventName: string;

  /**
   * Event timestamp
   */
  timestamp: string;

  /**
   * Duration in microseconds (if available)
   */
  duration?: number;

  /**
   * SQL query text
   */
  queryText?: string;

  /**
   * CPU time in microseconds (if available)
   */
  cpuTime?: number;

  /**
   * Reads count (if available)
   */
  reads?: number;

  /**
   * Writes count (if available)
   */
  writes?: number;

  /**
   * Application name
   */
  applicationName?: string;

  /**
   * Database name
   */
  databaseName?: string;

  /**
   * Login name
   */
  loginName?: string;

  /**
   * Session ID
   */
  sessionId?: number;

  /**
   * Original event data
   */
  rawEvent: ProfilerEvent;
}

/**
 * Converts a ProfilerEvent to a ProfilerEventRow for display
 * @param event - The profiler event to convert
 * @returns Formatted event row
 * @remarks This function extracts and normalizes event data for UI presentation
 * @example
 * ```typescript
 * const event: ProfilerEvent = {
 *   name: 'sql_batch_completed',
 *   timestamp: '2026-03-06T15:30:45.123Z',
 *   fields: { batch_text: 'SELECT * FROM Users', duration: 1500 },
 *   actions: { database_name: 'MyDB' }
 * };
 * const row = toEventRow(event);
 * // row.eventName === 'sql_batch_completed'
 * // row.queryText === 'SELECT * FROM Users'
 * ```
 */
export function toEventRow(event: ProfilerEvent): ProfilerEventRow {
  const fields = event.fields ?? {};
  const actions = event.actions ?? {};

  // Extract query text from different event types
  let queryText: string | undefined;
  if (typeof fields['batch_text'] === 'string') {
    queryText = fields['batch_text'];
  } else if (typeof fields['statement'] === 'string') {
    queryText = fields['statement'];
  }

  // Extract duration (convert to microseconds if needed)
  let duration: number | undefined;
  if (typeof fields['duration'] === 'number') {
    duration = fields['duration'];
  }

  // Extract CPU time
  let cpuTime: number | undefined;
  if (typeof fields['cpu_time'] === 'number') {
    cpuTime = fields['cpu_time'];
  }

  // Extract reads
  let reads: number | undefined;
  if (typeof fields['logical_reads'] === 'number') {
    reads = fields['logical_reads'];
  }

  // Extract writes
  let writes: number | undefined;
  if (typeof fields['writes'] === 'number') {
    writes = fields['writes'];
  }

  // Extract action data
  const applicationName =
    typeof actions['client_app_name'] === 'string'
      ? actions['client_app_name']
      : undefined;

  const databaseName =
    typeof actions['database_name'] === 'string'
      ? actions['database_name']
      : undefined;

  const loginName =
    typeof actions['username'] === 'string' ? actions['username'] : undefined;

  const sessionId =
    typeof actions['session_id'] === 'number'
      ? actions['session_id']
      : undefined;

  return {
    eventName: event.name ?? 'Unknown',
    timestamp: event.timestamp ?? new Date().toISOString(),
    duration,
    queryText,
    cpuTime,
    reads,
    writes,
    applicationName,
    databaseName,
    loginName,
    sessionId,
    rawEvent: event,
  };
}

/**
 * Formats duration from microseconds to human-readable string
 * @param microseconds - Duration in microseconds
 * @returns Formatted duration string
 * @remarks Returns '-' for undefined values
 * @example
 * ```typescript
 * formatDuration(500);      // '500 µs'
 * formatDuration(1500);     // '1.50 ms'
 * formatDuration(2500000);  // '2.50 s'
 * formatDuration(undefined); // '-'
 * ```
 */
export function formatDuration(microseconds: number | undefined): string {
  if (microseconds === undefined) {
    return '-';
  }

  if (microseconds < 1000) {
    return `${microseconds.toFixed(0)} µs`;
  }

  if (microseconds < 1000000) {
    return `${(microseconds / 1000).toFixed(2)} ms`;
  }

  return `${(microseconds / 1000000).toFixed(2)} s`;
}

/**
 * Formats a number with thousand separators
 * @param value - Number to format
 * @returns Formatted number string
 * @remarks Uses locale-specific formatting, returns '-' for undefined values
 * @example
 * ```typescript
 * formatNumber(1000);     // '1,000'
 * formatNumber(1234567);  // '1,234,567'
 * formatNumber(undefined); // '-'
 * ```
 */
export function formatNumber(value: number | undefined): string {
  if (value === undefined) {
    return '-';
  }

  return value.toLocaleString();
}
