using LightQueryProfiler.Shared.Data;
using LightQueryProfiler.Shared.Models;
using LightQueryProfiler.Shared.Repositories;
using LightQueryProfiler.Shared.Repositories.Interfaces;
using LightQueryProfiler.Shared.Services;
using LightQueryProfiler.Shared.Services.Interfaces;

namespace LightQueryProfiler.Shared.UnitTests.Services
{
    public class ProfilerServiceUnitTests
    {
        private readonly IApplicationDbContext _applicationDbContext;
        private readonly IXEventRepository _xEventRepository;
        private readonly IXEventService _xEventService;
        private readonly IProfilerService _profilerService;
        private readonly BaseProfilerSessionTemplate _baseProfilerSessionTemplate;
        private const string CONNECTION_STRING = "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;";
        private readonly string sessionName = "sessionNameTest1";

        public ProfilerServiceUnitTests()
        {
            _applicationDbContext = new ApplicationDbContext(CONNECTION_STRING);
            _xEventRepository = new XEventRepository(_applicationDbContext);
            _xEventService = new XEventService();
            _profilerService = new ProfilerService(_xEventRepository, _xEventService);
            _baseProfilerSessionTemplate = new DefaultProfilerSessionTemplate();
        }

        [Fact]
        public void StartProfiling()
        {
            _profilerService.StartProfiling(sessionName, _baseProfilerSessionTemplate);
        }

        [Fact]
        public async Task GetLastEventsAsync()
        {
            List<ProfilerEvent>? events;
            List<ProfilerEvent> totalEvents = new List<ProfilerEvent>();
            for (int i = 0; i < 5; i++)
            {
                events = await _profilerService.GetLastEventsAsync(sessionName);
                totalEvents.AddRange(events);
            }
            Assert.NotNull(totalEvents);
        }


        [Fact]
        public void StopProfiling()
        {
            _profilerService.StopProfiling(sessionName);
        }
    }
}
