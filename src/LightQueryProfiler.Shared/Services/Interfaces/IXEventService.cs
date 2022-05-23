using LightQueryProfiler.Shared.Models;

namespace LightQueryProfiler.Shared.Services.Interfaces
{
    public interface IXEventService
    {
        public List<ProfilerEvent> Parser(string xml);
    }
}