using LightQueryProfiler.JsonRpc;
using LightQueryProfiler.JsonRpc.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LightQueryProfiler.JsonRpc.Tests;

public class JsonRpcServerTests
{
    private readonly Mock<ILogger<JsonRpcServer>> _mockLogger;
    private readonly JsonRpcServer _server;

    public JsonRpcServerTests()
    {
        _mockLogger = new Mock<ILogger<JsonRpcServer>>();
        _server = new JsonRpcServer(_mockLogger.Object);
    }

    [Fact]
    public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new JsonRpcServer(null!));
    }

    [Fact]
    public async Task StartProfilingAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _server.StartProfilingAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StartProfilingAsync_WhenSessionNameIsEmpty_ThrowsArgumentException()
    {
        // Arrange
        var request = new StartProfilingRequest
        {
            SessionName = "",
            EngineType = 1,
            ConnectionString = "Server=localhost;Database=test;"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _server.StartProfilingAsync(request, TestContext.Current.CancellationToken));
        Assert.Contains("SessionName", exception.Message);
    }

    [Fact]
    public async Task StartProfilingAsync_WhenConnectionStringIsEmpty_ThrowsArgumentException()
    {
        // Arrange
        var request = new StartProfilingRequest
        {
            SessionName = "TestSession",
            EngineType = 1,
            ConnectionString = ""
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _server.StartProfilingAsync(request, TestContext.Current.CancellationToken));
        Assert.Contains("ConnectionString", exception.Message);
    }

    [Fact]
    public async Task StartProfilingAsync_WhenEngineTypeIsInvalid_ThrowsArgumentException()
    {
        // Arrange
        var request = new StartProfilingRequest
        {
            SessionName = "TestSession",
            EngineType = 999,
            ConnectionString = "Server=localhost;Database=test;"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _server.StartProfilingAsync(request, TestContext.Current.CancellationToken));
        Assert.Contains("EngineType", exception.Message);
    }

    [Fact]
    public async Task StopProfilingAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _server.StopProfilingAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StopProfilingAsync_WhenSessionNameIsEmpty_ThrowsArgumentException()
    {
        // Arrange
        var request = new StopProfilingRequest
        {
            SessionName = ""
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _server.StopProfilingAsync(request, TestContext.Current.CancellationToken));
        Assert.Contains("SessionName", exception.Message);
    }

    [Fact]
    public async Task StopProfilingAsync_WhenSessionNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new StopProfilingRequest
        {
            SessionName = "NonExistentSession"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _server.StopProfilingAsync(request, TestContext.Current.CancellationToken));
        Assert.Contains("No active profiling session found", exception.Message);
    }

    [Fact]
    public async Task GetLastEventsAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _server.GetLastEventsAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetLastEventsAsync_WhenSessionNameIsEmpty_ThrowsArgumentException()
    {
        // Arrange
        var request = new GetEventsRequest
        {
            SessionName = ""
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _server.GetLastEventsAsync(request, TestContext.Current.CancellationToken));
        Assert.Contains("SessionName", exception.Message);
    }

    [Fact]
    public async Task GetLastEventsAsync_WhenSessionNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new GetEventsRequest
        {
            SessionName = "NonExistentSession"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _server.GetLastEventsAsync(request, TestContext.Current.CancellationToken));
        Assert.Contains("No active profiling session found", exception.Message);
    }

    [Fact]
    public async Task PauseProfilingAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _server.PauseProfilingAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PauseProfilingAsync_WhenSessionNameIsEmpty_ThrowsArgumentException()
    {
        // Arrange
        var request = new StopProfilingRequest
        {
            SessionName = ""
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _server.PauseProfilingAsync(request, TestContext.Current.CancellationToken));
        Assert.Contains("SessionName", exception.Message);
    }

    [Fact]
    public async Task PauseProfilingAsync_WhenSessionNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new StopProfilingRequest
        {
            SessionName = "NonExistentSession"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _server.PauseProfilingAsync(request, TestContext.Current.CancellationToken));
        Assert.Contains("No active profiling session found", exception.Message);
    }
}
