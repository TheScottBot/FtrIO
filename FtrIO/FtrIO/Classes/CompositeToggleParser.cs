namespace FtrIO.Classes
{
    using FtrIO.Interfaces;
    using ToggleExceptions;

    /// <summary>
    /// Chains multiple IToggleParser implementations with first-wins fallthrough.
    /// On each toggle check, providers are tried in order; the first one that returns
    /// a value (without throwing ToggleDoesNotExistException) wins. If all providers
    /// throw ToggleDoesNotExistException, CompositeToggleParser re-throws it.
    ///
    /// Typical use: env var overrides → remote provider → appsettings.json fallback.
    ///
    ///   ToggleParserProvider.Configure(new CompositeToggleParser(
    ///       new EnvironmentVariableToggleParser(),
    ///       new HttpToggleParser("https://flags.internal/toggles"),
    ///       new ToggleParser()
    ///   ));
    /// </summary>
    public class CompositeToggleParser : IToggleParser
    {
        private readonly IToggleParser[] _parsers;

        public CompositeToggleParser(params IToggleParser[] parsers)
        {
            if (parsers == null || parsers.Length == 0)
                throw new ArgumentException("At least one parser must be specified.", nameof(parsers));
            _parsers = parsers;
        }

        public bool GetToggleStatus(string toggle)
        {
            foreach (var parser in _parsers)
            {
                try
                {
                    return parser.GetToggleStatus(toggle);
                }
                catch (ToggleDoesNotExistException) { }
            }
            throw new ToggleDoesNotExistException();
        }

        public bool ParseBoolValueFromSource(string status)
            => _parsers[0].ParseBoolValueFromSource(status);
    }
}
