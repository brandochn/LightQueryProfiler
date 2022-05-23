using LightQueryProfiler.Shared.Models;
using LightQueryProfiler.Shared.Repositories.Interfaces;

namespace LightQueryProfiler.Shared.Services.Interfaces
{
    public interface IProfilerService
    {
        public void PauseProfiling(string sessionName);

        public void StartProfiling(string sessionName, BaseProfilerSessionTemplate template);

        public void StopProfiling(string sessionName);

        public Task<List<ProfilerEvent>> GetLastEventsAsync(string sessionName);
    }
}