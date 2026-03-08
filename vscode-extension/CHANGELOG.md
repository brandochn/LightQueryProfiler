# Change Log

All notable changes to the "Light Query Profiler" extension will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2025-01-XX

### Added
- Initial release of Light Query Profiler for VS Code
- Real-time SQL Server and Azure SQL Database query profiling
- Support for multiple authentication modes:
  - Windows Authentication (Integrated Security)
  - SQL Server Authentication
  - Azure SQL Database Authentication
- Connection configuration panel with:
  - Server address input
  - Database name input
  - Username/password fields (for SQL Server and Azure SQL auth)
  - Authentication mode selector
- Events table displaying captured queries with:
  - Event name
  - Timestamp
  - Duration
  - CPU time
  - Logical reads
  - Database name
  - Application name
- Query details view showing full SQL text when selecting an event
- Profiler controls:
  - Start button to begin profiling
  - Stop button to end profiling
  - Pause button to temporarily stop collecting events
  - Clear button to remove all captured events
- Real-time event polling (900ms interval)
- Automatic filtering of profiler's own queries
- Status indicator showing profiler state (Stopped/Running/Paused)
- Event counter display
- JSON-RPC communication with .NET backend server
- Cross-platform support (Windows, Linux, macOS)
- Output channel for debugging and logs

### Technical Details
- Built with TypeScript 5.x targeting ES2022
- Uses VS Code Webview API for UI
- Communicates with backend via JSON-RPC over stdin/stdout
- Implements SQL Server Extended Events for minimal overhead
- Follows VS Code extension best practices
- Comprehensive error handling and validation
- Secure credential handling (passwords not logged)

### Requirements
- VS Code 1.85.0 or higher
- .NET 10 SDK or Runtime
- SQL Server 2016+ or Azure SQL Database
- Extended Events permissions on target database

### Known Limitations
- Pause functionality displays in UI but backend implementation pending
- Single active session per instance
- Events not persisted between VS Code sessions
- Requires manual installation of .NET backend server DLL

## [Unreleased]

### Planned Features
- Event filtering and search capabilities
- Export events to CSV/JSON
- Import previously captured events
- Session persistence across VS Code restarts
- Multiple simultaneous profiling sessions
- Query execution plan integration
- Performance metrics dashboard
- Customizable event columns
- Dark/light theme improvements
- Keyboard shortcuts for common actions
- Connection history and favorites
- Backend pause functionality implementation

---

[1.0.0]: https://github.com/your-repo/light-query-profiler/releases/tag/v1.0.0