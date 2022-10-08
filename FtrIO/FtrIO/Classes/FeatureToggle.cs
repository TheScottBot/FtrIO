namespace FtrIO.Classes
{
    using FtrIO.Enums;
    using FtrIO.Interfaces;
    using System.Diagnostics;

    
    public class FeatureToggle<T> : IFeatureToggle<T>
    {
        public FeatureToggle()
        {
            var sss = (IoAttribute[])typeof(FeatureToggle<T>).GetCustomAttributes(typeof(IoAttribute), true);
        }
        [Io]
        private ToggleStatus Status(bool active)
        {
            return active ? ToggleStatus.Active : ToggleStatus.Inactive;
        }

        public ToggleStatus GetToggleState(IToggleParser parser, string? toggleKey = null)
        {
            string key;
            if (string.IsNullOrEmpty(toggleKey))
            {
                var stack = new StackTrace(new StackFrame(3));
                key = stack.GetFrame(0).GetMethod().Name;
            }
            else
            {
                key = toggleKey;
            }
            return Status(parser.GetToggleStatus(key));
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