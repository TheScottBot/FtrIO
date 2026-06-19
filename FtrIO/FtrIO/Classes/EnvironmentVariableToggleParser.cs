namespace FtrIO.Classes
{
    using System.Collections;
    using FtrIO.Interfaces;
    using ToggleExceptions;

    /// <summary>
    /// Reads toggle values from environment variables.
    ///
    /// Two modes:
    ///
    /// Standalone — implements IToggleParser directly, resolves each toggle on demand.
    /// Useful for simple scenarios where you want env vars to override appsettings.json
    /// via CompositeToggleParser, without the buffer pipeline.
    ///
    ///   ToggleParserProvider.Configure(new CompositeToggleParser(
    ///       new EnvironmentVariableToggleParser(),
    ///       new ToggleParser()));
    ///
    /// Buffer mode — snapshots all matching env vars and stages them to an IToggleBuffer,
    /// which then flushes the values to appsettings.json. The optional pollInterval causes
    /// the snapshot to repeat so runtime env var changes (e.g. Docker secrets volumes)
    /// are picked up. Without a pollInterval, the snapshot runs once at startup.
    ///
    ///   var buffer = new ToggleProviderBuffer();
    ///   new EnvironmentVariableToggleParser(buffer);
    ///   ToggleParserProvider.Configure(new ToggleParser()); // reads from appsettings.json
    ///
    /// Default prefix: FTRIO__Toggles__ (double-underscore follows .NET config hierarchy
    /// conventions). Set FTRIO__Toggles__SendWelcomeEmail=true to enable that toggle.
    /// </summary>
    public class EnvironmentVariableToggleParser : IToggleParser, IDisposable
    {
        private readonly string _prefix;
        private readonly IToggleBuffer? _buffer;
        private readonly Timer? _timer;

        /// <summary>Standalone mode — reads env vars on demand via IToggleParser.</summary>
        public EnvironmentVariableToggleParser(string prefix = "FTRIO__Toggles__")
        {
            _prefix = prefix ?? throw new ArgumentNullException(nameof(prefix));
        }

        /// <summary>
        /// Buffer mode — snapshots all matching env vars now and stages them to the buffer.
        /// Pass a pollInterval to re-snapshot periodically; omit for a one-shot startup push.
        /// </summary>
        public EnvironmentVariableToggleParser(
            IToggleBuffer buffer,
            string prefix = "FTRIO__Toggles__",
            TimeSpan? pollInterval = null)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _prefix = prefix ?? throw new ArgumentNullException(nameof(prefix));

            PushSnapshot();

            if (pollInterval.HasValue)
                _timer = new Timer(_ => PushSnapshot(), null, pollInterval.Value, pollInterval.Value);
        }

        // ── IToggleParser (standalone mode) ───────────────────────────────────

        public bool GetToggleStatus(string toggle)
        {
            var raw = Environment.GetEnvironmentVariable(_prefix + toggle);
            if (raw == null) throw new ToggleDoesNotExistException();
            return ParseBoolValueFromSource(raw);
        }

        public bool ParseBoolValueFromSource(string status)
        {
            if (status.Equals("true", StringComparison.OrdinalIgnoreCase) || status == "1")
                return true;
            if (status.Equals("false", StringComparison.OrdinalIgnoreCase) || status == "0")
                return false;
            throw new ToggleParsedOutOfRangeException();
        }

        // ── Buffer push ────────────────────────────────────────────────────────

        private void PushSnapshot()
        {
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                var envKey = entry.Key?.ToString();
                if (envKey == null || !envKey.StartsWith(_prefix, StringComparison.Ordinal))
                    continue;

                var toggleKey = envKey.Substring(_prefix.Length);
                var value = entry.Value?.ToString() ?? string.Empty;
                _buffer!.Stage(toggleKey, value);
            }
        }

        public void Dispose() => _timer?.Dispose();
    }
}
