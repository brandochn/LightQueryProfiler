using LightQueryProfiler.JsonRpc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Serialization;
using StreamJsonRpc;

// ─────────────────────────────────────────────────────────────────────────────
// CRITICAL: Capture stdin/stdout raw streams BEFORE anything touches Console.
//
// Background:
//   stdout is owned exclusively by the StreamJsonRpc framing protocol.
//   The .NET Console class wraps the underlying OS streams with a TextWriter
//   that, on Windows, is placed into "synchronised" (non-async) mode the first
//   time Console.Out/Console.Write is called.  Once that happens the underlying
//   Stream returned by Console.OpenStandardOutput() reports CanWrite = false,
//   which causes StreamJsonRpc to throw:
//     "System.ArgumentException: Stream must be writable (Parameter 'stream')"
//
//   Solution: open the raw streams immediately at program start, before ANY
//   logging or Console API calls, then redirect all diagnostic output to stderr.
// ─────────────────────────────────────────────────────────────────────────────
var rawStdin  = Console.OpenStandardInput();
var rawStdout = Console.OpenStandardOutput();

// Redirect stderr for diagnostic logging (stdout belongs to JSON-RPC).
Console.SetOut(System.IO.TextWriter.Null);   // silence any accidental Console.Write

// Setup logging → stderr only
var services = new ServiceCollection();
services.AddLogging(builder =>
{
    builder.AddConsole(options =>
    {
        options.LogToStandardErrorThreshold = LogLevel.Trace;
    });
    builder.SetMinimumLevel(LogLevel.Information);
});

var serviceProvider = services.BuildServiceProvider();
var loggerFactory   = serviceProvider.GetRequiredService<ILoggerFactory>();
var logger          = loggerFactory.CreateLogger<Program>();

logger.LogInformation("LightQueryProfiler JSON-RPC Server starting...");

// Graceful-shutdown token
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    logger.LogInformation("Shutdown signal received");
    e.Cancel = true;
    cts.Cancel();
};

try
{
    var jsonRpcLogger = loggerFactory.CreateLogger<JsonRpcServer>();
    var rpcServer     = new JsonRpcServer(jsonRpcLogger);

    // Configure Newtonsoft.Json camelCase so property names on the wire match
    // the TypeScript ProfilerEvent interface (name, timestamp, fields, actions).
    var formatter = new JsonMessageFormatter();
    formatter.JsonSerializer.ContractResolver = new CamelCasePropertyNamesContractResolver();

    // Use the pre-captured raw streams — both are guaranteed writable/readable.
    using var jsonRpc = new JsonRpc(new HeaderDelimitedMessageHandler(rawStdout, rawStdin, formatter), rpcServer);
    jsonRpc.CancelLocallyInvokedMethodsWhenConnectionIsClosed = true;

    jsonRpc.StartListening();
    logger.LogInformation("JSON-RPC Server listening on stdin/stdout");

    // Signal TypeScript host that the channel is ready.
    // stderr is used because stdout is owned by StreamJsonRpc framing.
    await Console.Error.WriteLineAsync("READY").ConfigureAwait(false);
    await Console.Error.FlushAsync().ConfigureAwait(false);

    await jsonRpc.Completion.WaitAsync(cts.Token).ConfigureAwait(false);

    logger.LogInformation("JSON-RPC Server shutting down gracefully");
    return 0;
}
catch (OperationCanceledException)
{
    logger.LogInformation("JSON-RPC Server cancelled");
    return 130;
}
catch (Exception ex)
{
    logger.LogError(ex, "Fatal error in JSON-RPC Server");
    return 1;
}
