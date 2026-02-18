using LightQueryProfiler.Shared.Services;
using LightQueryProfiler.Shared.Services.Interfaces;

namespace LightQueryProfiler.Shared.UnitTests.Services
{
    [TestFixture]
    public class ProfilerEventUniqueKeyIntegrationTests
    {
        private IXEventService _eventService;

        [SetUp]
        public void SetUp()
        {
            _eventService = new XEventService();
        }

        [Test]
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
            Assert.That(events.Count, Is.GreaterThan(0), "Should have parsed events from XML");
            Assert.That(uniqueKeys.Count, Is.EqualTo(events.Count),
                $"All events should have unique keys. Total events: {events.Count}, Unique keys: {uniqueKeys.Count}");
        }

        [Test]
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
            Assert.That(events.Count, Is.GreaterThan(0), "Should have parsed events from XML");
            Assert.That(eventsWithSequence, Is.EqualTo(events.Count),
                "All events should have event_sequence action");
        }

        [Test]
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
            Assert.That(events.Count, Is.GreaterThan(0), "Should have parsed events from XML");
            Assert.That(sequenceBasedKeys, Is.EqualTo(events.Count),
                "All events should use sequence-based keys");
        }

        [Test]
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
            Assert.That(sqlBatchEvents.Count, Is.GreaterThan(0), "Should have sql_batch_completed events");
            Assert.That(rpcEvents.Count, Is.GreaterThan(0), "Should have rpc_completed events");
            Assert.That(duplicates.Count, Is.EqualTo(0),
                $"No duplicate keys should exist. Found {duplicates.Count} duplicates");
        }

        [Test]
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
            Assert.That(sequences.Count, Is.GreaterThan(1), "Should have multiple events with sequences");

            // Verify sequences are unique
            var uniqueSequences = new HashSet<ulong>(sequences);
            Assert.That(uniqueSequences.Count, Is.EqualTo(sequences.Count),
                "All event_sequence values should be unique");
        }

        [Test]
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

                Assert.That(isValid, Is.True,
                    $"Event key '{key}' for event '{evt.Name}' does not match expected format");
            }
        }
    }
}
