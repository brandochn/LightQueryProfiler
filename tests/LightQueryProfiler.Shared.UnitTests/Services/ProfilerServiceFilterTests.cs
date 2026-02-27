using LightQueryProfiler.Shared.Models;
using LightQueryProfiler.Shared.Repositories.Interfaces;
using LightQueryProfiler.Shared.Services;
using LightQueryProfiler.Shared.Services.Interfaces;
using Moq;

namespace LightQueryProfiler.Shared.UnitTests.Services
{
    public class ProfilerServiceFilterTests
    {
        private readonly Mock<IXEventRepository> _mockRepository;
        private readonly Mock<IXEventService> _mockXEventService;
        private readonly ProfilerService _profilerService;

        public ProfilerServiceFilterTests()
        {
            _mockRepository = new Mock<IXEventRepository>();
            _mockXEventService = new Mock<IXEventService>();
            _profilerService = new ProfilerService(_mockRepository.Object, _mockXEventService.Object);
        }

        [Fact]
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
            Assert.Single(result);
            Assert.Equal("SSMS", result[0].Actions?["client_app_name"]);
        }

        [Fact]
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
            Assert.Single(result);
            Assert.Equal("SELECT * FROM Users", result[0].Fields?["batch_text"]);
        }

        [Fact]
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
            Assert.Single(result);
            Assert.Equal("EXEC sp_GetUsers", result[0].Fields?["statement"]);
        }

        [Fact]
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
            Assert.Single(result);
            Assert.Equal("SSMS", result[0].Actions?["client_app_name"]);
        }

        [Fact]
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
            Assert.Equal(2, result.Count);
        }

        [Fact]
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
            Assert.Single(result);
        }
    }
}
