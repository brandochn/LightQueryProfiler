using LightQueryProfiler.Shared.Models;

namespace LightQueryProfiler.Shared.UnitTests.Models
{
    public class ProfilerEventTests
    {
        [Fact]
        public void GetEventKey_WhenEventSequenceExists_ReturnsSequenceBasedKey()
        {
            // Arrange
            var profilerEvent = new ProfilerEvent
            {
                Name = "sql_batch_completed",
                Timestamp = "2022-05-20T14:33:57.768Z",
                Actions = new Dictionary<string, object?>
                {
                    { "event_sequence", 6ul },
                    { "session_id", (ushort)52 }
                }
            };

            // Act
            var key = profilerEvent.GetEventKey();

            // Assert
            Assert.Equal("seq:6", key);
        }

        [Fact]
        public void GetEventKey_WhenOnlyActivityIdExists_ReturnsActivityBasedKey()
        {
            // Arrange
            var profilerEvent = new ProfilerEvent
            {
                Name = "sql_batch_completed",
                Timestamp = "2022-05-20T14:33:57.768Z",
                Actions = new Dictionary<string, object?>
                {
                    { "attach_activity_id", "4E3CD74C-9F1E-4C82-8CBD-EFA7E66E4607-6" },
                    { "session_id", (ushort)52 }
                }
            };

            // Act
            var key = profilerEvent.GetEventKey();

            // Assert
            Assert.Equal("activity:4E3CD74C-9F1E-4C82-8CBD-EFA7E66E4607-6", key);
        }

        [Fact]
        public void GetEventKey_WhenNoUniqueIdentifiers_ReturnsFallbackKey()
        {
            // Arrange
            var profilerEvent = new ProfilerEvent
            {
                Name = "existing_connection",
                Timestamp = "2022-05-20T14:33:57.766Z",
                Actions = new Dictionary<string, object?>
                {
                    { "session_id", (ushort)51 }
                }
            };

            // Act
            var key = profilerEvent.GetEventKey();

            // Assert
            Assert.Equal("2022-05-20T14:33:57.766Z|existing_connection|51", key);
        }

        [Fact]
        public void GetEventKey_WhenActionsIsNull_ReturnsFallbackKeyWithEmptySessionId()
        {
            // Arrange
            var profilerEvent = new ProfilerEvent
            {
                Name = "test_event",
                Timestamp = "2022-05-20T14:33:57.766Z",
                Actions = null
            };

            // Act
            var key = profilerEvent.GetEventKey();

            // Assert
            Assert.Equal("2022-05-20T14:33:57.766Z|test_event|", key);
        }

        [Fact]
        public void GetEventKey_DifferentEventSequences_ProduceDifferentKeys()
        {
            // Arrange
            var event1 = new ProfilerEvent
            {
                Name = "sql_batch_completed",
                Timestamp = "2022-05-20T14:33:57.768Z",
                Actions = new Dictionary<string, object?>
                {
                    { "event_sequence", 1ul }
                }
            };

            var event2 = new ProfilerEvent
            {
                Name = "sql_batch_completed",
                Timestamp = "2022-05-20T14:33:57.768Z",
                Actions = new Dictionary<string, object?>
                {
                    { "event_sequence", 2ul }
                }
            };

            // Act
            var key1 = event1.GetEventKey();
            var key2 = event2.GetEventKey();

            // Assert
            Assert.NotEqual(key1, key2);
            Assert.Equal("seq:1", key1);
            Assert.Equal("seq:2", key2);
        }

        [Fact]
        public void GetEventKey_SameEventSequence_ProducesSameKey()
        {
            // Arrange
            var event1 = new ProfilerEvent
            {
                Name = "sql_batch_completed",
                Timestamp = "2022-05-20T14:33:57.768Z",
                Fields = new Dictionary<string, object?>
                {
                    { "cpu_time", 1000ul }
                },
                Actions = new Dictionary<string, object?>
                {
                    { "event_sequence", 5ul },
                    { "session_id", (ushort)52 }
                }
            };

            var event2 = new ProfilerEvent
            {
                Name = "rpc_completed",
                Timestamp = "2022-05-20T14:33:57.770Z",
                Fields = new Dictionary<string, object?>
                {
                    { "cpu_time", 2000ul }
                },
                Actions = new Dictionary<string, object?>
                {
                    { "event_sequence", 5ul },
                    { "session_id", (ushort)53 }
                }
            };

            // Act
            var key1 = event1.GetEventKey();
            var key2 = event2.GetEventKey();

            // Assert
            Assert.Equal(key1, key2);
            Assert.Equal("seq:5", key1);
        }

        [Fact]
        public void GetEventKey_PrioritizesEventSequenceOverActivityId()
        {
            // Arrange
            var profilerEvent = new ProfilerEvent
            {
                Name = "sql_batch_completed",
                Timestamp = "2022-05-20T14:33:57.768Z",
                Actions = new Dictionary<string, object?>
                {
                    { "event_sequence", 10ul },
                    { "attach_activity_id", "4E3CD74C-9F1E-4C82-8CBD-EFA7E66E4607-6" }
                }
            };

            // Act
            var key = profilerEvent.GetEventKey();

            // Assert
            Assert.Equal("seq:10", key);
        }
    }
}
