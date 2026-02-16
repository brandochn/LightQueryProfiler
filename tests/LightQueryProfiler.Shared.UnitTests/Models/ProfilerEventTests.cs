using LightQueryProfiler.Shared.Models;

namespace LightQueryProfiler.Shared.UnitTests.Models
{
    [TestFixture]
    public class ProfilerEventTests
    {
        [Test]
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
            Assert.That(key, Is.EqualTo("seq:6"));
        }

        [Test]
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
            Assert.That(key, Is.EqualTo("activity:4E3CD74C-9F1E-4C82-8CBD-EFA7E66E4607-6"));
        }

        [Test]
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
            Assert.That(key, Is.EqualTo("2022-05-20T14:33:57.766Z|existing_connection|51"));
        }

        [Test]
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
            Assert.That(key, Is.EqualTo("2022-05-20T14:33:57.766Z|test_event|"));
        }

        [Test]
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
            Assert.That(key1, Is.Not.EqualTo(key2));
            Assert.That(key1, Is.EqualTo("seq:1"));
            Assert.That(key2, Is.EqualTo("seq:2"));
        }

        [Test]
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
            Assert.That(key1, Is.EqualTo(key2));
            Assert.That(key1, Is.EqualTo("seq:5"));
        }

        [Test]
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
            Assert.That(key, Is.EqualTo("seq:10"));
        }
    }
}
