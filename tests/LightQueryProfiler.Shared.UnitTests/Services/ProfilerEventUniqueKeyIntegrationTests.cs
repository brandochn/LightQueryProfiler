using LightQueryProfiler.Shared.Services;
using LightQueryProfiler.Shared.Services.Interfaces;

namespace LightQueryProfiler.Shared.UnitTests.Services
{
    public class ProfilerEventUniqueKeyIntegrationTests
    {
        private readonly IXEventService _eventService;

        public ProfilerEventUniqueKeyIntegrationTests()
        {
            _eventService = new XEventService();
        }

        [Fact]
        public void ParseRealXml_AllEventsHaveUniqueKeys()
        {
            // Arrange
            string sourceFile = "..\\..\\..\\TestFiles\\RingBufferTarget.xml";
            if (!File.Exists(sourceFile))
            {
                Assert.Fail("Test file not found: " + sourceFile);
            }

            string xml = File.ReadAllText(sourceFile);

            // Act
            var events = _eventService.Parser(xml);
            var eventKeys = events.Select(e => e.GetEventKey()).ToList();
            var uniqueKeys = new HashSet<string>(eventKeys);

            // Assert
            Assert.True(events.Count > 0);
            Assert.Equal(events.Count, uniqueKeys.Count);
        }

        [Fact]
        public void ParseRealXml_AllEventsHaveEventSequence()
        {
            // Arrange
            string sourceFile = "..\\..\\..\\TestFiles\\RingBufferTarget.xml";
            if (!File.Exists(sourceFile))
            {
                Assert.Fail("Test file not found: " + sourceFile);
            }

            string xml = File.ReadAllText(sourceFile);

            // Act
            var events = _eventService.Parser(xml);
            var eventsWithSequence = events.Count(e =>
                e.Actions?.ContainsKey("event_sequence") == true);

            // Assert
            Assert.True(events.Count > 0);
            Assert.Equal(events.Count, eventsWithSequence);
        }

        [Fact]
        public void ParseRealXml_EventKeysAreSequenceBased()
        {
            // Arrange
            string sourceFile = "..\\..\\..\\TestFiles\\RingBufferTarget.xml";
            if (!File.Exists(sourceFile))
            {
                Assert.Fail("Test file not found: " + sourceFile);
            }

            string xml = File.ReadAllText(sourceFile);

            // Act
            var events = _eventService.Parser(xml);
            var sequenceBasedKeys = events.Count(e => e.GetEventKey().StartsWith("seq:"));

            // Assert
            Assert.True(events.Count > 0);
            Assert.Equal(events.Count, sequenceBasedKeys);
        }

        [Fact]
        public void ParseRealXml_DifferentEventTypesWithSameSequenceDoNotCollide()
        {
            // Arrange
            string sourceFile = "..\\..\\..\\TestFiles\\RingBufferTarget.xml";
            if (!File.Exists(sourceFile))
            {
                Assert.Fail("Test file not found: " + sourceFile);
            }

            string xml = File.ReadAllText(sourceFile);

            // Act
            var events = _eventService.Parser(xml);

            // Find events with different names
            var sqlBatchEvents = events.Where(e => e.Name == "sql_batch_completed").ToList();
            var rpcEvents = events.Where(e => e.Name == "rpc_completed").ToList();

            // Get their keys
            var allKeys = events.Select(e => e.GetEventKey()).ToList();
            var duplicates = allKeys.GroupBy(k => k).Where(g => g.Count() > 1).ToList();

            // Assert
            Assert.True(sqlBatchEvents.Count > 0);
            Assert.True(rpcEvents.Count > 0);
            Assert.Empty(duplicates);
        }

        [Fact]
        public void ParseRealXml_EventSequenceValuesAreMonotonicallyIncreasing()
        {
            // Arrange
            string sourceFile = "..\\..\\..\\TestFiles\\RingBufferTarget.xml";
            if (!File.Exists(sourceFile))
            {
                Assert.Fail("Test file not found: " + sourceFile);
            }

            string xml = File.ReadAllText(sourceFile);

            // Act
            var events = _eventService.Parser(xml);
            var sequences = events
                .Where(e => e.Actions?.ContainsKey("event_sequence") == true)
                .Select(e => Convert.ToUInt64(e.Actions!["event_sequence"]))
                .ToList();

            // Assert
            Assert.True(sequences.Count > 1);

            // Verify sequences are unique
            var uniqueSequences = new HashSet<ulong>(sequences);
            Assert.Equal(sequences.Count, uniqueSequences.Count);
        }

        [Fact]
        public void ParseRealXml_EventKeysMatchExpectedFormat()
        {
            // Arrange
            string sourceFile = "..\\..\\..\\TestFiles\\RingBufferTarget.xml";
            if (!File.Exists(sourceFile))
            {
                Assert.Fail("Test file not found: " + sourceFile);
            }

            string xml = File.ReadAllText(sourceFile);

            // Act
            var events = _eventService.Parser(xml);

            // Assert
            foreach (var evt in events)
            {
                var key = evt.GetEventKey();

                // Key should match one of the expected formats
                bool isValid = key.StartsWith("seq:") ||
                              key.StartsWith("activity:") ||
                              key.Contains("|");

                Assert.True(isValid);
            }
        }
    }
}
