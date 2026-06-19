namespace FtrIO.Interfaces
{
    /// <summary>
    /// Source of raw (unparsed) toggle values. Implement this to feed toggle state from
    /// any backing store — environment variables, HTTP endpoints, Azure App Config, etc.
    /// The raw string is passed through the IToggleDecisionStrategy chain by StrategyToggleParser.
    /// Returns null when the key is not present in this provider.
    /// </summary>
    public interface IToggleValueProvider
    {
        string? GetRawValue(string key);
    }
}
