using LightQueryProfiler.Shared.Models;
using LightQueryProfiler.Shared.Repositories.Interfaces;
using LightQueryProfiler.Shared.Services.Interfaces;

namespace LightQueryProfiler.Shared.Services
{
    public class ProfilerService : IProfilerService
    {
        private const string TargetName = "ring_buffer";
        private readonly IXEventRepository _xEventRepository;
        private readonly IXEventService _xEventService;

        public ProfilerService(IXEventRepository xEventRepository, IXEventService xEventService)
        {
            _xEventRepository = xEventRepository;
            _xEventService = xEventService;
        }

        public async Task<List<ProfilerEvent>> GetLastEventsAsync(string sessionName)
        {
            List<ProfilerEvent> readEvents = await ReadXEventsAsync(sessionName);
            List<ProfilerEvent> newEvents = new List<ProfilerEvent>();

            foreach (ProfilerEvent readEvent in readEvents)
            {
                // ignore events created by itself
                if (!readEvent.GetEventKey().Contains("LightQueryProfiler", StringComparison.OrdinalIgnoreCase))
                {
                    newEvents.Add(readEvent);
                }
            }

            return newEvents;
        }

        public void PauseProfiling(string sessionName)
        {
            throw new NotImplementedException();
        }

        public void StartProfiling(string sessionName, BaseProfilerSessionTemplate template)
        {
            _xEventRepository.CreateXEventSession(sessionName, template);
            _xEventRepository.StartProfiling(sessionName);
        }

        public void StopProfiling(string sessionName)
        {
            _xEventRepository.DisconnectSession(sessionName);
        }

        private async Task<List<ProfilerEvent>> ReadXEventsAsync(string sessionName)
        {
            if (string.IsNullOrEmpty(sessionName))
            {
                throw new Exception("sessionName cannot be null or empty");
            }

            string? xml = await _xEventRepository.GetXEventsDataAsync(sessionName, TargetName);

            if (string.IsNullOrEmpty(xml))
            {
                return new List<ProfilerEvent>();
            }

            var events = _xEventService.Parser(xml);

            return events;
        }
    }
}