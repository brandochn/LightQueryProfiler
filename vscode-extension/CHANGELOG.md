# Changelog

All notable changes to the Light Query Profiler extension will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
