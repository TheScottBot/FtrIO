![FtrIO](assets/ftrio-banner.png)

[![NuGet](https://img.shields.io/nuget/v/FtrIO?color=ff69b4&logo=nuget)](https://www.nuget.org/packages/FtrIO)
[![Docs](https://img.shields.io/badge/docs-ftrio-ff69b4)](https://TheScottBot.github.io/FtrIO/)

# FtrIO

FtrIO == Feature I/O

Code to enable toggling logic without needing to wrap anything in "if" statements or anything else

## Installation

```bash
dotnet add package FtrIO
```

## The `[Toggle]` attribute

Decorate a method with `[Toggle]` and it becomes gated by its own name against the `Toggles` section of `appsettings.json` - no `if` statement, and no need to route the call through anything else.

```csharp
[Toggle]
public void SendWelcomeEmail() { ... }

SendWelcomeEmail(); // runs only if "SendWelcomeEmail" is true in config
```

This works because `[Toggle]` is also an [AspectInjector](https://github.com/pamidur/aspect-injector) aspect: the gating check is woven directly into the method's compiled IL, so it applies regardless of how the method is called - including a plain, direct call like the one above.

**Any project that wants its own `[Toggle]`-decorated methods to be woven needs its own reference to the AspectInjector package**, even if it already references FtrIO. Weaving happens per-compilation, not transitively through a library reference, so a console app (or any other consumer) that decorates its own methods with `[Toggle]` needs:

```xml
<PackageReference Include="AspectInjector" Version="2.9.0" />
```

in its own `.csproj`. Without it, `[Toggle]` on a method in that project is just inert metadata - it compiles fine, but nothing reads it.

**Decorated methods must be real, named methods on a class** - static or instance, doesn't matter - not local functions. Local functions get name-mangled by the compiler (e.g. a local function called `Calculate` may compile down to something like `<<Main>$>g__Calculate|0_0`), so name-based resolution won't behave as expected, and compiler-generated members like this may not be woven reliably in the first place.

## The `[ToggleAsync]` attribute

For methods that return `Task` or `Task<T>`, use `[ToggleAsync]` instead of `[Toggle]`. It behaves identically — the method is gated by its own name against `appsettings.json` — but handles the async "off" path correctly.

When the toggle is off, `[Toggle]` on an async method would return `null` for the `Task`, causing a `NullReferenceException` when the caller awaits it. `[ToggleAsync]` returns `Task.CompletedTask` (for `Task`-returning methods) or `Task.FromResult(default)` (for `Task<T>`-returning methods) instead, so it is always safe to await.

```csharp
[ToggleAsync]
public async Task SendWelcomeEmailAsync()
{
    await emailClient.SendAsync(...);
}

await SendWelcomeEmailAsync(); // safely awaitable whether the toggle is on or off
```

The same AspectInjector per-project `PackageReference` requirement applies as for `[Toggle]`.

## `ExecuteMethodIfToggleOn`

`FeatureToggle<T>.ExecuteMethodIfToggleOn` still exists for cases where you want explicit, manual control - passing a `keyName` override, gating a lambda, or gating a method you don't want to (or can't) decorate. Methods passed to it can also be decorated with `[Toggle]` so the toggle key is resolved automatically via reflection (using the method's own name) instead of always being passed explicitly.

If a method is both decorated with `[Toggle]` *and* called via `ExecuteMethodIfToggleOn` with an explicit `keyName`, both checks apply: the explicit key controls whether the call site invokes the method at all, and the method's own woven check (based on its own name) still applies once it runs. In practice both checks should agree, since they'd typically reference related config entries - but it's worth knowing that the explicit key is no longer a way to "override" a `[Toggle]`-decorated method's self-gating once AspectInjector is in play for that project.

## `ExecuteMethodIfToggleOnAsync`

For async methods, `FeatureToggle<T>` provides `ExecuteMethodIfToggleOnAsync` overloads that accept `Func<Task>` and `Func<Task<TResult>>`:

```csharp
var featureToggle = new FeatureToggle<bool>();

// Gate a Task-returning method
await featureToggle.ExecuteMethodIfToggleOnAsync(
    () => emailClient.SendAsync(), "SendWelcomeEmail");

// Gate a Task<T>-returning method
var result = await featureToggle.ExecuteMethodIfToggleOnAsync(
    () => orderService.PlaceOrderAsync(), "NewCheckoutFlow");
```

When the toggle is off, `ExecuteMethodIfToggleOnAsync` returns `Task.CompletedTask` (for `Func<Task>`) or `Task.FromResult(default)` (for `Func<Task<TResult>>`), so the result is always safely awaitable.

## Custom parser / Dependency Injection

By default, `[Toggle]`, `[ToggleAsync]`, and `ExecuteMethodIfToggleOn` all use `ToggleParser` — the built-in implementation that reads from `appsettings.json` in `AppContext.BaseDirectory`. This works out of the box with no configuration.

To use a custom `IToggleParser` — for example, one that reads from a database, a feature-flag service, or a DI container — call `ToggleParserProvider.Configure` once at application startup, before any toggled methods run:

```csharp
// Manual — point to a different path or a custom implementation
ToggleParserProvider.Configure(new ToggleParser("/etc/myapp"));

// With Microsoft.Extensions.DependencyInjection
ToggleParserProvider.Configure(host.Services.GetRequiredService<IToggleParser>());
```

From that point on, every `[Toggle]` and `[ToggleAsync]` woven call uses the configured parser. If `Configure` is never called, the default `ToggleParser` is used automatically — existing consumers don't need to change anything.

`ExecuteMethodIfToggleOn` and `ExecuteMethodIfToggleOnAsync` also accept an `IToggleParser` directly if you need per-call control:

```csharp
await featureToggle.ExecuteMethodIfToggleOnAsync(
    () => SendAsync(), myCustomParser, "SendWelcomeEmail");
```

### Analyzer behaviour when using a custom parser

The bundled Roslyn analyzer (`FTRIO001`) works by reading `appsettings.json` as a build-time `AdditionalFile` and checking that every `[Toggle]`-decorated method has a matching entry in the `Toggles` section.

**If you are using a custom `IToggleParser` that does not use `appsettings.json`**, you should not register `appsettings.json` as an `AdditionalFile`, or the analyzer will emit false `FTRIO001` errors for keys that exist in your custom source but not in the file.

| Scenario | What to do |
|---|---|
| Using the default `ToggleParser` with `appsettings.json` | Register `appsettings.json` as `AdditionalFiles` to enable the analyzer |
| Using a custom `IToggleParser` | Do **not** add the `AdditionalFiles` entry — the analyzer will stay silent |

To enable the analyzer (default `ToggleParser` only):

```xml
<ItemGroup>
  <AdditionalFiles Include="appsettings.json" />
</ItemGroup>
```

To disable the analyzer entirely (suppresses `FTRIO001` regardless of parser):

```xml
<PropertyGroup>
  <NoWarn>FTRIO001</NoWarn>
</PropertyGroup>
```

Without the `AdditionalFiles` entry the analyzer is always silent — runtime behaviour is unchanged either way.

## Compile-time validation

When using the default `ToggleParser` with the `AdditionalFiles` entry in place, the Roslyn analyzer catches missing config entries at build time. Any `[Toggle]`-decorated method whose name has no matching entry in the `Toggles` section produces a compiler error (`FTRIO001`) — the build fails rather than the method silently misbehaving at runtime.

```csharp
// appsettings.json has "SendWelcomeEmail" but not "NewCheckoutFlow"

[Toggle] public void SendWelcomeEmail() { }  // ✓ fine
[Toggle] public void NewCheckoutFlow()  { }  // ✗ FTRIO001: 'NewCheckoutFlow' has no entry in Toggles
```

The analyzer is included automatically when you reference the `FtrIO` NuGet package; no separate package is needed.

## Exceptions

All exceptions are in the `ToggleExceptions` namespace.

```csharp
using ToggleExceptions;
```

| Exception | When it's thrown |
|-----------|-----------------|
| `ToggleDoesNotExistException` | `appsettings.json` exists but has no entry for the requested key in the `Toggles` section |
| `ToggleParsedOutOfRangeException` | A `Toggles` entry exists but its value isn't parseable as a boolean (`true`/`false`/`1`/`0`) |
| `ToggleAttributeMissingException` | `ExecuteMethodIfToggleOn` is called without an explicit `keyName` and the method has no `[Toggle]` attribute to fall back on |

Example handling:

```csharp
using ToggleExceptions;

try
{
    featureToggle.ExecuteMethodIfToggleOn(MyMethod);
}
catch (ToggleDoesNotExistException)
{
    // "MyMethod" key is missing from appsettings.json
}
catch (ToggleParsedOutOfRangeException)
{
    // The value for "MyMethod" in appsettings.json isn't true/false/1/0
}
catch (ToggleAttributeMissingException)
{
    // MyMethod has no [Toggle] attribute and no keyName was passed
}
```

## Deploying `appsettings.json`

FtrIO's default config parser looks for `appsettings.json` in the application's output directory at runtime (`AppContext.BaseDirectory`). You need to make sure the file ends up there — it won't be copied automatically unless you tell the build system to do so.

**Local development** — mark the file as copy-to-output in your `.csproj`:

```xml
<ItemGroup>
  <None Update="appsettings.json">
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

`Always` re-copies on every build. Use `PreserveNewest` if you'd rather only copy when the file has changed.

**Build servers / CI** — the same `.csproj` entry handles this automatically as part of `dotnet build`, so no extra steps are needed as long as `appsettings.json` is committed to the repository and the `CopyToOutputDirectory` entry is in place. If you manage config separately (environment-specific files, secrets managers, etc.) and inject `appsettings.json` at deploy time rather than build time, make sure it lands in the same directory as the compiled output before the application starts.

This only applies if you are using FtrIO's built-in `ToggleParser`. If you implement `IToggleParser` yourself, config loading is entirely your responsibility and the above doesn't apply.

## `appsettings.json` is optional

`appsettings.json` doesn't have to exist. If it's missing entirely, nothing has been explicitly toggled off, so every `[Toggle]`-decorated method and every `ExecuteMethodIfToggleOn` call runs normally - `GetToggleStatus` returns `true` for any key in that case.

This only applies when the file itself is absent. If `appsettings.json` exists but is missing a `Toggles` entry for a specific key, that still throws `ToggleDoesNotExistException` as before - a present-but-incomplete config file is treated as a mistake worth surfacing, not as "everything's on".
