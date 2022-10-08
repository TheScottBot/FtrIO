namespace FtrIO.Classes
{
    using FtrIO.Enums;
    using FtrIO.Interfaces;
    
    public class FeatureToggle<T> : IFeatureToggle<T>
    {
        private ToggleStatus Status(bool active)
        {
            return active ? ToggleStatus.Active : ToggleStatus.Inactive;
        }

        public ToggleStatus GetToggleState(IToggleParser parser, string toggleKey)
        {
            return Status(parser.GetToggleStatus(toggleKey));
        }


        public void ExecuteMethodIfToggleOn(Action methodToRun, IToggleParser configParser, string keyName)
        {
            var response = GetToggleState(configParser, keyName);
            if (response == ToggleStatus.Active)
            {
                methodToRun();
            }
        }

        public void ExecuteMethodIfToggleOn(Action methodToRun, string keyName)
        {
            IToggleParser configParser = new ToggleParser();
            ExecuteMethodIfToggleOn(methodToRun, configParser, keyName);
        }

        public T ExecuteMethodIfToggleOn(Func<T> methodToRun, string keyName)
        {
            IToggleParser configParser = new ToggleParser();
            return ExecuteMethodIfToggleOn(methodToRun, configParser, keyName);
        }

        public T ExecuteMethodIfToggleOn(Func<T> methodToRun, IToggleParser configParser, string keyName)
        {
            var response = GetToggleState(configParser, keyName);
            if (response == ToggleStatus.Active)
            {
                return methodToRun();
            }

            return default(T);
        }
    }
}