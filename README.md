![FtrIO](assets/ftrio-banner.png)

[![NuGet](https://img.shields.io/nuget/v/FtrIO?color=ff69b4&logo=nuget)](https://www.nuget.org/packages/FtrIO)
[![Docs](https://img.shields.io/badge/docs-ftrio-ff69b4)](https://TheScottBot.github.io/FtrIO/)

# FtrIO

FtrIO == Feature I/O

Gate method execution from config — no `if` statements, no wrapper classes. Decorate a method with `[Toggle]` and it becomes automatically gated by its own name.

## Installation

```bash
dotnet add package FtrIO
```

---

## Simple path — appsettings.json

The default mode requires nothing but a config file. Add a `Toggles` section to `appsettings.json`, decorate methods, done.

### Config format

```json
{
  "Toggles": {
    "SendWelcomeEmail": true,
    "NewCheckoutFlow": false
  }
}
```

### `[Toggle]`

Decorate a method and it is automatically gated by its own name — no `if`, no routing:

```csharp
[Toggle]
public void SendWelcomeEmail() { ... }

SendWelcomeEmail(); // runs only if "SendWelcomeEmail" is true in config
```

This works because `[Toggle]` is an [AspectInjector](https://github.com/pamidur/aspect-injector) aspect: the gating check is woven directly into the compiled IL. **Any project that decorates its own methods needs its own AspectInjector reference** — weaving is per-compilation, not transitive:

```xml
<PackageReference Include="AspectInjector" Version="2.9.0" />
```

**Decorated methods must be real, named methods on a class** (static or instance) — not local functions. Local functions are name-mangled by the compiler and won't resolve correctly.

### `[ToggleAsync]`

For methods returning `Task` or `Task<T>`, use `[ToggleAsync]`. When the toggle is off it returns `Task.CompletedTask` / `Task.FromResult(default)` instead of `null`, so the result is always safely awaitable:

```csharp
[ToggleAsync]
public async Task SendWelcomeEmailAsync()
{
    await emailClient.SendAsync(...);
}

await SendWelcomeEmailAsync(); // safely awaitable whether the toggle is on or off
```

The same per-project `AspectInjector` reference requirement applies.

### `ExecuteMethodIfToggleOn`

For explicit manual control — passing a key override, gating a lambda, or gating a method you don't want to decorate:

```csharp
var featureToggle = new FeatureToggle<bool>();
featureToggle.ExecuteMethodIfToggleOn(SendWelcomeEmail, "SendWelcomeEmail");
```

If the method is decorated with `[Toggle]`, the key is resolved automatically from its name and doesn't need to be passed explicitly.

If a method is both decorated with `[Toggle]` *and* called via `ExecuteMethodIfToggleOn` with an explicit key, both checks apply — the explicit key controls the call site, and the method's own woven check still applies once it runs.

### `ExecuteMethodIfToggleOnAsync`

```csharp
var featureToggle = new FeatureToggle<bool>();

await featureToggle.ExecuteMethodIfToggleOnAsync(
    () => emailClient.SendAsync(), "SendWelcomeEmail");

var result = await featureToggle.ExecuteMethodIfToggleOnAsync(
    () => orderService.PlaceOrderAsync(), "NewCheckoutFlow");
```

When the toggle is off, returns `Task.CompletedTask` or `Task.FromResult(default)` — always safely awaitable.

### Hot-reload

By default, `ToggleParser` reads `appsettings.json` once at startup. Set `ReloadOnChange: true` to pick up file changes on disk without a restart:

```json
{
  "FtrIO": {
    "ReloadOnChange": true
  },
  "Toggles": {
    "SendWelcomeEmail": true
  }
}
```

**This is strongly recommended** for all applications. If you are using any dynamic provider (see [Dynamic providers](#dynamic-providers) below) it is **mandatory** — without it, `ToggleParser` reads the file once at startup and never sees the values that providers flush to it.

### appsettings.json is optional

If `appsettings.json` is absent entirely, every toggle is treated as **on** — nothing is gated off. You can ship without the file and nothing breaks.

If the file *exists* but is missing a `Toggles` entry for a specific key, that throws `ToggleDoesNotExistException` — a present-but-incomplete config is treated as a mistake worth surfacing, not as "everything's on".

---

## Strategy-based decisions

`StrategyToggleParser` builds on the simple path by routing raw config values through a chain of `IToggleDecisionStrategy` implementations before resolving a boolean. `BooleanStrategy` is always appended as the final fallback, so existing `true`/`false` values continue to work with no changes.

Use it as a drop-in replacement via `ToggleParserProvider.Configure` — call sites are unchanged.

### Percentage rollout

Any value ending in `%` is handled by `PercentageRolloutStrategy`. The check is probabilistic per-call — a 20% rollout means roughly 1 in 5 calls execute the method body:

```json
{ "Toggles": { "NewCheckout": "20%" } }
```

```csharp
ToggleParserProvider.Configure(new StrategyToggleParser(new PercentageRolloutStrategy()));

[Toggle]
public void NewCheckout() { ... } // runs ~20% of the time
```

### Blue-green deployment

`BlueGreenStrategy` routes by named deployment slot. The current slot is declared at startup; the config value is the slot that should be active:

```json
{ "Toggles": { "PaymentV2": "blue" } }
```

```csharp
ToggleParserProvider.Configure(new StrategyToggleParser(
    new BlueGreenStrategy("blue", "blue", "green"))); // currentSlot, knownSlots...

[Toggle]
public void PaymentV2() { ... } // runs only when current slot matches config value
```

### Combining strategies

Strategies are tried in registration order — the first whose `CanHandle` returns `true` wins. `BooleanStrategy` is always appended automatically as the final fallback:

```csharp
ToggleParserProvider.Configure(new StrategyToggleParser(
    new PercentageRolloutStrategy(),
    new BlueGreenStrategy("blue", "blue", "green")
));
```

### Custom strategies

Implement `IToggleDecisionStrategy` to add any decision logic your app needs:

```csharp
public class TimeWindowStrategy : IToggleDecisionStrategy
{
    public bool CanHandle(string rawValue) => rawValue.Contains(".."); // e.g. "09:00..17:00"
    public bool ShouldExecute(string key, string rawValue)
    {
        // parse the window and check the current time
    }
}
```

---

## Dynamic providers

Providers let toggle state be driven by external sources — HTTP endpoints, Azure App Config, environment variables — while `appsettings.json` remains the single source of truth for all reads.

**How it works:** providers push updates into a `ToggleProviderBuffer`; the buffer flushes to `appsettings.json` on a configurable interval; `ToggleParser` reads from `appsettings.json` as normal. If a provider goes offline, the last flushed state in the file is served automatically. Existing `[Toggle]`, `[ToggleAsync]`, and `ExecuteMethodIfToggleOn` call sites are completely unchanged.

> **`ReloadOnChange: true` is mandatory when using providers.** Without it, `ToggleParser` reads the file once at startup and never picks up the values providers flush to it.

### Configuration

```json
{
  "FtrIO": {
    "ReloadOnChange": true,
    "FlushInterval": 5
  },
  "Toggles": {
    "SendWelcomeEmail": true
  }
}
```

| Key | Default | Description |
|-----|---------|-------------|
| `ReloadOnChange` | `false` | **Mandatory when using providers.** Attaches a file watcher so `ToggleParser` picks up each buffer flush. |
| `FlushInterval` | `5` | Seconds between buffer flushes to `appsettings.json`. |

### `ToggleProviderBuffer`

Create once at startup and pass to each provider. Reads `FlushInterval` from `appsettings.json` automatically:

```csharp
var buffer = new ToggleProviderBuffer();
```

**Thread safety and write storms:** staging uses a `ConcurrentDictionary` — any number of providers can call `Stage` concurrently. Rapid successive updates to the same key collapse to the last value before the next flush. File writes are serialised; if a write is in progress when the timer fires, that tick is skipped and staged values accumulate for the next tick. Nothing is ever dropped.

**Atomic writes:** values are written to `appsettings.json.tmp` then replaced atomically — a crash mid-write cannot corrupt the file.

**Shutdown:** call `buffer.Dispose()` to flush any remaining staged changes before the process exits.

### HTTP provider

```bash
dotnet add package FtrIO.Providers.Http
```

The endpoint must return a `Toggles` object at the root — the same shape as `appsettings.json`:

```json
{ "Toggles": { "SendWelcomeEmail": "true", "NewCheckout": "50%" } }
```

```csharp
var buffer = new ToggleProviderBuffer();
new HttpToggleParser("https://flags.example.com/toggles", buffer,
    pollInterval: TimeSpan.FromSeconds(30));
```

If the endpoint is unreachable, the error is silently discarded and the last flushed state in `appsettings.json` persists.

### Azure App Config provider

```bash
dotnet add package FtrIO.Providers.AzureAppConfig
```

Keys should be prefixed with `FtrIO:Toggles:` so `FtrIO:Toggles:SendWelcomeEmail` maps to toggle key `SendWelcomeEmail`:

```csharp
var buffer = new ToggleProviderBuffer();

// Connection string
new AzureAppConfigToggleParser("Endpoint=https://...;Id=...;Secret=...", buffer);

// Managed Identity / DefaultAzureCredential
new AzureAppConfigToggleParser(
    new Uri("https://myconfig.azconfig.io"), new DefaultAzureCredential(), buffer);

// With label filter (e.g. production vs. staging)
new AzureAppConfigToggleParser(connectionString, buffer, label: "production");
```

Transient Azure failures are silently discarded; the last flushed state persists.

### Environment variable provider

```csharp
var buffer = new ToggleProviderBuffer();
new EnvironmentVariableToggleParser(buffer); // one-shot snapshot at startup
```

Set env vars with the `FTRIO__Toggles__` prefix (double-underscore matches .NET config hierarchy conventions):

```bash
FTRIO__Toggles__SendWelcomeEmail=true
FTRIO__Toggles__NewCheckout=50%
```

Pass `pollInterval` to re-snapshot periodically — useful if env vars can change at runtime (e.g. Docker secrets volumes):

```csharp
new EnvironmentVariableToggleParser(buffer, pollInterval: TimeSpan.FromMinutes(5));
```

`EnvironmentVariableToggleParser` also implements `IToggleParser` directly for scenarios that don't need the buffer — see `CompositeToggleParser` below.

### Multiple providers

Multiple providers writing to the same buffer work independently. Each polls its own source and stages its keys. If two providers update the same key before a flush, the last staged value wins.

### `CompositeToggleParser`

For read-time fallthrough without the buffer — first-wins across a chain of `IToggleParser` implementations:

```csharp
// Env var overrides appsettings.json at read time — no buffer
ToggleParserProvider.Configure(new CompositeToggleParser(
    new EnvironmentVariableToggleParser(), // standalone — reads on demand
    new ToggleParser()
));
```

### Full wiring example

```csharp
// Create the buffer — reads FlushInterval from appsettings.json (default 5s)
var buffer = new ToggleProviderBuffer();

// Providers push updates to the buffer in the background
new HttpToggleParser("https://flags.example.com/toggles", buffer);
new EnvironmentVariableToggleParser(buffer);

// The read side always comes from appsettings.json
ToggleParserProvider.Configure(new StrategyToggleParser(
    new PercentageRolloutStrategy(),
    new BlueGreenStrategy("blue", "blue", "green")
));

// ... application runs — [Toggle], [ToggleAsync], ExecuteMethodIfToggleOn unchanged ...

buffer.Dispose(); // flush remaining staged changes on shutdown
```

---

## Reference

### Custom parser / Dependency Injection

By default all toggle checks use the built-in `ToggleParser`. To swap in a custom `IToggleParser` — one that reads from a database, a feature-flag service, or a DI container — call `ToggleParserProvider.Configure` once at startup:

```csharp
// Point to a different path
ToggleParserProvider.Configure(new ToggleParser("/etc/myapp"));

// Resolve from DI
ToggleParserProvider.Configure(host.Services.GetRequiredService<IToggleParser>());
```

`ExecuteMethodIfToggleOn` and `ExecuteMethodIfToggleOnAsync` also accept an `IToggleParser` directly for per-call control:

```csharp
await featureToggle.ExecuteMethodIfToggleOnAsync(
    () => SendAsync(), myCustomParser, "SendWelcomeEmail");
```

If `Configure` is never called, the default `ToggleParser` is used automatically — no changes needed for existing consumers.

#### Analyzer behaviour with a custom parser

The bundled Roslyn analyzer (`FTRIO001`) reads `appsettings.json` as a build-time `AdditionalFile`. If your custom parser doesn't use `appsettings.json`, do not register it as an `AdditionalFile` or the analyzer will emit false errors for keys that exist in your custom source but not in the file.

| Scenario | What to do |
|---|---|
| Default `ToggleParser` | Add `appsettings.json` as `AdditionalFiles` to enable the analyzer |
| Custom `IToggleParser` | Do **not** add `AdditionalFiles` — the analyzer stays silent |

```xml
<!-- Enable analyzer (default ToggleParser only) -->
<ItemGroup>
  <AdditionalFiles Include="appsettings.json" />
</ItemGroup>

<!-- Disable entirely -->
<PropertyGroup>
  <NoWarn>FTRIO001</NoWarn>
</PropertyGroup>
```

### Compile-time validation

With `AdditionalFiles` in place, any `[Toggle]`-decorated method whose name has no matching entry in the `Toggles` section produces `FTRIO001` at build time — the build fails rather than the method silently misbehaving at runtime:

```csharp
[Toggle] public void SendWelcomeEmail() { }  // ✓ fine
[Toggle] public void NewCheckoutFlow()  { }  // ✗ FTRIO001: 'NewCheckoutFlow' has no entry in Toggles
```

The analyzer is included automatically when you reference the `FtrIO` NuGet package.

### Exceptions

All exceptions are in the `ToggleExceptions` namespace.

| Exception | When it's thrown |
|-----------|-----------------|
| `ToggleDoesNotExistException` | `appsettings.json` exists but has no entry for the key in `Toggles` |
| `ToggleParsedOutOfRangeException` | A `Toggles` entry exists but its value can't be resolved to a boolean |
| `ToggleAttributeMissingException` | `ExecuteMethodIfToggleOn` called without a `keyName` and the method has no `[Toggle]` attribute |

```csharp
using ToggleExceptions;

try
{
    featureToggle.ExecuteMethodIfToggleOn(MyMethod);
}
catch (ToggleDoesNotExistException)   { /* key missing from appsettings.json */ }
catch (ToggleParsedOutOfRangeException) { /* value not parseable */ }
catch (ToggleAttributeMissingException) { /* no key and no [Toggle] attribute */ }
```

**On async paths:** all three exceptions propagate synchronously — thrown before any `Task` is created, not wrapped in a faulted `Task`. A standard `try/catch` block catches them identically on sync and async call sites.

### Deploying appsettings.json

FtrIO looks for `appsettings.json` in `AppContext.BaseDirectory` at runtime. To ensure it is copied to the output directory:

```xml
<ItemGroup>
  <None Update="appsettings.json">
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

Use `PreserveNewest` instead of `Always` to only copy when the file has changed. This applies only when using the built-in `ToggleParser`.

### Companion tooling

[**ftrio-onetwo**](https://github.com/TheScottBot/ftrio-onetwo) is a command-line utility that supplements this library. See the [ftrio-onetwo repository](https://github.com/TheScottBot/ftrio-onetwo) for installation and usage instructions.
