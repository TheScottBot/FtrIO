namespace FtrIO.Interfaces
{
    /// <summary>
    /// Receives toggle value updates from providers. Staged values are accumulated in memory
    /// and committed to appsettings.json after each flush interval, making appsettings.json
    /// the single source of truth for all toggle state.
    /// </summary>
    public interface IToggleBuffer
    {
        /// <summary>
        /// Stage a toggle value update. If the same key is staged multiple times before
        /// the next flush, the last value wins. Thread-safe.
        /// </summary>
        void Stage(string key, string rawValue);
    }
}
