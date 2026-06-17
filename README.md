# FtrIO

FtrIO == Feature I/O

Code to enable toggling logic without needing to wrap anything in "if" statements or anything else

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

## `ExecuteMethodIfToggleOn`

`FeatureToggle<T>.ExecuteMethodIfToggleOn` still exists for cases where you want explicit, manual control - passing a `keyName` override, gating a lambda, or gating a method you don't want to (or can't) decorate. Methods passed to it can also be decorated with `[Toggle]` so the toggle key is resolved automatically via reflection (using the method's own name) instead of always being passed explicitly.

If a method is both decorated with `[Toggle]` *and* called via `ExecuteMethodIfToggleOn` with an explicit `keyName`, both checks apply: the explicit key controls whether the call site invokes the method at all, and the method's own woven check (based on its own name) still applies once it runs. In practice both checks should agree, since they'd typically reference related config entries - but it's worth knowing that the explicit key is no longer a way to "override" a `[Toggle]`-decorated method's self-gating once AspectInjector is in play for that project.

## `appsettings.json` is optional

`appsettings.json` doesn't have to exist. If it's missing entirely, nothing has been explicitly toggled off, so every `[Toggle]`-decorated method and every `ExecuteMethodIfToggleOn` call runs normally - `GetToggleStatus` returns `true` for any key in that case.

This only applies when the file itself is absent. If `appsettings.json` exists but is missing a `Toggles` entry for a specific key, that still throws `ToggleDoesNotExistException` as before - a present-but-incomplete config file is treated as a mistake worth surfacing, not as "everything's on".
