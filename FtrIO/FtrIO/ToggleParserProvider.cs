namespace FtrIO
{
    using FtrIO.Classes;
    using FtrIO.Interfaces;

    /// <summary>
    /// Ambient provider for the IToggleParser used by [Toggle] and [ToggleAsync] aspects.
    ///
    /// Defaults to ToggleParser (reads from appsettings.json in AppContext.BaseDirectory).
    /// Override at application startup to inject a custom parser — including one resolved
    /// from a DI container:
    ///
    ///   // Manual:
    ///   ToggleParserProvider.Configure(new ToggleParser("/custom/path"));
    ///
    ///   // With Microsoft.Extensions.DependencyInjection:
    ///   ToggleParserProvider.Configure(serviceProvider.GetRequiredService&lt;IToggleParser&gt;());
    /// </summary>
    public static class ToggleParserProvider
    {
        private static IToggleParser? _instance;

        public static IToggleParser Instance => _instance ??= new ToggleParser();

        public static void Configure(IToggleParser parser)
        {
            _instance = parser ?? throw new ArgumentNullException(nameof(parser));
        }
    }
}
