namespace FtrIO.Strategies
{
    using System.Globalization;
    using FtrIO.Interfaces;
    using ToggleExceptions;

    public class PercentageRolloutStrategy : IToggleDecisionStrategy
    {
        public bool CanHandle(string rawValue) => rawValue.TrimEnd().EndsWith("%");

        public bool ShouldExecute(string toggleKey, string rawValue)
        {
            var numStr = rawValue.TrimEnd('%').Trim();
            if (!double.TryParse(numStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var pct)
                || pct < 0 || pct > 100)
                throw new ToggleParsedOutOfRangeException();

            return Random.Shared.NextDouble() * 100 < pct;
        }
    }
}
