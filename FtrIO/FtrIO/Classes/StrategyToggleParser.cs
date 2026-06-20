namespace FtrIO.Classes
{
    using System.IO;
    using FtrIO.Interfaces;
    using FtrIO.Strategies;
    using Microsoft.Extensions.Configuration;
    using ToggleExceptions;

    /// <summary>
    /// An IToggleParser that routes raw toggle values through a chain of IToggleDecisionStrategy
    /// implementations before resolving a boolean result. BooleanStrategy is always appended as
    /// the final fallback so existing true/false config values continue to work.
    ///
    /// Two ways to supply values:
    ///   1. appsettings.json (default) — same two-pass reload behaviour as ToggleParser.
    ///   2. IToggleValueProvider — any external source (env vars, HTTP, Azure App Config).
    ///
    /// Usage:
    ///   // appsettings.json with strategies
    ///   new StrategyToggleParser(new PercentageRolloutStrategy(), new BlueGreenStrategy(...))
    ///
    ///   // HTTP source with strategies
    ///   new StrategyToggleParser(new HttpToggleParser("https://..."), new PercentageRolloutStrategy())
    /// </summary>
    public class StrategyToggleParser : IToggleParser
    {
        private readonly IToggleValueProvider? _provider;
        private readonly IToggleDecisionStrategy[] _strategies;

        // appsettings.json path (used when no IToggleValueProvider)
        private readonly bool _configFileExists;
        private readonly IConfigurationSection? _toggles;

        public StrategyToggleParser(params IToggleDecisionStrategy[] strategies)
            : this((string?)null, strategies) { }

        public StrategyToggleParser(string? basePath, params IToggleDecisionStrategy[] strategies)
        {
            _strategies = BuildStrategyChain(strategies);
            basePath ??= AppContext.BaseDirectory;
            _configFileExists = File.Exists(Path.Combine(basePath, "appsettings.json"));

            if (_configFileExists)
            {
                // First pass: read FtrIO settings from the base file only.
                var bootstrap = new ConfigurationBuilder()
                    .SetBasePath(basePath)
                    .AddJsonFile("appsettings.json", optional: true)
                    .Build();

                var reloadOnChange = string.Equals(
                    bootstrap["FtrIO:ReloadOnChange"], "true", StringComparison.OrdinalIgnoreCase);

                var environment = bootstrap["FtrIO:Environment"]
                    ?? System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                    ?? System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

                // Second pass: build the live config with correct reload and env layer.
                var builder = new ConfigurationBuilder()
                    .SetBasePath(basePath)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: reloadOnChange);

                if (environment != null)
                    builder.AddJsonFile(
                        $"appsettings.{environment}.json", optional: true, reloadOnChange: reloadOnChange);

                _toggles = builder.Build().GetSection("Toggles");
            }
        }

        public StrategyToggleParser(IToggleValueProvider provider, params IToggleDecisionStrategy[] strategies)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _strategies = BuildStrategyChain(strategies);
            _configFileExists = true;
        }

        public bool GetToggleStatus(string toggle)
        {
            string? rawValue;

            if (_provider != null)
            {
                rawValue = _provider.GetRawValue(toggle);
                if (rawValue == null) throw new ToggleDoesNotExistException();
            }
            else
            {
                if (!_configFileExists) return true;
                rawValue = _toggles?[toggle];
                if (rawValue == null) throw new ToggleDoesNotExistException();
            }

            var strategy = _strategies.FirstOrDefault(s => s.CanHandle(rawValue));
            if (strategy == null) throw new ToggleParsedOutOfRangeException();
            return strategy.ShouldExecute(toggle, rawValue);
        }

        public bool ParseBoolValueFromSource(string status)
        {
            var strategy = _strategies.FirstOrDefault(s => s.CanHandle(status));
            if (strategy == null) throw new ToggleParsedOutOfRangeException();
            return strategy.ShouldExecute(string.Empty, status);
        }

        private static IToggleDecisionStrategy[] BuildStrategyChain(IToggleDecisionStrategy[] strategies)
        {
            var chain = strategies.ToList();
            if (!chain.Any(s => s is BooleanStrategy))
                chain.Add(new BooleanStrategy());
            return chain.ToArray();
        }
    }
}
