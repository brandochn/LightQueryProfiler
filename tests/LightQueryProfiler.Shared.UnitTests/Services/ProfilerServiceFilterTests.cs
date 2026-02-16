using LightQueryProfiler.Shared.Models;
using LightQueryProfiler.Shared.Repositories.Interfaces;
using LightQueryProfiler.Shared.Services;
using LightQueryProfiler.Shared.Services.Interfaces;
using Moq;

namespace LightQueryProfiler.Shared.UnitTests.Services
{
    [TestFixture]
    public class ProfilerServiceFilterTests
    {
        private Mock<IXEventRepository> _mockRepository;
        private Mock<IXEventService> _mockXEventService;
        private ProfilerService _profilerService;

        [SetUp]
        public void SetUp()
        {
            _mockRepository = new Mock<IXEventRepository>();
            _mockXEventService = new Mock<IXEventService>();
            _profilerService = new ProfilerService(_mockRepository.Object, _mockXEventService.Object);
        }

        [Test]
        public async Task GetLastEventsAsync_FiltersEventsWithLightQueryProfilerInClientAppName()
        {
            // Arrange
            var events = new List<ProfilerEvent>
            {
                new ProfilerEvent
                {
                    Name = "sql_batch_completed",
                    Actions = new Dictionary<string, object?>
                    {
                        { "client_app_name", "LightQueryProfiler" },
                        { "event_sequence", 1ul }
                    }
                },
                new ProfilerEvent
                {
                    Name = "sql_batch_completed",
                    Actions = new Dictionary<string, object?>
                    {
                        { "client_app_name", "SSMS" },
                        { "event_sequence", 2ul }
                    }
                }
            };

            _mockRepository.Setup(r => r.GetXEventsDataAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("<RingBufferTarget></RingBufferTarget>");
            _mockXEventService.Setup(s => s.Parser(It.IsAny<string>()))
                .Returns(events);

            // Act
            var result = await _profilerService.GetLastEventsAsync("TestSession");

            // Assert
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Actions?["client_app_name"], Is.EqualTo("SSMS"));
        }

        [Test]
        public async Task GetLastEventsAsync_FiltersEventsWithLightQueryProfilerInBatchText()
        {
            // Arrange
            var events = new List<ProfilerEvent>
            {
                new ProfilerEvent
                {
                    Name = "sql_batch_completed",
                    Fields = new Dictionary<string, object?>
                    {
                        { "batch_text", "SELECT * FROM sys.dm_xe_sessions WHERE name='LightQueryProfiler'" }
                    },
                    Actions = new Dictionary<string, object?> { { "event_sequence", 1ul } }
                },
                new ProfilerEvent
                {
                    Name = "sql_batch_completed",
                    Fields = new Dictionary<string, object?>
                    {
                        { "batch_text", "SELECT * FROM Users" }
                    },
                    Actions = new Dictionary<string, object?> { { "event_sequence", 2ul } }
                }
            };

            _mockRepository.Setup(r => r.GetXEventsDataAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("<RingBufferTarget></RingBufferTarget>");
            _mockXEventService.Setup(s => s.Parser(It.IsAny<string>()))
                .Returns(events);

            // Act
            var result = await _profilerService.GetLastEventsAsync("TestSession");

            // Assert
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Fields?["batch_text"], Is.EqualTo("SELECT * FROM Users"));
        }

        [Test]
        public async Task GetLastEventsAsync_FiltersEventsWithLightQueryProfilerInStatement()
        {
            // Arrange
            var events = new List<ProfilerEvent>
            {
                new ProfilerEvent
                {
                    Name = "rpc_completed",
                    Fields = new Dictionary<string, object?>
                    {
                        { "statement", "EXEC sp_executesql N'SELECT * FROM LightQueryProfiler_Config'" }
                    },
                    Actions = new Dictionary<string, object?> { { "event_sequence", 1ul } }
                },
                new ProfilerEvent
                {
                    Name = "rpc_completed",
                    Fields = new Dictionary<string, object?>
                    {
                        { "statement", "EXEC sp_GetUsers" }
                    },
                    Actions = new Dictionary<string, object?> { { "event_sequence", 2ul } }
                }
            };

            _mockRepository.Setup(r => r.GetXEventsDataAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("<RingBufferTarget></RingBufferTarget>");
            _mockXEventService.Setup(s => s.Parser(It.IsAny<string>()))
                .Returns(events);

            // Act
            var result = await _profilerService.GetLastEventsAsync("TestSession");

            // Assert
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Fields?["statement"], Is.EqualTo("EXEC sp_GetUsers"));
        }

        [Test]
        public async Task GetLastEventsAsync_IsCaseInsensitive()
        {
            // Arrange
            var events = new List<ProfilerEvent>
            {
                new ProfilerEvent
                {
                    Name = "sql_batch_completed",
                    Actions = new Dictionary<string, object?>
                    {
                        { "client_app_name", "lightqueryprofiler" },  // lowercase
                        { "event_sequence", 1ul }
                    }
                },
                new ProfilerEvent
                {
                    Name = "sql_batch_completed",
                    Actions = new Dictionary<string, object?>
                    {
                        { "client_app_name", "SSMS" },
                        { "event_sequence", 2ul }
                    }
                }
            };

            _mockRepository.Setup(r => r.GetXEventsDataAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("<RingBufferTarget></RingBufferTarget>");
            _mockXEventService.Setup(s => s.Parser(It.IsAny<string>()))
                .Returns(events);

            // Act
            var result = await _profilerService.GetLastEventsAsync("TestSession");

            // Assert
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Actions?["client_app_name"], Is.EqualTo("SSMS"));
        }

        [Test]
        public async Task GetLastEventsAsync_DoesNotFilterEventsWithoutLightQueryProfiler()
        {
            // Arrange
            var events = new List<ProfilerEvent>
            {
                new ProfilerEvent
                {
                    Name = "sql_batch_completed",
                    Fields = new Dictionary<string, object?> { { "batch_text", "SELECT * FROM Users" } },
                    Actions = new Dictionary<string, object?>
                    {
                        { "client_app_name", "SSMS" },
                        { "event_sequence", 1ul }
                    }
                },
                new ProfilerEvent
                {
                    Name = "rpc_completed",
                    Fields = new Dictionary<string, object?> { { "statement", "EXEC sp_GetOrders" } },
                    Actions = new Dictionary<string, object?>
                    {
                        { "client_app_name", "MyApp" },
                        { "event_sequence", 2ul }
                    }
                }
            };

            _mockRepository.Setup(r => r.GetXEventsDataAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("<RingBufferTarget></RingBufferTarget>");
            _mockXEventService.Setup(s => s.Parser(It.IsAny<string>()))
                .Returns(events);

            // Act
            var result = await _profilerService.GetLastEventsAsync("TestSession");

            // Assert
            Assert.That(result.Count, Is.EqualTo(2));
        }

        [Test]
        public async Task GetLastEventsAsync_HandlesNullFieldsAndActions()
        {
            // Arrange
            var events = new List<ProfilerEvent>
            {
                new ProfilerEvent
                {
                    Name = "login",
                    Fields = null,
                    Actions = null
                }
            };

            _mockRepository.Setup(r => r.GetXEventsDataAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync("<RingBufferTarget></RingBufferTarget>");
            _mockXEventService.Setup(s => s.Parser(It.IsAny<string>()))
                .Returns(events);

            // Act
            var result = await _profilerService.GetLastEventsAsync("TestSession");

            // Assert
            Assert.That(result.Count, Is.EqualTo(1));
        }
    }
}
