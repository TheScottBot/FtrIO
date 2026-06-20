namespace FtrIO.Strategies
{
    using FtrIO.Interfaces;

    public class BlueGreenStrategy : IToggleDecisionStrategy
    {
        private readonly string _currentSlot;
        private readonly HashSet<string> _knownSlots;

        public BlueGreenStrategy(string currentSlot, params string[] knownSlots)
        {
            _currentSlot = currentSlot ?? throw new ArgumentNullException(nameof(currentSlot));
            _knownSlots = new HashSet<string>(knownSlots, StringComparer.OrdinalIgnoreCase);
        }

        public bool CanHandle(string rawValue) => _knownSlots.Contains(rawValue.Trim());

        public bool ShouldExecute(string toggleKey, string rawValue)
            => string.Equals(rawValue.Trim(), _currentSlot, StringComparison.OrdinalIgnoreCase);
    }
}
