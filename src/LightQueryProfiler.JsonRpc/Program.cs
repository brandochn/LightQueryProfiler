using LightQueryProfiler.JsonRpc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StreamJsonRpc;
using System.Diagnostics;

// Setup logging
var services = new ServiceCollection();
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

var serviceProvider = services.BuildServiceProvider();
var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger<Program>();

logger.LogInformation("LightQueryProfiler JSON-RPC Server starting...");

// Setup cancellation token for graceful shutdown
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (sender, e) =>
{
    logger.LogInformation("Shutdown signal received");
    e.Cancel = true;
    cts.Cancel();
};

try
{
    // Create JSON-RPC server instance
    var jsonRpcLogger = loggerFactory.CreateLogger<JsonRpcServer>();
    var rpcServer = new JsonRpcServer(jsonRpcLogger);

    // Setup JSON-RPC over stdin/stdout
    using var jsonRpc = new JsonRpc(Console.OpenStandardInput(), Console.OpenStandardOutput(), rpcServer);

    // Configure JSON-RPC options
    jsonRpc.CancelLocallyInvokedMethodsWhenConnectionIsClosed = true;

    // Start listening
    jsonRpc.StartListening();
    logger.LogInformation("JSON-RPC Server listening on stdin/stdout");

    // Wait for completion or cancellation
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
