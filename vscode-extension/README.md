# Light Query Profiler

A SQL Server and Azure SQL Database query profiler for Visual Studio Code, powered by [Extended Events](https://docs.microsoft.com/en-us/sql/relational-databases/extended-events/quick-start-extended-events-in-sql-server).

## Features

- Real-time query profiling for SQL Server and Azure SQL Database
- Support for Windows Authentication, SQL Server Authentication, and Azure Active Directory
- Syntax-highlighted SQL query viewer
- Event filtering and full-text search
- Sortable, resizable event columns
- Detailed event inspection with tabbed view

## Requirements

- **[.NET 10 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)** must be installed and available in your PATH. This is required to run the profiler backend server.
- SQL Server 2012 or later, or Azure SQL Database
- The SQL login must have `ALTER ANY EVENT SESSION` permission to create Extended Events sessions

## Getting Started

1. Install the extension
2. Open the Command Palette (`Ctrl+Shift+P` / `Cmd+Shift+P`)
3. Run **Light Query Profiler: Show SQL Profiler**
4. Enter your connection details:
   - Server name or IP address
   - Database name
   - Authentication mode and credentials
5. Click **Start** to begin profiling

## Authentication Modes

| Mode | Description |
|---|---|
| Windows Authentication | Uses the current Windows user credentials (Windows only) |
| SQL Server Authentication | Username and password |
| Azure Active Directory | Azure AD authentication for Azure SQL Database |

## Supported Platforms

The extension works on **Windows**, **Linux**, and **macOS**, provided .NET 10 is installed.

> **Note:** Extended Events sessions require appropriate permissions on the SQL Server instance. Azure SQL Database requires at least the `VIEW DATABASE STATE` permission.

## Extension Settings

This extension does not contribute any VS Code settings at this time.

## Known Issues

- Windows Authentication is only available when running VS Code on Windows
- Azure SQL Database Managed Instance may require additional firewall configuration

## License

MIT — see [LICENSE](https://github.com/brandochn/LightQueryProfiler/blob/main/LICENSE.md)
