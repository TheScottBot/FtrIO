namespace FtrIO.Providers.AzureAppConfig
{
    using System.Threading;
    using Azure.Core;
    using Azure.Data.AppConfiguration;
    using FtrIO.Interfaces;

    /// <summary>
    /// Polls Azure App Configuration for toggle values and stages them to an IToggleBuffer,
    /// which then flushes them to appsettings.json. appsettings.json is the source of
    /// truth for all reads — if Azure App Config is unreachable, the last flushed state
    /// in appsettings.json is served automatically (fail-safe).
    ///
    /// Keys in App Config: {keyPrefix}{toggleName}
    /// Default prefix: "FtrIO:Toggles:" — so "FtrIO:Toggles:SendWelcomeEmail" maps to
    /// toggle key "SendWelcomeEmail".
    ///
    /// Values support the same raw formats as appsettings.json: "true", "false", "50%",
    /// "blue", etc. Strategy decisions (PercentageRolloutStrategy, BlueGreenStrategy)
    /// are applied at read time by StrategyToggleParser — not at fetch time.
    ///
    /// Usage (connection string):
    ///   var buffer = new ToggleProviderBuffer();
    ///   new AzureAppConfigToggleParser("Endpoint=https://...;Id=...;Secret=...", buffer);
    ///   ToggleParserProvider.Configure(new StrategyToggleParser(new PercentageRolloutStrategy()));
    ///
    /// Usage (Managed Identity / DefaultAzureCredential):
    ///   new AzureAppConfigToggleParser(new Uri("https://myconfig.azconfig.io"),
    ///       new DefaultAzureCredential(), buffer);
    ///
    /// Label filtering:
    ///   new AzureAppConfigToggleParser(connectionString, buffer, label: "production");
    /// </summary>
    public class AzureAppConfigToggleParser : IDisposable
    {
        private readonly ConfigurationClient _client;
        private readonly string _keyPrefix;
        private readonly string? _label;
        private readonly IToggleBuffer _buffer;
        private readonly Timer _timer;

        public AzureAppConfigToggleParser(
            string connectionString,
            IToggleBuffer buffer,
            string keyPrefix = "FtrIO:Toggles:",
            string? label = null,
            TimeSpan? pollInterval = null)
        {
            _client = new ConfigurationClient(connectionString);
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _keyPrefix = keyPrefix ?? throw new ArgumentNullException(nameof(keyPrefix));
            _label = label;

            var interval = pollInterval ?? TimeSpan.FromSeconds(30);
            _timer = new Timer(_ => _ = PollAsync(), null, TimeSpan.Zero, interval);
        }

        public AzureAppConfigToggleParser(
            Uri endpoint,
            TokenCredential credential,
            IToggleBuffer buffer,
            string keyPrefix = "FtrIO:Toggles:",
            string? label = null,
            TimeSpan? pollInterval = null)
        {
            _client = new ConfigurationClient(endpoint, credential);
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _keyPrefix = keyPrefix ?? throw new ArgumentNullException(nameof(keyPrefix));
            _label = label;

            var interval = pollInterval ?? TimeSpan.FromSeconds(30);
            _timer = new Timer(_ => _ = PollAsync(), null, TimeSpan.Zero, interval);
        }

        private async Task PollAsync()
        {
            try
            {
                var selector = new SettingSelector
                {
                    KeyFilter = _keyPrefix + "*",
                    LabelFilter = _label ?? "\0"
                };

                await foreach (var setting in _client
                    .GetConfigurationSettingsAsync(selector).ConfigureAwait(false))
                {
                    var toggleName = setting.Key.Substring(_keyPrefix.Length);
                    if (!string.IsNullOrEmpty(toggleName))
                        _buffer.Stage(toggleName, setting.Value ?? string.Empty);
                }
            }
            catch
            {
                // Transient Azure failure — last flushed state in appsettings.json persists.
            }
        }

        public void Dispose() => _timer.Dispose();
    }
}
