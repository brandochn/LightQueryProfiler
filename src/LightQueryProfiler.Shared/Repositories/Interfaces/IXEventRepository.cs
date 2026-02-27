using LightQueryProfiler.Shared.Enums;

namespace LightQueryProfiler.Shared.Repositories.Interfaces
{
    public interface IXEventRepository
    {
        public void CreateXEventSession(string sessionName, BaseProfilerSessionTemplate template);

        public void DeleteXEventSession(string sessionName);

        public void StartProfiling(string sessionName);

        public void StopProfiling(string sessionName);

        public void PauseProfiling(string sessionName);

        public void DisconnectSession(string sessionName);

        public Task<string> GetXEventsDataAsync(string sessionName, string targetName);

        public void SetEngineType(DatabaseEngineType engineType);
    }
}
