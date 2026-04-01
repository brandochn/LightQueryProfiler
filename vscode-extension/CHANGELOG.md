# Changelog

All notable changes to the Light Query Profiler extension will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.0] - 2026-03-30

### Added

- **Recent Connections panel**: Automatically saves connection settings when profiling stops.
  Use the new "Show Recent Connections" command to quickly restore a previous connection.
- Cross-platform AES-256-GCM password encryption for stored credentials.

## [1.1.0] - 2026-03-27

### Added

- Export profiling events to a JSON file via the toolbar **Export...** button or the `Light Query Profiler: Export Events` palette command
- Import profiling events from a JSON file via the toolbar **Import...** button or the `Light Query Profiler: Import Events` palette command
- New `EventExportImportService` responsible for serializing/deserializing events, preserving row order (`__RowIndex`) and timestamps (`__Timestamp`)
- Confirmation dialog when importing events over an existing session (replace or cancel)
- Pending-import handshake: events imported while the profiler panel is closed are automatically loaded once the panel is opened
- Host-side `capturedEvents` mirror (up to 10,000 events) used as the source of truth for exports, keeping the extension host and webview in sync

### Changed

- **Export** and **Import** toolbar buttons are enabled only when the profiler is in the `stopped` state, preventing data corruption during live or paused sessions
- `README.md` and root `README.md` updated with an "Export & Import Events" section describing usage and the JSON format

## [1.0.1] - 2026-03-24

### Changed

- Welcome message now only appears on first activation after installation
- Improved user experience by preventing repetitive notification on every VS Code startup

### Technical

- Implemented `globalState` persistence for welcome message display tracking

## [1.0.0] - 2026-03-21

### Added

- Real-time SQL query profiling via Extended Events
- Support for SQL Server (2012+) and Azure SQL Database
- Windows Authentication, SQL Server Authentication, and Azure Active Directory modes
- Syntax-highlighted SQL query viewer using highlight.js
- Collapsible event cards with 15-column event table
- Full-text search and column filtering
- Detailed event inspection pane with tabbed view
- Sortable and resizable columns
- Export and import profiling sessions (JSON format)
- Duplicate event detection
- Cross-platform support: Windows, Linux, macOS (requires .NET 10)
- JSON-RPC communication bridge between VS Code and the .NET backend
