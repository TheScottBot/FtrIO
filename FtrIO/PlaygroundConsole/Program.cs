using FtrIO;
using FtrIO.Classes;
using FtrIO.Strategies;

// appsettings.json is the single source of truth for all toggle reads.
// ReloadOnChange: true means ToggleParser picks up buffer flushes automatically.
//
// Providers push updates into the buffer; the buffer flushes to appsettings.json
// every FlushInterval seconds (configured in appsettings.json, default 5s).
// If a provider goes offline, the last flushed state in appsettings.json is served.
//
// Uncomment the HTTP or Azure providers below to see the full pipeline in action.

var buffer = new ToggleProviderBuffer();

// Example: uncomment to push from an HTTP endpoint
// new FtrIO.Providers.Http.HttpToggleParser("https://flags.example.com/toggles", buffer);

// Example: uncomment to push from env vars (one-shot at startup)
// new EnvironmentVariableToggleParser(buffer);

// The read side always comes from appsettings.json, optionally with strategy decisions on top.
ToggleParserProvider.Configure(new StrategyToggleParser(
    new PercentageRolloutStrategy(),
    new BlueGreenStrategy("blue", "blue", "green")
));

Playground.TestingTrue();
Playground.TestingFalse();
Playground.TestingNoAttribute();
Playground.TestingPercentage();
Playground.TestingBlueGreen();

// Dispose flushes any remaining staged changes before exit.
buffer.Dispose();

internal static class Playground
{
    [Toggle]
    public static void TestingTrue()
        => Console.WriteLine("Hello, World! TestingTrue");

    [Toggle]
    public static void TestingFalse()
        => Console.WriteLine("Hello, World! TestingFalse");

    public static void TestingNoAttribute()
        => Console.WriteLine("Hello, World! TestingNoAttribute");

    [Toggle]
    public static void TestingPercentage()
        => Console.WriteLine("Hello, World! TestingPercentage (50% rollout)");

    [Toggle]
    public static void TestingBlueGreen()
        => Console.WriteLine("Hello, World! TestingBlueGreen (current slot: blue)");
}
