namespace FtrIO
{
    using AspectInjector.Broker;
    using FtrIO.Classes;
    using FtrIO.Interfaces;

    /// <summary>
    /// Decorate an async method with [ToggleAsync] to gate it by its own method name,
    /// exactly like [Toggle] but safe for methods returning Task or Task&lt;T&gt;.
    ///
    /// When the toggle is off, [ToggleAsync] returns Task.CompletedTask (for Task-returning
    /// methods) or Task.FromResult(default) (for Task&lt;T&gt;-returning methods) instead of null,
    /// so the caller can safely await the result without a NullReferenceException.
    ///
    /// The same per-project AspectInjector PackageReference requirement applies as for [Toggle].
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [Aspect(Scope.Global)]
    [Injection(typeof(ToggleAsync))]
    public class ToggleAsync : Attribute
    {
        [Advice(Kind.Around, Targets = Target.Method)]
        public object? Around(
            [Argument(Source.Name)] string name,
            [Argument(Source.Arguments)] object[] arguments,
            [Argument(Source.Target)] Func<object[], object> target,
            [Argument(Source.ReturnType)] Type returnType)
        {
            IToggleParser parser = ToggleParserProvider.Instance;

            if (parser.GetToggleStatus(name))
            {
                return target(arguments);
            }

            if (returnType == typeof(Task))
                return Task.CompletedTask;

            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var resultType = returnType.GetGenericArguments()[0];
                var defaultValue = resultType.IsValueType ? Activator.CreateInstance(resultType) : null;
                return typeof(Task)
                    .GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(resultType)
                    .Invoke(null, new[] { defaultValue });
            }

            return null;
        }
    }
}
