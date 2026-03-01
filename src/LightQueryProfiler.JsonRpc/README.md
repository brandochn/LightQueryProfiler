# LightQueryProfiler.JsonRpc

JSON-RPC bridge for Light Query Profiler, enabling VS Code extensions and other tools to interact with the profiling service via stdin/stdout communication.

## Overview

This console application provides a **JSON-RPC 2.0** server that exposes the `ProfilerService` functionality through standard input/output streams. It's designed to be spawned by external applications (like VS Code extensions) to enable SQL Server query profiling capabilities.

## Features

- ✅ **Cross-platform**: Runs on Windows, Linux, and macOS (.NET 10)
- ✅ **Standard I/O**: Uses stdin/stdout for communication (no network configuration needed)
- ✅ **JSON-RPC 2.0**: Industry-standard protocol
- ✅ **SQL Server & Azure SQL**: Supports both database engines
- ✅ **Session Management**: Multiple profiling sessions supported
- ✅ **Structured Logging**: Built-in logging with Microsoft.Extensions.Logging

## Protocol

### Communication

The application uses **StreamJsonRpc** to communicate via stdin/stdout with the following methods:

#### `StartProfilingAsync`

Starts a new profiling session.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "StartProfilingAsync",
  "params": {
    "sessionName": "MySession",
    "engineType": 1,
    "connectionString": "Server=localhost;Database=MyDb;Integrated Security=true;"
  }
}
```

**Parameters:**
- `sessionName` (string): Unique name for the profiling session
- `engineType` (int): Database engine type (1 = SQL Server, 2 = Azure SQL Database)
- `connectionString` (string): SQL Server connection string

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": null
}
```

#### `GetLastEventsAsync`

Retrieves the latest profiling events from a session.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "GetLastEventsAsync",
  "params": {
    "sessionName": "MySession"
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": [
    {
      "name": "sql_batch_completed",
      "timestamp": "2024-01-15T10:30:45.123Z",
      "fields": {
        "batch_text": "SELECT * FROM Users",
        "duration": 150000
      },
      "actions": {
        "session_id": 52,
        "client_app_name": "SSMS"
      }
    }
  ]
}
```

#### `StopProfilingAsync`

Stops and removes a profiling session.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "StopProfilingAsync",
  "params": {
    "sessionName": "MySession"
  }
}
```

**Response:**
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": null
}
```

#### `PauseProfilingAsync`

Pauses a profiling session (currently not implemented in ProfilerService).

**Request:**
```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "method": "PauseProfilingAsync",
  "params": {
    "sessionName": "MySession"
  }
}
```

### Error Handling

Errors are returned using standard JSON-RPC error format:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "error": {
    "code": -32603,
    "message": "SessionName cannot be null or empty"
  }
}
```

## Usage

### Command Line

```bash
# Run the JSON-RPC server
dotnet run --project src/LightQueryProfiler.JsonRpc/LightQueryProfiler.JsonRpc.csproj
```

### From TypeScript/Node.js

```typescript
import { spawn } from 'child_process';
import { StreamMessageReader, StreamMessageWriter, createMessageConnection } from 'vscode-jsonrpc/node';

// Spawn the .NET process
const serverProcess = spawn('dotnet', ['run', '--project', 'path/to/LightQueryProfiler.JsonRpc.csproj']);

// Create JSON-RPC connection
const connection = createMessageConnection(
  new StreamMessageReader(serverProcess.stdout),
  new StreamMessageWriter(serverProcess.stdin)
);

connection.listen();

// Start profiling
await connection.sendRequest('StartProfilingAsync', {
  sessionName: 'MySession',
  engineType: 1,
  connectionString: 'Server=localhost;...'
});

// Get events
const events = await connection.sendRequest('GetLastEventsAsync', {
  sessionName: 'MySession'
});

// Stop profiling
await connection.sendRequest('StopProfilingAsync', {
  sessionName: 'MySession'
});

// Cleanup
connection.dispose();
serverProcess.kill();
```

## Building

```bash
# Build the project
dotnet build src/LightQueryProfiler.JsonRpc/LightQueryProfiler.JsonRpc.csproj

# Run tests
dotnet test tests/LightQueryProfiler.JsonRpc.Tests/LightQueryProfiler.JsonRpc.Tests.csproj

# Publish for deployment
dotnet publish src/LightQueryProfiler.JsonRpc/LightQueryProfiler.JsonRpc.csproj -c Release -o ./publish
```

## Architecture

```
┌─────────────────────┐
│  VS Code Extension  │
│    (TypeScript)     │
└──────────┬──────────┘
           │ stdin/stdout
           │ JSON-RPC 2.0
           │
┌──────────▼──────────┐
│  JsonRpcServer      │
│  (This Project)     │
└──────────┬──────────┘
           │
┌──────────▼──────────┐
│  ProfilerService    │
│ ApplicationDbContext│
│  (LightQueryProfiler│
│      .Shared)       │
└──────────┬──────────┘
           │
┌──────────▼──────────┐
│   SQL Server /      │
│ Azure SQL Database  │
│  (Extended Events)  │
└─────────────────────┘
```

## Dependencies

- **LightQueryProfiler.Shared**: Core profiling logic
- **StreamJsonRpc** (2.20.37+): JSON-RPC protocol implementation
- **Microsoft.Extensions.Logging**: Structured logging
- **Microsoft.Extensions.DependencyInjection**: Dependency injection

## Security Considerations

- **Connection strings are passed as parameters** - never hardcoded
- **No network ports exposed** - uses stdin/stdout only
- **Input validation** on all RPC methods
- **Scoped sessions** - each connection string is isolated per session

## Troubleshooting

### Logging

The application logs to console (stderr) by default. To adjust log level, modify `Program.cs`:

```csharp
builder.SetMinimumLevel(LogLevel.Debug); // For verbose logging
```

### Common Issues

1. **"No active profiling session found"**: Ensure you've called `StartProfilingAsync` before other operations
2. **Connection errors**: Verify the connection string has proper permissions for Extended Events
3. **Process hangs**: Check that stdin/stdout aren't being blocked by parent process

## License

See LICENSE.md in the root of the repository.