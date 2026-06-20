namespace FtrIO.Strategies
{
    using FtrIO.Interfaces;

    public class BooleanStrategy : IToggleDecisionStrategy
    {
        public bool CanHandle(string rawValue)
            => rawValue.Equals("true", StringComparison.OrdinalIgnoreCase)
               || rawValue.Equals("false", StringComparison.OrdinalIgnoreCase)
               || rawValue == "1"
               || rawValue == "0";

        public bool ShouldExecute(string toggleKey, string rawValue)
            => rawValue.Equals("true", StringComparison.OrdinalIgnoreCase) || rawValue == "1";
    }
}
