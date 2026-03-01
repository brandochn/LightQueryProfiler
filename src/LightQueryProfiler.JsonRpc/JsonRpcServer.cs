using LightQueryProfiler.JsonRpc.Models;
using LightQueryProfiler.Shared.Data;
using LightQueryProfiler.Shared.Enums;
using LightQueryProfiler.Shared.Factories;
using LightQueryProfiler.Shared.Repositories;
using LightQueryProfiler.Shared.Services;
using LightQueryProfiler.Shared.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace LightQueryProfiler.JsonRpc;

/// <summary>
/// JSON-RPC server that exposes profiling operations for VS Code extension
/// </summary>
public class JsonRpcServer
{
    private readonly ILogger<JsonRpcServer> _logger;
    private readonly Dictionary<string, IProfilerService> _activeSessions;
    private readonly Dictionary<string, IApplicationDbContext> _activeContexts;

    public JsonRpcServer(ILogger<JsonRpcServer> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _activeSessions = new Dictionary<string, IProfilerService>();
        _activeContexts = new Dictionary<string, IApplicationDbContext>();
    }

    /// <summary>
    /// Starts a profiling session with the specified parameters
    /// </summary>
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

        if (!Enum.IsDefined(typeof(DatabaseEngineType), request.EngineType))
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
            // Create context and services for this session
            var dbContext = new ApplicationDbContext(request.ConnectionString);
            var xEventRepository = new XEventRepository(dbContext);
            xEventRepository.SetEngineType((DatabaseEngineType)request.EngineType);

            var xEventService = new XEventService();
            var profilerService = new ProfilerService(xEventRepository, xEventService);

            // Create template based on engine type
            var template = ProfilerSessionTemplateFactory.CreateTemplate((DatabaseEngineType)request.EngineType);

            // Start profiling
            await Task.Run(() => profilerService.StartProfiling(request.SessionName, template), cancellationToken)
                .ConfigureAwait(false);

            // Store for later use
            _activeSessions[request.SessionName] = profilerService;
            _activeContexts[request.SessionName] = dbContext;

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Profiling session started successfully: {SessionName}", request.SessionName);
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
}
