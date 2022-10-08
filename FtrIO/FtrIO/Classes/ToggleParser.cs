namespace FtrIO.Classes
{
    using ToggleExceptions;
    using FtrIO.Interfaces;
    using Microsoft.Extensions.Configuration;
    public class ToggleParser : IToggleParser
    {
        private readonly IConfigurationSection _toggles;

        public ToggleParser()
        {
            if (ToggleConfigTagExists())
            {
                _toggles = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("Toggles");
            }
        }

        public bool ToggleConfigTagExists()
        {
            var toggleSection = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("Toggles");
            return toggleSection != null;
        }

        public bool GetToggleStatus(string toggle)
        {
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

            throw new ToggleDoesNotExistException();
        }
    }
}