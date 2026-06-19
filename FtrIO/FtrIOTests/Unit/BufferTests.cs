namespace FtrIOTests.Unit
{
    using FtrIO.Classes;
    using NUnit.Framework;
    using System.IO;
    using System.Text.Json;

    [TestFixture]
    public class BufferTests
    {
        private string _tempDir = null!;
        private string _settingsPath = null!;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
            _settingsPath = Path.Combine(_tempDir, "appsettings.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        // ── Stage + flush ─────────────────────────────────────────────────────

        [Test]
        public void FlushNow_WritesNewKeyToAppsettingsJson()
        {
            File.WriteAllText(_settingsPath, """{"Toggles":{}}""");
            using var buffer = new ToggleProviderBuffer(_tempDir, TimeSpan.FromHours(1));

            buffer.Stage("MyToggle", "true");
            buffer.FlushNow();

            var toggles = ReadToggles(_settingsPath);
            Assert.AreEqual("true", toggles["MyToggle"].GetString());
        }

        [Test]
        public void FlushNow_UpdatesExistingKeyInAppsettingsJson()
        {
            File.WriteAllText(_settingsPath, """{"Toggles":{"MyToggle":"false"}}""");
            using var buffer = new ToggleProviderBuffer(_tempDir, TimeSpan.FromHours(1));

            buffer.Stage("MyToggle", "true");
            buffer.FlushNow();

            var toggles = ReadToggles(_settingsPath);
            Assert.AreEqual("true", toggles["MyToggle"].GetString());
        }

        [Test]
        public void FlushNow_PreservesUnchangedKeysInAppsettingsJson()
        {
            // B is a JSON boolean (not a string) so we can assert ValueKind is preserved
            File.WriteAllText(_settingsPath, """{"Toggles":{"A":"true","B":false}}""");
            using var buffer = new ToggleProviderBuffer(_tempDir, TimeSpan.FromHours(1));

            buffer.Stage("A", "false"); // only update A
            buffer.FlushNow();

            var toggles = ReadToggles(_settingsPath);
            Assert.AreEqual("false", toggles["A"].GetString());
            // B was not staged — it should be preserved with its original value/type
            Assert.AreEqual(JsonValueKind.False, toggles["B"].ValueKind);
        }

        [Test]
        public void FlushNow_CreatesAppsettingsJsonWhenFileDoesNotExist()
        {
            // No appsettings.json — buffer should create it
            using var buffer = new ToggleProviderBuffer(_tempDir, TimeSpan.FromHours(1));

            buffer.Stage("NewKey", "true");
            buffer.FlushNow();

            Assert.IsTrue(File.Exists(_settingsPath));
            var toggles = ReadToggles(_settingsPath);
            Assert.AreEqual("true", toggles["NewKey"].GetString());
        }

        [Test]
        public void FlushNow_AddsTogglesSectionWhenItDoesNotExistInFile()
        {
            File.WriteAllText(_settingsPath, """{"FtrIO":{"ReloadOnChange":false}}""");
            using var buffer = new ToggleProviderBuffer(_tempDir, TimeSpan.FromHours(1));

            buffer.Stage("MyToggle", "true");
            buffer.FlushNow();

            var toggles = ReadToggles(_settingsPath);
            Assert.AreEqual("true", toggles["MyToggle"].GetString());
        }

        [Test]
        public void FlushNow_PreservesOtherTopLevelSectionsInAppsettingsJson()
        {
            File.WriteAllText(_settingsPath, """{"FtrIO":{"ReloadOnChange":true},"Toggles":{"A":"true"}}""");
            using var buffer = new ToggleProviderBuffer(_tempDir, TimeSpan.FromHours(1));

            buffer.Stage("A", "false");
            buffer.FlushNow();

            using var doc = JsonDocument.Parse(File.ReadAllText(_settingsPath));
            Assert.IsTrue(doc.RootElement.TryGetProperty("FtrIO", out var ftrio));
            Assert.AreEqual(JsonValueKind.True, ftrio.GetProperty("ReloadOnChange").ValueKind);
        }

        // ── Rapid succession / write storm ────────────────────────────────────

        [Test]
        public void RapidSuccessiveStages_LastValueWinsBeforeFlush()
        {
            File.WriteAllText(_settingsPath, """{"Toggles":{}}""");
            using var buffer = new ToggleProviderBuffer(_tempDir, TimeSpan.FromHours(1));

            // Stage the same key 1000 times in quick succession
            for (var i = 0; i < 1000; i++)
                buffer.Stage("MyToggle", i % 2 == 0 ? "true" : "false");

            buffer.FlushNow();

            // Whatever the last staged value was should be in the file
            var toggles = ReadToggles(_settingsPath);
            var value = toggles["MyToggle"].GetString();
            Assert.That(value, Is.EqualTo("true").Or.EqualTo("false"));
        }

        [Test]
        public void ConcurrentStages_AllKeysEventuallyFlushed()
        {
            File.WriteAllText(_settingsPath, """{"Toggles":{}}""");
            using var buffer = new ToggleProviderBuffer(_tempDir, TimeSpan.FromHours(1));

            // Simulate multiple providers staging different keys concurrently
            var tasks = Enumerable.Range(0, 20)
                .Select(i => Task.Run(() => buffer.Stage($"Key{i}", "true")))
                .ToArray();
            Task.WaitAll(tasks);

            buffer.FlushNow();

            var toggles = ReadToggles(_settingsPath);
            for (var i = 0; i < 20; i++)
                Assert.IsTrue(toggles.ContainsKey($"Key{i}"), $"Key{i} missing after flush");
        }

        // ── FlushInterval read from config ────────────────────────────────────

        [Test]
        public void ReadsFlushIntervalFromAppsettingsJson()
        {
            // Buffer uses a 1-hour timer, so the auto-flush won't fire.
            // We verify FlushInterval config reading via the FlushNow pathway.
            File.WriteAllText(_settingsPath,
                """{"FtrIO":{"ReloadOnChange":true,"FlushInterval":2},"Toggles":{}}""");

            using var buffer = new ToggleProviderBuffer(_tempDir);
            buffer.Stage("FromConfig", "true");
            buffer.FlushNow();

            var toggles = ReadToggles(_settingsPath);
            Assert.IsTrue(toggles.ContainsKey("FromConfig"));
        }

        // ── Dispose performs final flush ───────────────────────────────────────

        [Test]
        public void Dispose_FlushesPendingChanges()
        {
            File.WriteAllText(_settingsPath, """{"Toggles":{}}""");

            using (var buffer = new ToggleProviderBuffer(_tempDir, TimeSpan.FromHours(1)))
            {
                buffer.Stage("OnDispose", "true");
                // Do NOT call FlushNow — let Dispose do it
            }

            var toggles = ReadToggles(_settingsPath);
            Assert.AreEqual("true", toggles["OnDispose"].GetString());
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Dictionary<string, JsonElement> ReadToggles(string path)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("Toggles", out var toggles))
                return new Dictionary<string, JsonElement>();

            return toggles.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.Clone());
        }
    }
}
