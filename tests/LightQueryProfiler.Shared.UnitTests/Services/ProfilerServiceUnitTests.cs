using LightQueryProfiler.Shared.Data;
using LightQueryProfiler.Shared.Models;
using LightQueryProfiler.Shared.Repositories;
using LightQueryProfiler.Shared.Repositories.Interfaces;
using LightQueryProfiler.Shared.Services;
using LightQueryProfiler.Shared.Services.Interfaces;

namespace LightQueryProfiler.Shared.UnitTests.Services
{
    [TestFixture]
    internal class ProfilerServiceUnitTests
    {
        private IApplicationDbContext _applicationDbContext;
        private IXEventRepository _xEventRepository;
        private IXEventService _xEventService;
        private IProfilerService _profilerService;
        private BaseProfilerSessionTemplate _baseProfilerSessionTemplate;
        private const string CONNECTION_STRING = "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;";
        private string sessionName = "sessionNameTest1";

        [SetUp]
        public void SetUp()
        {
            _applicationDbContext = new ApplicationDbContext(CONNECTION_STRING);
            _xEventRepository = new XEventRepository(_applicationDbContext);
            _xEventService = new XEventService();
            _profilerService = new ProfilerService(_xEventRepository, _xEventService);
            _baseProfilerSessionTemplate = new DefaultProfilerSessionTemplate();
        }

        [Test, Order(1)]
        public void StartProfiling()
        {
            _profilerService.StartProfiling(sessionName, _baseProfilerSessionTemplate);
        }

        [Test, Order(2)]
        public async Task GetLastEventsAsync()
        {
            List<ProfilerEvent>? events;
            List<ProfilerEvent> totalEvents = new List<ProfilerEvent>();
            for (int i = 0; i < 5; i++)
            {
                events = await _profilerService.GetLastEventsAsync(sessionName);
                totalEvents.AddRange(events);
            }
            Assert.That(totalEvents, Is.Not.Null);
        }


        [Test, Order(5)]
        public void StopProfiling()
        {
            _profilerService.StopProfiling(sessionName);
        }
    }
}