using LightQueryProfiler.JsonRpc;
using LightQueryProfiler.JsonRpc.Models;
using LightQueryProfiler.Shared.Models;
using LightQueryProfiler.Shared.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using LightQueryProfiler.Shared.Services.Interfaces;
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

    // ─── GetRecentConnectionsAsync ───────────────────────────────────────────

    [Fact]
    public async Task GetRecentConnectionsAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var mockRepo = new Mock<IConnectionRepository>();
        var server = new JsonRpcServer(_mockLogger.Object, mockRepo.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            server.GetRecentConnectionsAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetRecentConnectionsAsync_WhenNoConnections_ReturnsEmptyList()
    {
        // Arrange
        var mockRepo = new Mock<IConnectionRepository>();
        mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Connection>());
        var server = new JsonRpcServer(_mockLogger.Object, mockRepo.Object);

        // Act
        var result = await server.GetRecentConnectionsAsync(
            new GetRecentConnectionsRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // ─── SaveRecentConnectionAsync ───────────────────────────────────────────

    [Fact]
    public async Task SaveRecentConnectionAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var mockRepo = new Mock<IConnectionRepository>();
        var server = new JsonRpcServer(_mockLogger.Object, mockRepo.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            server.SaveRecentConnectionAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveRecentConnectionAsync_WhenDataSourceIsEmpty_ThrowsArgumentException()
    {
        // Arrange
        var mockRepo = new Mock<IConnectionRepository>();
        var server = new JsonRpcServer(_mockLogger.Object, mockRepo.Object);
        var request = new SaveRecentConnectionRequest
        {
            DataSource = "",
            InitialCatalog = "MyDatabase"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            server.SaveRecentConnectionAsync(request, TestContext.Current.CancellationToken));
        Assert.Contains("DataSource", exception.Message);
    }

    [Fact]
    public async Task SaveRecentConnectionAsync_WhenInitialCatalogIsEmpty_ThrowsArgumentException()
    {
        // Arrange
        var mockRepo = new Mock<IConnectionRepository>();
        var server = new JsonRpcServer(_mockLogger.Object, mockRepo.Object);
        var request = new SaveRecentConnectionRequest
        {
            DataSource = "localhost",
            InitialCatalog = ""
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            server.SaveRecentConnectionAsync(request, TestContext.Current.CancellationToken));
        Assert.Contains("InitialCatalog", exception.Message);
    }

    [Fact]
    public async Task SaveRecentConnectionAsync_WhenValid_CallsUpsert()
    {
        // Arrange
        var mockRepo = new Mock<IConnectionRepository>();
        mockRepo.Setup(r => r.UpsertAsync(It.IsAny<Connection>())).Returns(Task.CompletedTask);
        var server = new JsonRpcServer(_mockLogger.Object, mockRepo.Object);
        var request = new SaveRecentConnectionRequest
        {
            DataSource = "localhost",
            InitialCatalog = "AdventureWorks"
        };

        // Act
        await server.SaveRecentConnectionAsync(request, TestContext.Current.CancellationToken);

        // Assert
        mockRepo.Verify(r => r.UpsertAsync(It.Is<Connection>(c =>
            c.DataSource == "localhost" &&
            c.InitialCatalog == "AdventureWorks")), Times.Once);
    }

    // ─── ConnectionString mode tests ─────────────────────────────────────────

    [Fact]
    public async Task SaveRecentConnectionAsync_WhenConnectionStringMode_ParsesAndSavesCorrectly()
    {
        // Arrange
        var mockRepo = new Mock<IConnectionRepository>();
        mockRepo.Setup(r => r.UpsertAsync(It.IsAny<Connection>())).Returns(Task.CompletedTask);
        var server = new JsonRpcServer(_mockLogger.Object, mockRepo.Object);
        var request = new SaveRecentConnectionRequest
        {
            DataSource = "",
            InitialCatalog = "",
            AuthenticationMode = 3,
            ConnectionString = "Server=myserver;Database=mydb;User Id=myuser;Password=mypass;"
        };

        // Act
        await server.SaveRecentConnectionAsync(request, TestContext.Current.CancellationToken);

        // Assert
        mockRepo.Verify(r => r.UpsertAsync(It.Is<Connection>(c =>
            c.AuthenticationMode == LightQueryProfiler.Shared.Enums.AuthenticationMode.ConnectionString &&
            c.ConnectionString == "Server=myserver;Database=mydb;User Id=myuser;Password=mypass;" &&
            c.DataSource == "myserver" &&
            c.InitialCatalog == "mydb")), Times.Once);
    }

    [Fact]
    public async Task SaveRecentConnectionAsync_WhenConnectionStringModeAndMissingConnectionString_ThrowsArgumentException()
    {
        // Arrange
        var mockRepo = new Mock<IConnectionRepository>();
        var server = new JsonRpcServer(_mockLogger.Object, mockRepo.Object);
        var request = new SaveRecentConnectionRequest
        {
            DataSource = "",
            InitialCatalog = "",
            AuthenticationMode = 3,
            ConnectionString = ""
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            server.SaveRecentConnectionAsync(request, TestContext.Current.CancellationToken));
        Assert.Contains("request", exception.Message);
    }

    [Fact]
    public async Task GetRecentConnectionsAsync_WhenConnectionStringModeRow_ReturnsDtoWithConnectionString()
    {
        // Arrange
        var mockRepo = new Mock<IConnectionRepository>();
        var storedConnection = new Connection(
            id: 1,
            initialCatalog: "mydb",
            creationDate: DateTime.UtcNow,
            dataSource: "myserver",
            integratedSecurity: false,
            password: null,
            userId: "myuser",
            engineType: null,
            authenticationMode: LightQueryProfiler.Shared.Enums.AuthenticationMode.ConnectionString,
            connectionString: "Server=myserver;Database=mydb;User Id=myuser;Password=mypass;");

        mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Connection> { storedConnection });
        var server = new JsonRpcServer(_mockLogger.Object, mockRepo.Object);

        // Act
        var result = await server.GetRecentConnectionsAsync(
            new GetRecentConnectionsRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(result);
        var dto = result[0];
        Assert.Equal(3, dto.AuthenticationMode);
        Assert.Equal("Server=myserver;Database=mydb;User Id=myuser;Password=mypass;", dto.ConnectionString);
        Assert.Equal("myserver", dto.DataSource);
        Assert.Equal("mydb", dto.InitialCatalog);
    }

    // ─── DeleteRecentConnectionAsync ────────────────────────────────────────────

    [Fact]
    public async Task DeleteRecentConnectionAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var mockRepo = new Mock<IConnectionRepository>();
        var server = new JsonRpcServer(_mockLogger.Object, mockRepo.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            server.DeleteRecentConnectionAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteRecentConnectionAsync_WhenValidId_CallsRepositoryDelete()
    {
        // Arrange
        var mockRepo = new Mock<IConnectionRepository>();
        mockRepo.Setup(r => r.Delete(It.IsAny<int>())).Returns(Task.CompletedTask);
        var server = new JsonRpcServer(_mockLogger.Object, mockRepo.Object);
        var request = new DeleteRecentConnectionRequest { Id = 42 };

        // Act
        await server.DeleteRecentConnectionAsync(request, TestContext.Current.CancellationToken);

        // Assert
        mockRepo.Verify(r => r.Delete(42), Times.Once);
    }

    [Fact]
    public async Task DeleteRecentConnectionAsync_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        var mockRepo = new Mock<IConnectionRepository>();
        mockRepo.Setup(r => r.Delete(It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));
        var server = new JsonRpcServer(_mockLogger.Object, mockRepo.Object);
        var request = new DeleteRecentConnectionRequest { Id = 99 };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            server.DeleteRecentConnectionAsync(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StartProfilingAsync_WhenEngineTypeIsZero_IsValidInput()
    {
        // Arrange — EngineType=0 should NOT throw; it will attempt to connect (and fail in tests, but not on validation)
        var request = new StartProfilingRequest
        {
            SessionName = "TestSession",
            EngineType = 0,
            ConnectionString = "Server=localhost;Database=test;"
        };

        // Act
        // EngineType=0 is now valid (auto-detect sentinel); the method will proceed past
        // the validation guard and fail when it tries to open a real DB connection.
        // We verify that the exception thrown is NOT an ArgumentException about EngineType.
        var exception = await Record.ExceptionAsync(() =>
            _server.StartProfilingAsync(request, TestContext.Current.CancellationToken));

        // Assert — should NOT throw ArgumentException for EngineType
        Assert.True(
            exception == null || exception is not ArgumentException,
            $"Expected no ArgumentException for EngineType=0, but got: {exception?.GetType().Name}: {exception?.Message}");
    }
}
