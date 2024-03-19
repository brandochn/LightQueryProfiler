using LightQueryProfiler.Shared.Services;
using LightQueryProfiler.Shared.Services.Interfaces;
using NUnit.Framework.Internal;

namespace LightQueryProfiler.Shared.UnitTests.Services
{
    [TestFixture]
    public class XEventServiceUnitTests
    {
        private IXEventService _eventService;

        [SetUp]
        public void SetUp()
        {
            _eventService = new XEventService();
        }

        [Test]
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