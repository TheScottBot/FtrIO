namespace FtrIO
{
    using AspectInjector.Broker;
    using FtrIO.Classes;
    using FtrIO.Interfaces;

    /// <summary>
    /// Decorate a method with [Toggle] to gate it by its own method name.
    /// As of this version, FtrIO itself doubles as an AspectInjector aspect:
    /// the gating check is woven directly into the decorated method's IL at
    /// compile time, so the method is gated even when called directly -
    /// no call through FeatureToggle.ExecuteMethodIfToggleOn is required.
    ///
    /// IMPORTANT: any project that wants its OWN [Toggle]-decorated methods
    /// to be woven (not just FtrIO itself) must add its own PackageReference
    /// to AspectInjector. Weaving happens per-compilation, so a console app
    /// or other consumer referencing FtrIO.dll does not get this "for free"
    /// just by referencing the library - see README for details.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [Aspect(Scope.Global)]
    [Injection(typeof(Toggle))]
    public class Toggle : Attribute
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

            if (returnType == typeof(void))
            {
                return null;
            }

            return returnType.IsValueType ? Activator.CreateInstance(returnType) : null;
        }
    }
}
