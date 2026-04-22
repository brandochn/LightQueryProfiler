using LightQueryProfiler.JsonRpc.Models;
using LightQueryProfiler.Shared.Data;
using LightQueryProfiler.Shared.Enums;
using LightQueryProfiler.Shared.Factories;
using LightQueryProfiler.Shared.Models;
using LightQueryProfiler.Shared.Repositories;
using LightQueryProfiler.Shared.Repositories.Interfaces;
using LightQueryProfiler.Shared.Services;
using LightQueryProfiler.Shared.Services.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using StreamJsonRpc;

namespace LightQueryProfiler.JsonRpc;

/// <summary>
/// JSON-RPC server that exposes profiling operations for VS Code extension
/// </summary>
public class JsonRpcServer
{
    private readonly ILogger<JsonRpcServer> _logger;
    private readonly Dictionary<string, IProfilerService> _activeSessions;
    private readonly Dictionary<string, IApplicationDbContext> _activeContexts;
    private readonly IConnectionRepository _connectionRepository;
    private readonly IDatabaseEngineDetector _engineDetector;

    public JsonRpcServer(ILogger<JsonRpcServer> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _activeSessions = new Dictionary<string, IProfilerService>();
        _activeContexts = new Dictionary<string, IApplicationDbContext>();
        _connectionRepository = new ConnectionRepository(
            new SqliteContext(),
            new AesGcmPasswordProtectionService());
        _engineDetector = new DatabaseEngineDetector();
    }

    /// <summary>
    /// Internal constructor for unit testing — allows injection of a mock repository
    /// without requiring a real SQLite database on disk.
    /// </summary>
    internal JsonRpcServer(ILogger<JsonRpcServer> logger, IConnectionRepository connectionRepository, IDatabaseEngineDetector? engineDetector = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(connectionRepository);
        _logger = logger;
        _activeSessions = new Dictionary<string, IProfilerService>();
        _activeContexts = new Dictionary<string, IApplicationDbContext>();
        _connectionRepository = connectionRepository;
        _engineDetector = engineDetector ?? new DatabaseEngineDetector();
    }

    /// <summary>
    /// Starts a profiling session with the specified parameters
    /// </summary>
    /// <remarks>
    /// UseSingleObjectParameterDeserialization = true is required because the TypeScript
    /// client sends the three fields (SessionName, EngineType, ConnectionString) as a
    /// single JSON object.  Without it StreamJsonRpc tries to match them as three
    /// positional parameters and throws "Unable to find method 'StartProfilingAsync/3'".
    /// </remarks>
    [JsonRpcMethod("StartProfilingAsync", UseSingleObjectParameterDeserialization = true)]
    public async Task StartProfilingAsync(StartProfilingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.SessionName))
        {
            throw new ArgumentException("SessionName cannot be null or empty", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ConnectionString))
        {
            throw new ArgumentException("ConnectionString cannot be null or empty", nameof(request));
        }

        // Allow 0 (auto-detect for ConnectionString mode) or a defined DatabaseEngineType value.
        if (request.EngineType != 0 && !Enum.IsDefined(typeof(DatabaseEngineType), request.EngineType))
        {
            throw new ArgumentException($"Invalid EngineType: {request.EngineType}", nameof(request));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Starting profiling session: {SessionName} with engine type: {EngineType}",
                request.SessionName, request.EngineType);
        }

        try
        {
            // Guarantee the profiler's own SQL connections are tagged with "LightQueryProfiler"
            // as the Application Name. ProfilerService.IsProfilerGeneratedEvent uses
            // client_app_name to exclude the profiler's XEvent management queries (create session,
            // read ring buffer, etc.) from the captured event stream. In standard auth modes this
            // is handled by toConnectionString() on the TypeScript client side; for ConnectionString
            // mode we normalise here at the server level so all modes are covered consistently.
            var csBuilder = new SqlConnectionStringBuilder(request.ConnectionString);
            csBuilder.ApplicationName = "LightQueryProfiler";
            var dbContext = new ApplicationDbContext(csBuilder.ConnectionString);

            DatabaseEngineType effectiveEngineType;
            if (request.EngineType == 0)
            {
                effectiveEngineType = await _engineDetector.DetectEngineTypeAsync(dbContext, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                effectiveEngineType = (DatabaseEngineType)request.EngineType;
            }

            var xEventRepository = new XEventRepository(dbContext);
            xEventRepository.SetEngineType(effectiveEngineType);

            var xEventService = new XEventService();
            var profilerService = new ProfilerService(xEventRepository, xEventService);

            var template = ProfilerSessionTemplateFactory.CreateTemplate(effectiveEngineType);

            await Task.Run(() => profilerService.StartProfiling(request.SessionName, template), cancellationToken)
                .ConfigureAwait(false);

            _activeSessions[request.SessionName] = profilerService;
            _activeContexts[request.SessionName] = dbContext;

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Profiling session started successfully: {SessionName} with engine type: {EngineType}",
                    request.SessionName, effectiveEngineType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start profiling session: {SessionName}", request.SessionName);
            throw;
        }
    }

    /// <summary>
    /// Stops the specified profiling session
    /// </summary>
    /// <remarks>
    /// UseSingleObjectParameterDeserialization = true is required because the TypeScript
    /// client sends the fields as a single JSON object.  Without it StreamJsonRpc tries
    /// to match them as positional parameters and throws "Unable to find method".
    /// </remarks>
    [JsonRpcMethod("StopProfilingAsync", UseSingleObjectParameterDeserialization = true)]
    public async Task StopProfilingAsync(StopProfilingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.SessionName))
        {
            throw new ArgumentException("SessionName cannot be null or empty", nameof(request));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Stopping profiling session: {SessionName}", request.SessionName);
        }

        try
        {
            if (!_activeSessions.TryGetValue(request.SessionName, out var profilerService))
            {
                throw new InvalidOperationException($"No active profiling session found: {request.SessionName}");
            }

            await Task.Run(() => profilerService.StopProfiling(request.SessionName), cancellationToken)
                .ConfigureAwait(false);

            // Clean up
            _activeSessions.Remove(request.SessionName);
            _activeContexts.Remove(request.SessionName);


            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Profiling session stopped successfully: {SessionName}", request.SessionName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop profiling session: {SessionName}", request.SessionName);
            throw;
        }
    }

    /// <summary>
    /// Retrieves the latest events from the specified profiling session
    /// </summary>
    /// <remarks>
    /// UseSingleObjectParameterDeserialization = true is required because the TypeScript
    /// client sends the fields as a single JSON object.  Without it StreamJsonRpc tries
    /// to match them as positional parameters and throws "Unable to find method".
    /// </remarks>
    [JsonRpcMethod("GetLastEventsAsync", UseSingleObjectParameterDeserialization = true)]
    public async Task<List<ProfilerEventDto>> GetLastEventsAsync(GetEventsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.SessionName))
        {
            throw new ArgumentException("SessionName cannot be null or empty", nameof(request));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Retrieving events for session: {SessionName}", request.SessionName);
        }

        try
        {
            if (!_activeSessions.TryGetValue(request.SessionName, out var profilerService))
            {
                throw new InvalidOperationException($"No active profiling session found: {request.SessionName}");
            }

            var events = await profilerService.GetLastEventsAsync(request.SessionName)
                .ConfigureAwait(false);

            // Convert to DTOs
            var eventDtos = events.Select(e => new ProfilerEventDto
            {
                Name = e.Name,
                Timestamp = e.Timestamp,
                Fields = e.Fields,
                Actions = e.Actions
            }).ToList();

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Retrieved {Count} events for session: {SessionName}", eventDtos.Count, request.SessionName);
            }

            return eventDtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve events for session: {SessionName}", request.SessionName);
            throw;
        }
    }

    /// <summary>
    /// Pauses the specified profiling session (not yet implemented in ProfilerService)
    /// </summary>
    /// <remarks>
    /// UseSingleObjectParameterDeserialization = true is required because the TypeScript
    /// client sends the fields as a single JSON object.  Without it StreamJsonRpc tries
    /// to match them as positional parameters and throws "Unable to find method".
    /// </remarks>
    [JsonRpcMethod("PauseProfilingAsync", UseSingleObjectParameterDeserialization = true)]
    public async Task PauseProfilingAsync(StopProfilingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.SessionName))
        {
            throw new ArgumentException("SessionName cannot be null or empty", nameof(request));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Pausing profiling session: {SessionName}", request.SessionName);
        }

        try
        {
            if (!_activeSessions.TryGetValue(request.SessionName, out var profilerService))
            {
                throw new InvalidOperationException($"No active profiling session found: {request.SessionName}");
            }

            await Task.Run(() => profilerService.PauseProfiling(request.SessionName), cancellationToken)
                .ConfigureAwait(false);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Profiling session paused successfully: {SessionName}", request.SessionName);
            }
        }
        catch (NotImplementedException)
        {
            _logger.LogWarning("PauseProfiling is not yet implemented");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause profiling session: {SessionName}", request.SessionName);
            throw;
        }
    }

    /// <summary>
    /// Returns all saved recent connections sorted by most recent first.
    /// Passwords in the returned DTOs are already decrypted by the repository layer.
    /// </summary>
    [JsonRpcMethod("GetRecentConnectionsAsync", UseSingleObjectParameterDeserialization = true)]
    public async Task<List<RecentConnectionDto>> GetRecentConnectionsAsync(
        GetRecentConnectionsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var connections = await _connectionRepository.GetAllAsync().ConfigureAwait(false);

            return connections
                .OrderByDescending(c => c.CreationDate)
                .Select(c => new RecentConnectionDto
                {
                    Id = c.Id,
                    DataSource = c.DataSource,
                    InitialCatalog = c.InitialCatalog,
                    UserId = c.UserId,
                    Password = c.Password,
                    IntegratedSecurity = c.IntegratedSecurity,
                    EngineType = c.EngineType.HasValue ? (int)c.EngineType.Value : null,
                    AuthenticationMode = (int)c.AuthenticationMode,
                    ConnectionString = c.ConnectionString,
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve recent connections");
            throw;
        }
    }

    /// <summary>
    /// Saves (upserts) a connection. If the same DataSource+UserId+InitialCatalog already
    /// exists the row is updated; otherwise a new row is inserted.
    /// Passwords are encrypted by the repository layer before storage.
    /// </summary>
    [JsonRpcMethod("SaveRecentConnectionAsync", UseSingleObjectParameterDeserialization = true)]
    public async Task SaveRecentConnectionAsync(
        SaveRecentConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        // ── ConnectionString mode — MUST come before DataSource/InitialCatalog guards ──
        if (request.AuthenticationMode.HasValue
            && request.AuthenticationMode.Value == (int)AuthenticationMode.ConnectionString)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request.ConnectionString, nameof(request));

            var builder = new SqlConnectionStringBuilder(request.ConnectionString);
            var connection = new Connection(
                id: 0,
                initialCatalog: builder.InitialCatalog,
                creationDate: DateTime.UtcNow,
                dataSource: builder.DataSource,
                integratedSecurity: builder.IntegratedSecurity,
                password: null,
                userId: string.IsNullOrEmpty(builder.UserID) ? null : builder.UserID,
                engineType: null,
                authenticationMode: AuthenticationMode.ConnectionString,
                connectionString: request.ConnectionString);

            await _connectionRepository.UpsertAsync(connection).ConfigureAwait(false);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Recent connection saved (ConnString mode): DataSource={DataSource}, InitialCatalog={InitialCatalog}",
                    builder.DataSource,
                    builder.InitialCatalog);
            }

            return;
        }

        // ── Standard mode guards (unchanged) ──────────────────────────────────
        if (string.IsNullOrWhiteSpace(request.DataSource))
        {
            throw new ArgumentException("DataSource cannot be null or empty", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.InitialCatalog))
        {
            throw new ArgumentException("InitialCatalog cannot be null or empty", nameof(request));
        }

        try
        {
            var connection = new Connection(
                id: 0,
                initialCatalog: request.InitialCatalog,
                creationDate: DateTime.UtcNow,
                dataSource: request.DataSource,
                integratedSecurity: request.IntegratedSecurity,
                password: request.Password,
                userId: request.UserId,
                engineType: request.EngineType.HasValue
                    ? (DatabaseEngineType?)request.EngineType.Value
                    : null,
                authenticationMode: request.AuthenticationMode.HasValue
                    ? (AuthenticationMode)request.AuthenticationMode.Value
                    : AuthenticationMode.WindowsAuth);

            await _connectionRepository.UpsertAsync(connection).ConfigureAwait(false);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Recent connection saved: DataSource={DataSource}, InitialCatalog={InitialCatalog}",
                    request.DataSource,
                    request.InitialCatalog);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save recent connection: {DataSource}", request.DataSource);
            throw;
        }
    }

    /// <summary>
    /// Deletes a recent connection by its unique identifier.
    /// </summary>
    /// <remarks>
    /// If no row with the given <paramref name="request"/> Id exists the operation
    /// completes silently — SQLite DELETE is a no-op when no rows match.
    /// </remarks>
    [JsonRpcMethod("DeleteRecentConnectionAsync", UseSingleObjectParameterDeserialization = true)]
    public async Task DeleteRecentConnectionAsync(
        DeleteRecentConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await _connectionRepository.Delete(request.Id).ConfigureAwait(false);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Recent connection deleted: Id={Id}", request.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete recent connection: {Id}", request.Id);
            throw;
        }
    }
}
