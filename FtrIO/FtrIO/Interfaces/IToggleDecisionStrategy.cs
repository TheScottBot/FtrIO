namespace FtrIO.Interfaces
{
    public interface IToggleDecisionStrategy
    {
        bool CanHandle(string rawValue);
        bool ShouldExecute(string toggleKey, string rawValue);
    }
}
