namespace FtrIOTests.Unit
{
    using FtrIO.Classes;
    using FtrIO.Interfaces;
    using FtrIO.Providers.Http;
    using FtrIO.Strategies;
    using NUnit.Framework;
    using System.Net;
    using System.Net.Http;
    using ToggleExceptions;

    [TestFixture]
    public class ProviderTests
    {
        // ── EnvironmentVariableToggleParser (standalone mode) ─────────────────

        [Test]
        public void EnvVar_Standalone_ReturnsTrueWhenEnvVarIsTrue()
        {
            Environment.SetEnvironmentVariable("FTRIO__Toggles__EnvTrue", "true");
            try
            {
                Assert.IsTrue(new EnvironmentVariableToggleParser().GetToggleStatus("EnvTrue"));
            }
            finally { Environment.SetEnvironmentVariable("FTRIO__Toggles__EnvTrue", null); }
        }

        [Test]
        public void EnvVar_Standalone_ReturnsFalseWhenEnvVarIsFalse()
        {
            Environment.SetEnvironmentVariable("FTRIO__Toggles__EnvFalse", "false");
            try
            {
                Assert.IsFalse(new EnvironmentVariableToggleParser().GetToggleStatus("EnvFalse"));
            }
            finally { Environment.SetEnvironmentVariable("FTRIO__Toggles__EnvFalse", null); }
        }

        [Test]
        public void EnvVar_Standalone_ThrowsToggleDoesNotExistExceptionWhenKeyMissing()
        {
            Assert.Throws<ToggleDoesNotExistException>(
                () => new EnvironmentVariableToggleParser().GetToggleStatus("xyzzy_missing_key"));
        }

        [Test]
        public void EnvVar_Standalone_ThrowsToggleParsedOutOfRangeExceptionForInvalidValue()
        {
            Environment.SetEnvironmentVariable("FTRIO__Toggles__EnvInvalid", "ASDF");
            try
            {
                Assert.Throws<ToggleParsedOutOfRangeException>(
                    () => new EnvironmentVariableToggleParser().GetToggleStatus("EnvInvalid"));
            }
            finally { Environment.SetEnvironmentVariable("FTRIO__Toggles__EnvInvalid", null); }
        }

        [Test]
        public void EnvVar_Standalone_SupportsCustomPrefix()
        {
            Environment.SetEnvironmentVariable("MYAPP_SendEmail", "true");
            try
            {
                Assert.IsTrue(new EnvironmentVariableToggleParser("MYAPP_").GetToggleStatus("SendEmail"));
            }
            finally { Environment.SetEnvironmentVariable("MYAPP_SendEmail", null); }
        }

        // ── EnvironmentVariableToggleParser (buffer mode) ─────────────────────

        [Test]
        public void EnvVar_BufferMode_StagesAllMatchingEnvVarsToBuffer()
        {
            Environment.SetEnvironmentVariable("FTRIO__Toggles__BufA", "true");
            Environment.SetEnvironmentVariable("FTRIO__Toggles__BufB", "false");
            try
            {
                var spy = new SpyToggleBuffer();
                using var _ = new EnvironmentVariableToggleParser(spy);

                Assert.AreEqual("true", spy.Staged["BufA"]);
                Assert.AreEqual("false", spy.Staged["BufB"]);
            }
            finally
            {
                Environment.SetEnvironmentVariable("FTRIO__Toggles__BufA", null);
                Environment.SetEnvironmentVariable("FTRIO__Toggles__BufB", null);
            }
        }

        [Test]
        public void EnvVar_BufferMode_DoesNotStageUnrelatedEnvVars()
        {
            Environment.SetEnvironmentVariable("FTRIO__Toggles__Relevant", "true");
            Environment.SetEnvironmentVariable("OTHER_PREFIX_Key", "true");
            try
            {
                var spy = new SpyToggleBuffer();
                using var _ = new EnvironmentVariableToggleParser(spy);

                Assert.IsTrue(spy.Staged.ContainsKey("Relevant"));
                Assert.IsFalse(spy.Staged.ContainsKey("OTHER_PREFIX_Key"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("FTRIO__Toggles__Relevant", null);
                Environment.SetEnvironmentVariable("OTHER_PREFIX_Key", null);
            }
        }

        // ── CompositeToggleParser ─────────────────────────────────────────────

        [Test]
        public void Composite_UsesFirstParserThatHasTheKey()
        {
            var first = new StubToggleParser(new Dictionary<string, bool> { ["KeyA"] = true });
            var second = new StubToggleParser(new Dictionary<string, bool> { ["KeyA"] = false });

            Assert.IsTrue(new CompositeToggleParser(first, second).GetToggleStatus("KeyA"));
        }

        [Test]
        public void Composite_FallsThroughToNextParserWhenKeyMissing()
        {
            var first = new StubToggleParser(new Dictionary<string, bool>());
            var second = new StubToggleParser(new Dictionary<string, bool> { ["KeyB"] = true });

            Assert.IsTrue(new CompositeToggleParser(first, second).GetToggleStatus("KeyB"));
        }

        [Test]
        public void Composite_ThrowsToggleDoesNotExistExceptionWhenAllParsersMissKey()
        {
            var composite = new CompositeToggleParser(
                new StubToggleParser(new Dictionary<string, bool>()),
                new StubToggleParser(new Dictionary<string, bool>()));

            Assert.Throws<ToggleDoesNotExistException>(() => composite.GetToggleStatus("Missing"));
        }

        [Test]
        public void Composite_ThrowsArgumentExceptionWhenNoParsersProvided()
            => Assert.Throws<ArgumentException>(() => new CompositeToggleParser());

        // ── HttpToggleParser — stages to buffer ───────────────────────────────

        [Test]
        public void Http_StagesTrueValueToBuffer()
        {
            var spy = new SpyToggleBuffer();
            using var client = MakeHttpClient("""{"Toggles":{"SendWelcomeEmail":"true"}}""");
            using var _ = new HttpToggleParser("http://fake/toggles", spy, client: client);

            WaitForStage(spy, "SendWelcomeEmail");
            Assert.AreEqual("true", spy.Staged["SendWelcomeEmail"]);
        }

        [Test]
        public void Http_StagesFalseValueToBuffer()
        {
            var spy = new SpyToggleBuffer();
            using var client = MakeHttpClient("""{"Toggles":{"NewCheckout":"false"}}""");
            using var _ = new HttpToggleParser("http://fake/toggles", spy, client: client);

            WaitForStage(spy, "NewCheckout");
            Assert.AreEqual("false", spy.Staged["NewCheckout"]);
        }

        [Test]
        public void Http_StagesRawPercentageValueToBuffer()
        {
            var spy = new SpyToggleBuffer();
            using var client = MakeHttpClient("""{"Toggles":{"Rollout":"50%"}}""");
            using var _ = new HttpToggleParser("http://fake/toggles", spy, client: client);

            WaitForStage(spy, "Rollout");
            Assert.AreEqual("50%", spy.Staged["Rollout"]);
        }

        [Test]
        public void Http_StagesMultipleTogglesToBuffer()
        {
            var spy = new SpyToggleBuffer();
            using var client = MakeHttpClient("""{"Toggles":{"A":"true","B":"false","C":"blue"}}""");
            using var _ = new HttpToggleParser("http://fake/toggles", spy, client: client);

            WaitForStage(spy, "C");
            Assert.AreEqual("true", spy.Staged["A"]);
            Assert.AreEqual("false", spy.Staged["B"]);
            Assert.AreEqual("blue", spy.Staged["C"]);
        }

        [Test]
        public void Http_DoesNotThrowWhenEndpointReturnsError()
        {
            var spy = new SpyToggleBuffer();
            using var client = MakeHttpClient("{}", HttpStatusCode.InternalServerError);
            // Constructor should not throw — error is swallowed, existing appsettings.json state preserved
            Assert.DoesNotThrow(() =>
            {
                using var _ = new HttpToggleParser("http://fake/toggles", spy, client: client);
                Thread.Sleep(100); // let the initial poll fire
            });
        }

        [Test]
        public void Http_DoesNotStageWhenResponseHasNoTogglesProperty()
        {
            var spy = new SpyToggleBuffer();
            using var client = MakeHttpClient("""{"NotToggles":{"Key":"true"}}""");
            using var _ = new HttpToggleParser("http://fake/toggles", spy, client: client);

            Thread.Sleep(200);
            Assert.IsEmpty(spy.Staged);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static HttpClient MakeHttpClient(string json, HttpStatusCode status = HttpStatusCode.OK)
            => new HttpClient(new FakeHttpMessageHandler(json, status));

        private static void WaitForStage(SpyToggleBuffer spy, string key, int timeoutMs = 2000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (!spy.Staged.ContainsKey(key) && DateTime.UtcNow < deadline)
                Thread.Sleep(20);
        }
    }

    internal class SpyToggleBuffer : IToggleBuffer
    {
        public Dictionary<string, string> Staged { get; } = new(StringComparer.Ordinal);
        public void Stage(string key, string rawValue) => Staged[key] = rawValue;
    }

    internal class StubToggleParser : FtrIO.Interfaces.IToggleParser
    {
        private readonly Dictionary<string, bool> _values;
        public StubToggleParser(Dictionary<string, bool> values) => _values = values;

        public bool GetToggleStatus(string toggle)
        {
            if (_values.TryGetValue(toggle, out var val)) return val;
            throw new ToggleDoesNotExistException();
        }

        public bool ParseBoolValueFromSource(string status) => throw new NotImplementedException();
    }

    internal class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _json;
        private readonly HttpStatusCode _status;

        public FakeHttpMessageHandler(string json, HttpStatusCode status = HttpStatusCode.OK)
        {
            _json = json;
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_json, System.Text.Encoding.UTF8, "application/json")
            });
    }
}
