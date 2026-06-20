namespace FtrIO.Classes
{
    using System.Collections.Concurrent;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using FtrIO.Interfaces;

    /// <summary>
    /// Buffers toggle value updates from providers and flushes them to appsettings.json
    /// atomically after a configurable interval. appsettings.json is always the on-disk
    /// source of truth — if a provider goes offline, the last flushed state persists there
    /// and ToggleParser continues serving from it.
    ///
    /// Thread safety:
    ///   Staging uses a ConcurrentDictionary — any number of providers can stage concurrently.
    ///   Rapid successive updates to the same key are collapsed: the last write before the
    ///   next flush wins. File writes are serialised with a lock; if a write is already in
    ///   progress when the timer fires, that tick is skipped (staged values are not lost —
    ///   they accumulate until the next flush succeeds).
    ///
    /// Configuration (appsettings.json):
    ///   "FtrIO": {
    ///     "ReloadOnChange": true,   // recommended so ToggleParser sees each flush live
    ///     "FlushInterval": 5        // seconds between flushes, default 5
    ///   }
    ///
    /// Atomic write: staged values are written to a .tmp file then replaced atomically,
    /// so a crash mid-write never leaves a corrupt appsettings.json.
    ///
    /// Dispose() performs a final flush so no staged changes are lost on shutdown.
    /// </summary>
    public class ToggleProviderBuffer : IToggleBuffer, IDisposable
    {
        private readonly string _settingsPath;
        private readonly ConcurrentDictionary<string, string> _pending
            = new(StringComparer.Ordinal);
        private readonly object _writeLock = new();
        private readonly Timer _timer;
        private volatile bool _disposing;

        public ToggleProviderBuffer(string? basePath = null, TimeSpan? flushInterval = null)
        {
            basePath ??= AppContext.BaseDirectory;

            var environment = ResolveEnvironment(basePath);
            var settingsFileName = environment != null
                ? $"appsettings.{environment}.json"
                : "appsettings.json";
            _settingsPath = Path.Combine(basePath, settingsFileName);

            var interval = flushInterval ?? ReadFlushIntervalFromConfig(basePath);
            _timer = new Timer(_ => TimerFlush(), null, interval, interval);
        }

        /// <inheritdoc />
        public void Stage(string key, string rawValue)
            => _pending[key] = rawValue;

        /// <summary>
        /// Flush all staged changes to appsettings.json immediately.
        /// Safe to call concurrently — only one flush runs at a time.
        /// </summary>
        public void FlushNow() => FlushCore();

        public void Dispose()
        {
            _disposing = true;
            _timer.Dispose();
            FlushCore(); // final flush — don't lose staged changes on shutdown
        }

        private void TimerFlush()
        {
            if (_disposing) return;
            FlushCore();
        }

        private void FlushCore()
        {
            if (_pending.IsEmpty) return;

            // Try to acquire the write lock without blocking the timer thread.
            // If a flush is already running, this tick is skipped — pending changes
            // remain in the ConcurrentDictionary and are picked up next tick.
            if (!Monitor.TryEnter(_writeLock)) return;
            try
            {
                // Drain the staging dict. Keys added by providers between now and the
                // end of the write will appear in the next flush, not this one.
                var toWrite = DrainPending();
                if (toWrite.Count == 0) return;

                try
                {
                    var existing = File.Exists(_settingsPath)
                        ? File.ReadAllText(_settingsPath, Encoding.UTF8)
                        : "{}";

                    var updated = MergeToggles(existing, toWrite);
                    WriteAtomically(updated);
                }
                catch
                {
                    // Write failed — re-stage the values so the next flush retries them.
                    // TryAdd: preserves any newer value that arrived during the failed write.
                    foreach (var kv in toWrite)
                        _pending.TryAdd(kv.Key, kv.Value);
                }
            }
            finally
            {
                Monitor.Exit(_writeLock);
            }
        }

        private Dictionary<string, string> DrainPending()
        {
            var drained = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var key in _pending.Keys.ToArray())
                if (_pending.TryRemove(key, out var value))
                    drained[key] = value;
            return drained;
        }

        private void WriteAtomically(string json)
        {
            var tempPath = _settingsPath + ".tmp";
            File.WriteAllText(tempPath, json, Encoding.UTF8);

            if (File.Exists(_settingsPath))
                File.Replace(tempPath, _settingsPath, destinationBackupFileName: null);
            else
                File.Move(tempPath, _settingsPath);
        }

        private static string MergeToggles(string existingJson, Dictionary<string, string> updates)
        {
            using var doc = JsonDocument.Parse(existingJson,
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });

            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true });

            writer.WriteStartObject();

            var togglesWritten = false;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name == "Toggles")
                {
                    togglesWritten = true;
                    writer.WritePropertyName("Toggles");
                    WriteTogglesSection(writer, prop.Value, updates);
                }
                else
                {
                    prop.WriteTo(writer);
                }
            }

            // appsettings.json had no Toggles section yet — append one
            if (!togglesWritten)
            {
                writer.WritePropertyName("Toggles");
                writer.WriteStartObject();
                foreach (var kv in updates)
                    writer.WriteString(kv.Key, kv.Value);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.Flush();

            return Encoding.UTF8.GetString(ms.ToArray());
        }

        private static void WriteTogglesSection(
            Utf8JsonWriter writer,
            JsonElement existing,
            Dictionary<string, string> updates)
        {
            writer.WriteStartObject();
            var written = new HashSet<string>(StringComparer.Ordinal);

            foreach (var toggle in existing.EnumerateObject())
            {
                writer.WritePropertyName(toggle.Name);
                if (updates.TryGetValue(toggle.Name, out var updated))
                    writer.WriteStringValue(updated);
                else
                    toggle.Value.WriteTo(writer); // preserves original JSON type (bool, string, etc.)
                written.Add(toggle.Name);
            }

            // Keys from providers that don't yet exist in appsettings.json
            foreach (var kv in updates)
                if (!written.Contains(kv.Key))
                    writer.WriteString(kv.Key, kv.Value);

            writer.WriteEndObject();
        }

        private static string? ResolveEnvironment(string basePath)
        {
            var path = Path.Combine(basePath, "appsettings.json");
            if (File.Exists(path))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(path));
                    if (doc.RootElement.TryGetProperty("FtrIO", out var ftrio)
                        && ftrio.TryGetProperty("Environment", out var env)
                        && env.GetString() is { Length: > 0 } envValue)
                        return envValue;
                }
                catch { }
            }

            return System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        }

        private static TimeSpan ReadFlushIntervalFromConfig(string basePath)
        {
            var path = Path.Combine(basePath, "appsettings.json");
            if (!File.Exists(path)) return TimeSpan.FromSeconds(5);

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("FtrIO", out var ftrio)
                    && ftrio.TryGetProperty("FlushInterval", out var interval)
                    && interval.TryGetInt32(out var seconds))
                    return TimeSpan.FromSeconds(seconds);
            }
            catch { }

            return TimeSpan.FromSeconds(5);
        }
    }
}
