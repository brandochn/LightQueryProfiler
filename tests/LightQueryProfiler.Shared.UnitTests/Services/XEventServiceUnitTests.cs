using LightQueryProfiler.Shared.Services;
using LightQueryProfiler.Shared.Services.Interfaces;

namespace LightQueryProfiler.Shared.UnitTests.Services
{
    public class XEventServiceUnitTests
    {
        private readonly IXEventService _eventService;

        public XEventServiceUnitTests()
        {
            _eventService = new XEventService();
        }

        [Fact]
        public void Parse()
        {
            string sourceFile = "..\\..\\..\\TestFiles\\RingBufferTarget.xml";
            if (File.Exists(sourceFile) == false)
            {
                throw new Exception("File not found");
            }

            string xml = File.ReadAllText(sourceFile);

            var events = _eventService.Parser(xml);
            //if (events != null)
            //{
            //    foreach (var iter in events)
            //    {
            //        Console.WriteLine(iter.ToString());
            //    }
            //}

            //Assert.IsNotNull(events);
        }
    }
}
