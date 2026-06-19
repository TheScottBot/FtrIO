namespace FtrIO.Classes
{
    using FtrIO.Enums;
    using FtrIO.Interfaces;
    using System.Reflection;
    using ToggleExceptions;

    public class FeatureToggle<T> : IFeatureToggle<T>
    {
        private static ToggleStatus Status(bool active)
        {
            return active ? ToggleStatus.Active : ToggleStatus.Inactive;
        }

        public ToggleStatus GetToggleState(IToggleParser parser, string toggleKey)
        {
            return Status(parser.GetToggleStatus(toggleKey));
        }

        /// <summary>
        /// Resolves the toggle key to use for a method passed in to ExecuteMethodIfToggleOn.
        /// An explicitly supplied keyName always takes precedence. Otherwise, the method must be
        /// decorated with a [Toggle] attribute (FtrIO.Toggle), in which case the method's own
        /// name is used as the key. If neither an explicit key nor the attribute is present,
        /// a ToggleAttributeMissingException is thrown.
        /// </summary>
        private static string ResolveToggleKey(Delegate methodToRun, string? keyName)
        {
            if (!string.IsNullOrEmpty(keyName))
            {
                return keyName;
            }

            var toggleAttribute = methodToRun.Method.GetCustomAttribute<Toggle>();
            if (toggleAttribute == null)
            {
                throw new ToggleAttributeMissingException(
                    $"Method '{methodToRun.Method.Name}' has no [Toggle] attribute and no keyName was provided.");
            }

            return methodToRun.Method.Name;
        }

        public void ExecuteMethodIfToggleOn(Action methodToRun, IToggleParser configParser, string? keyName = null)
        {
            var key = ResolveToggleKey(methodToRun, keyName);
            var response = GetToggleState(configParser, key);
            if (response == ToggleStatus.Active)
            {
                methodToRun();
            }
        }

        public void ExecuteMethodIfToggleOn(Action methodToRun, string? keyName = null)
        {
            IToggleParser configParser = new ToggleParser();
            ExecuteMethodIfToggleOn(methodToRun, configParser, keyName);
        }

        public T ExecuteMethodIfToggleOn(Func<T> methodToRun, string? keyName = null)
        {
            IToggleParser configParser = new ToggleParser();
            return ExecuteMethodIfToggleOn(methodToRun, configParser, keyName);
        }

        public T ExecuteMethodIfToggleOn(Func<T> methodToRun, IToggleParser configParser, string? keyName = null)
        {
            var key = ResolveToggleKey(methodToRun, keyName);
            var response = GetToggleState(configParser, key);
            if (response == ToggleStatus.Active)
            {
                return methodToRun();
            }

            return default(T);
        }

        public Task ExecuteMethodIfToggleOnAsync(Func<Task> methodToRun, IToggleParser configParser, string? keyName = null)
        {
            var key = ResolveToggleKey(methodToRun, keyName);
            var response = GetToggleState(configParser, key);
            if (response == ToggleStatus.Active)
            {
                return methodToRun();
            }

            return Task.CompletedTask;
        }

        public Task ExecuteMethodIfToggleOnAsync(Func<Task> methodToRun, string? keyName = null)
        {
            IToggleParser configParser = new ToggleParser();
            return ExecuteMethodIfToggleOnAsync(methodToRun, configParser, keyName);
        }

        public Task<TResult> ExecuteMethodIfToggleOnAsync<TResult>(Func<Task<TResult>> methodToRun, IToggleParser configParser, string? keyName = null)
        {
            var key = ResolveToggleKey(methodToRun, keyName);
            var response = GetToggleState(configParser, key);
            if (response == ToggleStatus.Active)
            {
                return methodToRun();
            }

            return Task.FromResult(default(TResult)!);
        }

        public Task<TResult> ExecuteMethodIfToggleOnAsync<TResult>(Func<Task<TResult>> methodToRun, string? keyName = null)
        {
            IToggleParser configParser = new ToggleParser();
            return ExecuteMethodIfToggleOnAsync(methodToRun, configParser, keyName);
        }
    }
}
