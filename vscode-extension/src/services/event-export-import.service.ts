import * as fs from 'fs';
import * as path from 'path';

/**
 * Represents a single profiler event row as displayed in the events table.
 * All fields are strings to match the webview's flat display format.
 */
export interface DisplayEvent {
  /** SQL event type (e.g. 'sql_batch_completed', 'rpc_completed') */
  eventClass: string;
  /** SQL query text */
  textData: string;
  /** Application name that issued the query */
  applicationName: string;
  /** Client host name */
  hostName: string;
  /** Windows NT user name */
  ntUserName: string;
  /** SQL login name */
  loginName: string;
  /** Client process ID */
  clientProcessId: string;
  /** Session/SPID identifier */
  spid: string;
  /** Event start timestamp (ISO 8601) */
  startTime: string;
  /** CPU time in microseconds */
  cpu: string;
  /** Logical reads count */
  reads: string;
  /** Writes count */
  writes: string;
  /** Duration in microseconds */
  duration: string;
  /** Database ID */
  databaseId: string;
  /** Database name */
  databaseName: string;
}

/**
 * Result returned by a successful import operation.
 */
export interface ImportResult {
  /**
   * Imported events in display format, ordered by `__RowIndex` when available,
   * otherwise in the original JSON array order.
   */
  events: DisplayEvent[];
}

/**
 * Internal wire format written to JSON — extends DisplayEvent with row-order metadata.
 * Fields prefixed with `__` are stripped on import and never shown in the UI.
 */
/* eslint-disable @typescript-eslint/naming-convention */
interface SerializedEvent extends DisplayEvent {
  /** Zero-based position of this row at export time — used to restore order on import */
  __RowIndex: number;
  /** Copy of startTime kept at the top level for easy sorting without parsing */
  __Timestamp: string;
}
/* eslint-enable @typescript-eslint/naming-convention */

/**
 * Service for exporting and importing profiler events to and from JSON files.
 *
 * @remarks
 * All methods are static. File I/O uses the Node `fs.promises` API so the
 * extension host event loop is never blocked. The JSON format is compatible
 * with files produced by the Light Query Profiler desktop (WinForms) application.
 *
 * @example
 * ```typescript
 * // Export
 * await EventExportImportService.exportEvents(events, '/tmp/session.json');
 *
 * // Import
 * const { events } = await EventExportImportService.importEvents('/tmp/session.json');
 * ```
 */
export class EventExportImportService {
  /**
   * Maximum number of events stored in one export file.
   * Matches the `MAX_EVENTS` cap used by the webview's `allEvents` array.
   */
  static readonly maxExportEvents = 10_000;

  // ── Export ────────────────────────────────────────────────────────────────

  /**
   * Serialises `events` as a JSON array and writes the result to `filePath`.
   *
   * Each element in the output array contains all `DisplayEvent` fields plus:
   * - `__RowIndex`  — the element's position in the original array (0-based)
   * - `__Timestamp` — a copy of `startTime` for convenient sorting
   *
   * @param events   - Snapshot of captured events to persist.
   * @param filePath - Absolute path of the destination `.json` file.
   *
   * @throws {Error} When `filePath` is empty.
   * @throws {Error} When `events` is empty.
   * @throws {Error} When the file cannot be written (permission denied, disk full, …).
   */
  public static async exportEvents(
    events: ReadonlyArray<DisplayEvent>,
    filePath: string,
  ): Promise<void> {
    if (!filePath || filePath.trim().length === 0) {
      throw new Error('File path is required');
    }

    if (!events || events.length === 0) {
      throw new Error('No events to export');
    }

    const serialized: SerializedEvent[] = events.map((event, index) => {
      /* eslint-disable @typescript-eslint/naming-convention */
      const entry: SerializedEvent = {
        __RowIndex: index,
        __Timestamp: event.startTime,
        ...event,
      };
      /* eslint-enable @typescript-eslint/naming-convention */
      return entry;
    });

    const json = JSON.stringify(serialized, null, 2);

    try {
      await fs.promises.writeFile(filePath, json, {
        encoding: 'utf8',
        flag: 'w',
      });
    } catch (error) {
      const code = (error as NodeJS.ErrnoException).code;
      if (code === 'EACCES' || code === 'EPERM') {
        throw new Error(
          `Permission denied writing to: ${path.basename(filePath)}`,
        );
      }
      throw new Error(`Failed to write file: ${(error as Error).message}`);
    }
  }

  // ── Import ────────────────────────────────────────────────────────────────

  /**
   * Reads a JSON file produced by `exportEvents` (or the WinForms app) and
   * returns the events sorted by `__RowIndex` when that metadata is present.
   *
   * Field mapping supports both camelCase (VS Code extension) and PascalCase
   * (WinForms desktop) field names so files can be shared across platforms.
   * Metadata fields (`__RowIndex`, `__Timestamp`) are excluded from the result.
   *
   * @param filePath - Absolute path of the source `.json` file.
   * @returns `ImportResult` containing the ordered `DisplayEvent` array.
   *
   * @throws {Error} When `filePath` is empty.
   * @throws {Error} When the file does not exist.
   * @throws {Error} When the file cannot be read.
   * @throws {Error} When the file content is not valid JSON.
   * @throws {Error} When the JSON root is not an array.
   * @throws {Error} When the JSON array is empty.
   */
  public static async importEvents(filePath: string): Promise<ImportResult> {
    if (!filePath || filePath.trim().length === 0) {
      throw new Error('File path is required');
    }

    // ── Read file ──────────────────────────────────────────────────────────
    let content: string;
    try {
      content = await fs.promises.readFile(filePath, 'utf8');
    } catch (error) {
      const code = (error as NodeJS.ErrnoException).code;
      if (code === 'ENOENT') {
        throw new Error(`File not found: ${path.basename(filePath)}`);
      }
      throw new Error(`Cannot read file: ${(error as Error).message}`);
    }

    // ── Parse JSON ─────────────────────────────────────────────────────────
    let parsed: unknown;
    try {
      parsed = JSON.parse(content);
    } catch {
      throw new Error(
        'Invalid JSON file. Please select a valid profiler events file.',
      );
    }

    if (!Array.isArray(parsed)) {
      throw new Error('Invalid format: expected a JSON array of events.');
    }

    if (parsed.length === 0) {
      throw new Error('The selected file contains no events.');
    }

    // ── Sort by __RowIndex (stable; falls back to JSON array order) ────────
    // Cast to unknown[] explicitly so the spread below is type-safe and does
    // not trigger @typescript-eslint/no-unsafe-assignment (Array.isArray()
    // narrows `unknown` to `any[]` in TypeScript, not `unknown[]`).
    const items = parsed as unknown[];
    const sorted = items.slice().sort((a, b) => {
      const ra = a as Record<string, unknown>;
      const rb = b as Record<string, unknown>;
      const aIdx =
        typeof ra['__RowIndex'] === 'number'
          ? ra['__RowIndex']
          : Number.MAX_SAFE_INTEGER;
      const bIdx =
        typeof rb['__RowIndex'] === 'number'
          ? rb['__RowIndex']
          : Number.MAX_SAFE_INTEGER;
      return aIdx - bIdx;
    });

    // ── Map to DisplayEvent ────────────────────────────────────────────────
    // Supports camelCase (VS Code) and PascalCase / legacy (WinForms) keys.
    const events: DisplayEvent[] = sorted.map((item) => {
      const r = item as Record<string, unknown>;

      /**
       * Returns the first non-empty string value found among the given keys,
       * or an empty string when none match.
       */
      const str = (...keys: string[]): string => {
        for (const k of keys) {
          const v = r[k];
          if (v !== undefined && v !== null && String(v).trim().length > 0) {
            return String(v);
          }
        }
        return '';
      };

      return {
        eventClass: str('eventClass', 'EventClass', 'EventName'),
        textData: str('textData', 'TextData'),
        applicationName: str('applicationName', 'ApplicationName'),
        hostName: str('hostName', 'HostName'),
        ntUserName: str('ntUserName', 'NTUserName'),
        loginName: str('loginName', 'LoginName'),
        clientProcessId: str(
          'clientProcessId',
          'ClientProcessId',
          'ClientProcessID',
        ),
        spid: str('spid', 'Spid', 'SPID'),
        startTime: str('startTime', '__Timestamp', 'StartTime'),
        cpu: str('cpu', 'CPU'),
        reads: str('reads', 'Reads'),
        writes: str('writes', 'Writes'),
        duration: str('duration', 'Duration'),
        databaseId: str('databaseId', 'DatabaseId', 'DatabaseID'),
        databaseName: str('databaseName', 'DatabaseName'),
      };
    });

    return { events };
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  /**
   * Generates a timestamped default filename for an export operation.
   *
   * @returns A string in the format `ProfilerEvents_yyyyMMdd_HHmmss.json`,
   *          e.g. `ProfilerEvents_20250115_143022.json`.
   */
  public static generateDefaultFilename(): string {
    const now = new Date();
    const pad = (n: number): string => String(n).padStart(2, '0');
    const datePart = `${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}`;
    const timePart = `${pad(now.getHours())}${pad(now.getMinutes())}${pad(now.getSeconds())}`;
    return `ProfilerEvents_${datePart}_${timePart}.json`;
  }
}
