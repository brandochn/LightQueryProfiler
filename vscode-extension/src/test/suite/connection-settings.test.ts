import * as assert from 'assert';
import { AuthenticationMode } from '../../models/authentication-mode';
import {
  validateConnectionSettings,
  toConnectionString,
  getEngineType,
} from '../../models/connection-settings';
import type { ConnectionSettings } from '../../models/connection-settings';

// ── Helpers ──────────────────────────────────────────────────────────────────

/**
 * Builds a valid Azure SQL Database settings object.
 * All required fields populated so individual tests can override one field at a time.
 */
function makeAzureSettings(overrides: Partial<ConnectionSettings> = {}): ConnectionSettings {
  return {
    server: 'myserver.database.windows.net',
    database: 'MyDatabase',
    authenticationMode: AuthenticationMode.AzureSqlDatabase,
    username: 'azureuser',
    password: 'Secret123!',
    ...overrides,
  };
}

/**
 * Builds a valid SQL Server Auth settings object.
 */
function makeSqlServerAuthSettings(overrides: Partial<ConnectionSettings> = {}): ConnectionSettings {
  return {
    server: 'localhost',
    database: 'master',
    authenticationMode: AuthenticationMode.SqlServerAuth,
    username: 'sa',
    password: 'Password1!',
    ...overrides,
  };
}

/**
 * Builds a valid Windows Auth settings object.
 */
function makeWindowsAuthSettings(overrides: Partial<ConnectionSettings> = {}): ConnectionSettings {
  return {
    server: 'localhost\\SQLEXPRESS',
    database: 'master',
    authenticationMode: AuthenticationMode.WindowsAuth,
    ...overrides,
  };
}

// ── validateConnectionSettings ───────────────────────────────────────────────

suite('validateConnectionSettings', () => {
  // ── Server validation ───────────────────────────────────────────────────

  test('returns error when server is empty string', () => {
    const result = validateConnectionSettings(makeAzureSettings({ server: '' }));
    assert.strictEqual(result, 'Server is required');
  });

  test('returns error when server is whitespace only', () => {
    const result = validateConnectionSettings(makeAzureSettings({ server: '   ' }));
    assert.strictEqual(result, 'Server is required');
  });

  // ── Database validation ─────────────────────────────────────────────────

  test('returns error when database is empty for Azure SQL Database', () => {
    // Mirrors WinForms ConfigureAsync: throws InvalidOperationException when
    // authMode == AzureSQLDatabase and database is blank.
    const result = validateConnectionSettings(makeAzureSettings({ database: '' }));
    assert.strictEqual(result, 'Database is required');
  });

  test('returns error when database is whitespace for Azure SQL Database', () => {
    const result = validateConnectionSettings(makeAzureSettings({ database: '   ' }));
    assert.strictEqual(result, 'Database is required');
  });

  test('returns error when database is empty for SQL Server Auth', () => {
    const result = validateConnectionSettings(makeSqlServerAuthSettings({ database: '' }));
    assert.strictEqual(result, 'Database is required');
  });

  test('returns error when database is empty for Windows Auth', () => {
    const result = validateConnectionSettings(makeWindowsAuthSettings({ database: '' }));
    assert.strictEqual(result, 'Database is required');
  });

  // ── Credentials validation for Azure SQL Database ───────────────────────

  test('returns error when username is empty for Azure SQL Database', () => {
    const result = validateConnectionSettings(makeAzureSettings({ username: '' }));
    assert.strictEqual(result, 'Username is required for SQL Server and Azure SQL authentication');
  });

  test('returns error when username is undefined for Azure SQL Database', () => {
    const result = validateConnectionSettings(makeAzureSettings({ username: undefined }));
    assert.strictEqual(result, 'Username is required for SQL Server and Azure SQL authentication');
  });

  test('returns error when password is empty for Azure SQL Database', () => {
    const result = validateConnectionSettings(makeAzureSettings({ password: '' }));
    assert.strictEqual(result, 'Password is required for SQL Server and Azure SQL authentication');
  });

  test('returns error when password is undefined for Azure SQL Database', () => {
    const result = validateConnectionSettings(makeAzureSettings({ password: undefined }));
    assert.strictEqual(result, 'Password is required for SQL Server and Azure SQL authentication');
  });

  // ── Credentials validation for SQL Server Auth ──────────────────────────

  test('returns error when username is empty for SQL Server Auth', () => {
    const result = validateConnectionSettings(makeSqlServerAuthSettings({ username: '' }));
    assert.strictEqual(result, 'Username is required for SQL Server and Azure SQL authentication');
  });

  test('returns error when password is empty for SQL Server Auth', () => {
    const result = validateConnectionSettings(makeSqlServerAuthSettings({ password: '' }));
    assert.strictEqual(result, 'Password is required for SQL Server and Azure SQL authentication');
  });

  // ── Valid settings return undefined ─────────────────────────────────────

  test('returns undefined for a fully valid Azure SQL Database settings', () => {
    const result = validateConnectionSettings(makeAzureSettings());
    assert.strictEqual(result, undefined);
  });

  test('returns undefined for a fully valid SQL Server Auth settings', () => {
    const result = validateConnectionSettings(makeSqlServerAuthSettings());
    assert.strictEqual(result, undefined);
  });

  test('returns undefined for a fully valid Windows Auth settings', () => {
    const result = validateConnectionSettings(makeWindowsAuthSettings());
    assert.strictEqual(result, undefined);
  });

  test('Windows Auth does not require username or password', () => {
    // Windows Auth settings without username/password should be valid
    const result = validateConnectionSettings(
      makeWindowsAuthSettings({ username: undefined, password: undefined }),
    );
    assert.strictEqual(result, undefined);
  });
});

// ── getEngineType ─────────────────────────────────────────────────────────────

suite('getEngineType', () => {
  // Mirrors WinForms GetDatabaseEngineTypeAsync short-circuit:
  // AzureSQLDatabase auth mode maps directly to EngineType=2 without any DB query.

  test('returns 2 for AzureSqlDatabase authentication mode', () => {
    const result = getEngineType(AuthenticationMode.AzureSqlDatabase);
    assert.strictEqual(result, 2);
  });

  test('returns 1 for WindowsAuth authentication mode', () => {
    const result = getEngineType(AuthenticationMode.WindowsAuth);
    assert.strictEqual(result, 1);
  });

  test('returns 1 for SqlServerAuth authentication mode', () => {
    // SQL Server Auth also maps to SqlServer engine type (value 1),
    // including Azure SQL Managed Instance which supports SQL logins
    // and uses server-scoped XEvents like on-prem SQL Server.
    const result = getEngineType(AuthenticationMode.SqlServerAuth);
    assert.strictEqual(result, 1);
  });
});

// ── toConnectionString ────────────────────────────────────────────────────────

suite('toConnectionString', () => {
  // ── Azure SQL Database ──────────────────────────────────────────────────

  test('Azure SQL: includes Server and Database', () => {
    const cs = toConnectionString(makeAzureSettings());
    assert.ok(cs.includes('Server=myserver.database.windows.net'), `Expected Server in: ${cs}`);
    assert.ok(cs.includes('Database=MyDatabase'), `Expected Database in: ${cs}`);
  });

  test('Azure SQL: includes User Id and Password', () => {
    const cs = toConnectionString(makeAzureSettings());
    assert.ok(cs.includes('User Id=azureuser'), `Expected User Id in: ${cs}`);
    assert.ok(cs.includes('Password=Secret123!'), `Expected Password in: ${cs}`);
  });

  test('Azure SQL: does NOT include Integrated Security', () => {
    const cs = toConnectionString(makeAzureSettings());
    assert.ok(!cs.includes('Integrated Security'), `Unexpected Integrated Security in: ${cs}`);
  });

  test('Azure SQL: includes required connection metadata', () => {
    const cs = toConnectionString(makeAzureSettings());
    assert.ok(cs.includes('Application Name=LightQueryProfiler'), `Expected Application Name in: ${cs}`);
    assert.ok(cs.includes('Connect Timeout=30'), `Expected Connect Timeout in: ${cs}`);
    assert.ok(cs.includes('TrustServerCertificate=true'), `Expected TrustServerCertificate in: ${cs}`);
  });

  // ── Windows Authentication ──────────────────────────────────────────────

  test('Windows Auth: includes Integrated Security=true', () => {
    const cs = toConnectionString(makeWindowsAuthSettings());
    assert.ok(cs.includes('Integrated Security=true'), `Expected Integrated Security in: ${cs}`);
  });

  test('Windows Auth: does NOT include User Id or Password', () => {
    const cs = toConnectionString(makeWindowsAuthSettings());
    assert.ok(!cs.includes('User Id'), `Unexpected User Id in: ${cs}`);
    assert.ok(!cs.includes('Password'), `Unexpected Password in: ${cs}`);
  });

  // ── SQL Server Authentication ───────────────────────────────────────────

  test('SQL Server Auth: includes User Id and Password', () => {
    const cs = toConnectionString(makeSqlServerAuthSettings());
    assert.ok(cs.includes('User Id=sa'), `Expected User Id in: ${cs}`);
    assert.ok(cs.includes('Password=Password1!'), `Expected Password in: ${cs}`);
  });

  test('SQL Server Auth: does NOT include Integrated Security', () => {
    const cs = toConnectionString(makeSqlServerAuthSettings());
    assert.ok(!cs.includes('Integrated Security'), `Unexpected Integrated Security in: ${cs}`);
  });

  // ── Connection string format ────────────────────────────────────────────

  test('connection string ends with semicolon', () => {
    const cs = toConnectionString(makeAzureSettings());
    assert.ok(cs.endsWith(';'), `Expected trailing semicolon in: ${cs}`);
  });

  test('omits User Id when username is undefined', () => {
    const cs = toConnectionString(makeWindowsAuthSettings({ username: undefined }));
    assert.ok(!cs.includes('User Id'), `Unexpected User Id in: ${cs}`);
  });

  test('omits Password when password is undefined', () => {
    const cs = toConnectionString(makeWindowsAuthSettings({ password: undefined }));
    assert.ok(!cs.includes('Password='), `Unexpected Password in: ${cs}`);
  });
});
