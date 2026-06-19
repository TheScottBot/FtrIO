namespace FtrIO.Classes
{
    using System.IO;
    using ToggleExceptions;
    using FtrIO.Interfaces;
    using Microsoft.Extensions.Configuration;
    public class ToggleParser : IToggleParser
    {
        private readonly bool _configFileExists;
        private readonly IConfigurationSection _toggles;

        public ToggleParser(string? basePath = null)
        {
            basePath ??= AppContext.BaseDirectory;
            _configFileExists = File.Exists(Path.Combine(basePath, "appsettings.json"));

            if (_configFileExists)
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(basePath)
                    .AddJsonFile("appsettings.json", optional: true)
                    .Build();

                var reloadOnChange = string.Equals(config["FtrIO:ReloadOnChange"], "true", StringComparison.OrdinalIgnoreCase);

                if (reloadOnChange)
                {
                    config = new ConfigurationBuilder()
                        .SetBasePath(basePath)
                        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .Build();
                }

                _toggles = config.GetSection("Toggles");
            }
        }

        public bool ToggleConfigTagExists()
        {
            return _configFileExists;
        }

        public bool GetToggleStatus(string toggle)
        {
            if (!_configFileExists)
            {
                // No appsettings.json on disk at all, so nothing has been
                // explicitly toggled off - everything should run.
                return true;
            }

            if (_toggles[toggle] == null)
            {
                throw new ToggleDoesNotExistException();
            }

            return ParseBoolValueFromSource(_toggles[toggle]);
        }

        public bool ParseBoolValueFromSource(string status)
        {
            if (status == "1" || status.ToLower() == "true")
            {
                return true;
            }
            else if (status == "0" || status.ToLower() == "false")
            {
                return false;
            }
            else
            {
                throw new ToggleParsedOutOfRangeException();
            }
        }
    }
}
