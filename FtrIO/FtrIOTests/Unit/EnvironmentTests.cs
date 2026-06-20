namespace FtrIOTests.Unit
{
    using FtrIO.Classes;
    using NUnit.Framework;
    using System.IO;
    using System.Text.Json;

    /// <summary>
    /// Verifies multi-environment support across ToggleParser, StrategyToggleParser,
    /// and ToggleProviderBuffer. The active environment is resolved from:
    ///   1. FtrIO:Environment in appsettings.json
    ///   2. ASPNETCORE_ENVIRONMENT env var
    ///   3. DOTNET_ENVIRONMENT env var
    /// </summary>
    [TestFixture]
    public class EnvironmentTests
    {
        private string _tempDir = null!;
        private string _baseSettings = null!;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
            _baseSettings = Path.Combine(_tempDir, "appsettings.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);

            // Clean up any env var set during a test
            System.Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
            System.Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", null);
        }

        // ── ToggleParser ──────────────────────────────────────────────────────

        [Test]
        public void ToggleParser_ReturnsBaseValue_WhenNoEnvironmentSet()
        {
            File.WriteAllText(_baseSettings,
                """{"Toggles":{"MyToggle":"true"}}""");

            var parser = new ToggleParser(_tempDir);
            Assert.IsTrue(parser.GetToggleStatus("MyToggle"));
        }

        [Test]
        public void ToggleParser_EnvFileOverridesBase_WhenFtrIOEnvironmentSet()
        {
            File.WriteAllText(_baseSettings,
                """{"FtrIO":{"Environment":"Staging"},"Toggles":{"MyToggle":"true"}}""");
            File.WriteAllText(Path.Combine(_tempDir, "appsettings.Staging.json"),
                """{"Toggles":{"MyToggle":"false"}}""");

            var parser = new ToggleParser(_tempDir);
            Assert.IsFalse(parser.GetToggleStatus("MyToggle"));
        }

        [Test]
        public void ToggleParser_FallsBackToBase_WhenKeyMissingFromEnvFile()
        {
            File.WriteAllText(_baseSettings,
                """{"FtrIO":{"Environment":"Staging"},"Toggles":{"Base":"true","Override":"false"}}""");
            File.WriteAllText(Path.Combine(_tempDir, "appsettings.Staging.json"),
                """{"Toggles":{"Override":"true"}}""");

            var parser = new ToggleParser(_tempDir);
            Assert.IsTrue(parser.GetToggleStatus("Base"));    // from base file
            Assert.IsTrue(parser.GetToggleStatus("Override")); // overridden by staging
        }

        [Test]
        public void ToggleParser_ReadsEnvironmentFromAspNetCoreEnvVar()
        {
            File.WriteAllText(_baseSettings,
                """{"Toggles":{"MyToggle":"true"}}""");
            File.WriteAllText(Path.Combine(_tempDir, "appsettings.Production.json"),
                """{"Toggles":{"MyToggle":"false"}}""");

            System.Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");

            var parser = new ToggleParser(_tempDir);
            Assert.IsFalse(parser.GetToggleStatus("MyToggle"));
        }

        [Test]
        public void ToggleParser_ReadsEnvironmentFromDotnetEnvVar()
        {
            File.WriteAllText(_baseSettings,
                """{"Toggles":{"MyToggle":"true"}}""");
            File.WriteAllText(Path.Combine(_tempDir, "appsettings.Development.json"),
                """{"Toggles":{"MyToggle":"false"}}""");

            System.Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");

            var parser = new ToggleParser(_tempDir);
            Assert.IsFalse(parser.GetToggleStatus("MyToggle"));
        }

        [Test]
        public void ToggleParser_FtrIOEnvironmentTakesPrecedenceOverEnvVar()
        {
            // FtrIO:Environment = Staging, but ASPNETCORE_ENVIRONMENT = Production
            File.WriteAllText(_baseSettings,
                """{"FtrIO":{"Environment":"Staging"},"Toggles":{"MyToggle":"true"}}""");
            File.WriteAllText(Path.Combine(_tempDir, "appsettings.Staging.json"),
                """{"Toggles":{"MyToggle":"false"}}""");
            File.WriteAllText(Path.Combine(_tempDir, "appsettings.Production.json"),
                """{"Toggles":{"MyToggle":"true"}}""");

            System.Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");

            var parser = new ToggleParser(_tempDir);
            Assert.IsFalse(parser.GetToggleStatus("MyToggle")); // Staging wins
        }

        // ── ToggleProviderBuffer ──────────────────────────────────────────────

        [Test]
        public void Buffer_FlushesToBaseFile_WhenNoEnvironmentSet()
        {
            File.WriteAllText(_baseSettings, """{"Toggles":{}}""");

            using var buffer = new ToggleProviderBuffer(_tempDir, TimeSpan.FromHours(1));
            buffer.Stage("MyToggle", "true");
            buffer.FlushNow();

            Assert.IsTrue(File.Exists(_baseSettings));
            var toggles = ReadToggles(_baseSettings);
            Assert.AreEqual("true", toggles["MyToggle"].GetString());
        }

        [Test]
        public void Buffer_FlushesToEnvFile_WhenFtrIOEnvironmentSet()
        {
            File.WriteAllText(_baseSettings,
                """{"FtrIO":{"Environment":"Staging"},"Toggles":{}}""");

            var stagingPath = Path.Combine(_tempDir, "appsettings.Staging.json");

            using var buffer = new ToggleProviderBuffer(_tempDir, TimeSpan.FromHours(1));
            buffer.Stage("MyToggle", "true");
            buffer.FlushNow();

            Assert.IsTrue(File.Exists(stagingPath), "Expected appsettings.Staging.json to be created");
            Assert.IsFalse(HasToggle(_baseSettings, "MyToggle"), "Base file should not be modified");
            var toggles = ReadToggles(stagingPath);
            Assert.AreEqual("true", toggles["MyToggle"].GetString());
        }

        [Test]
        public void Buffer_FlushesToBaseFile_EvenWhenAspNetCoreEnvVarSet()
        {
            // ASPNETCORE_ENVIRONMENT is commonly set on production servers for unrelated
            // reasons. The buffer must still write to appsettings.json — the server's own
            // file is its environment. Env vars only affect the read path (ToggleParser).
            File.WriteAllText(_baseSettings, """{"Toggles":{}}""");
            System.Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");

            var prodPath = Path.Combine(_tempDir, "appsettings.Production.json");

            using var buffer = new ToggleProviderBuffer(_tempDir, TimeSpan.FromHours(1));
            buffer.Stage("MyToggle", "true");
            buffer.FlushNow();

            Assert.IsFalse(File.Exists(prodPath), "Buffer must not create an env-specific file based on env vars alone");
            var toggles = ReadToggles(_baseSettings);
            Assert.AreEqual("true", toggles["MyToggle"].GetString());
        }

        [Test]
        public void Buffer_EnvFile_PreservesExistingKeys()
        {
            File.WriteAllText(_baseSettings,
                """{"FtrIO":{"Environment":"Staging"}}""");
            var stagingPath = Path.Combine(_tempDir, "appsettings.Staging.json");
            File.WriteAllText(stagingPath, """{"Toggles":{"Existing":"false"}}""");

            using var buffer = new ToggleProviderBuffer(_tempDir, TimeSpan.FromHours(1));
            buffer.Stage("New", "true");
            buffer.FlushNow();

            var toggles = ReadToggles(stagingPath);
            Assert.IsTrue(toggles.ContainsKey("Existing"), "Existing key should be preserved");
            Assert.AreEqual("true", toggles["New"].GetString());
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

        private static bool HasToggle(string path, string key)
        {
            if (!File.Exists(path)) return false;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.TryGetProperty("Toggles", out var toggles)
                && toggles.TryGetProperty(key, out _);
        }
    }
}
