using FtrIO;
using FtrIO.Classes;
using FtrIO.Strategies;
using System.Text.Json;

// Multi-environment demo:
//   appsettings.json             — base config (FtrIO:Environment = "Development")
//   appsettings.Development.json — overlay: TestingFalse=true, TestingPercentage=80%
//
// ToggleParser merges both files; env values win over the base.
// ToggleProviderBuffer writes to the env file, leaving the base untouched.
// Change FtrIO:Environment in appsettings.json to switch overlays without restarting.

var buffer = new ToggleProviderBuffer();

// Example: uncomment to push from an HTTP endpoint
// new FtrIO.Providers.Http.HttpToggleParser("https://flags.example.com/toggles", buffer);

// Example: uncomment to push from env vars (one-shot at startup)
// new EnvironmentVariableToggleParser(buffer);

// The read side always comes from appsettings.json (+overlay), with strategy decisions on top.
ToggleParserProvider.Configure(new StrategyToggleParser(
    new PercentageRolloutStrategy(),
    new BlueGreenStrategy("blue", "blue", "green")
));

var baseDir = AppContext.BaseDirectory;
var baseFile = Path.Combine(baseDir, "appsettings.json");

// Read the active environment from config so we can report it at startup.
string? activeEnv = null;
using (var doc = JsonDocument.Parse(File.ReadAllText(baseFile)))
{
    if (doc.RootElement.TryGetProperty("FtrIO", out var ftrio)
        && ftrio.TryGetProperty("Environment", out var envEl))
        activeEnv = envEl.GetString();
}

Console.WriteLine(new string('=', 60));
Console.WriteLine("FtrIO PlaygroundConsole — multi-environment demo");
Console.WriteLine(new string('=', 60));
Console.WriteLine($"Base file   : {baseFile}");
if (activeEnv is { Length: > 0 })
{
    var envFile = Path.Combine(baseDir, $"appsettings.{activeEnv}.json");
    Console.WriteLine($"Env overlay : {envFile}");
    Console.WriteLine($"Active env  : {activeEnv}  (buffer writes to overlay; base stays untouched)");
}
else
{
    Console.WriteLine("Env overlay : none  (set FtrIO:Environment in appsettings.json to activate)");
}
Console.WriteLine(new string('-', 60));
Console.WriteLine("Edit the overlay file to see toggle changes live. Ctrl+C to exit.");
Console.WriteLine(new string('-', 60));

while (true)
{
    Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}]");
    Playground.TestingTrue();
    Playground.TestingFalse();
    Playground.TestingNoAttribute();
    Playground.TestingPercentage();
    Playground.TestingBlueGreen();
    Thread.Sleep(2000);
}

// Dispose flushes any remaining staged changes before exit.
buffer.Dispose();

internal static class Playground
{
    [Toggle]
    public static void TestingTrue()
        => Console.WriteLine("  TestingTrue       — ON  (base: true)");

    [Toggle]
    public static void TestingFalse()
        => Console.WriteLine("  TestingFalse      — ON  (overlay: true, base: false)");

    public static void TestingNoAttribute()
        => Console.WriteLine("  TestingNoAttribute — always runs (no [Toggle])");

    [Toggle]
    public static void TestingPercentage()
        => Console.WriteLine("  TestingPercentage — ON this tick  (overlay: 80%, base: 50%)");

    [Toggle]
    public static void TestingBlueGreen()
        => Console.WriteLine("  TestingBlueGreen  — ON  (slot: blue, base: blue)");
}
