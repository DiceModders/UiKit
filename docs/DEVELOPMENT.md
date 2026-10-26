# Development

This document is for working on UiKit itself. For using the library from a mod, start with the
[main README](../README.md). For internals, see [ARCHITECTURE.md](ARCHITECTURE.md) and [NATIVE.md](NATIVE.md).

## Requirements

- .NET 6 SDK.
- Dice Kingdoms with BepInEx 6 for IL2CPP.
- The interop references configured by `UiKit.csproj`.

## Build

```bash
dotnet build UiKit.csproj -c Debug --nologo
dotnet build UiKit.csproj -c Release --nologo
```

Outputs:

```text
bin/Debug/net6.0/DiceModders.UiKit.dll
bin/Release/net6.0/DiceModders.UiKit.dll
```

The project has nullable annotations enabled. Keep both Debug and Release builds warning-free. The current expected
result is `0 warnings, 0 errors`.

## Install for a local test

Copy the built assembly to the game's plugin directory:

```bash
cp bin/Debug/net6.0/DiceModders.UiKit.dll \
   "$HOME/.local/share/Steam/steamapps/common/Dice-Kingdoms/BepInEx/plugins/"
```

Keep only one UiKit assembly installed. If a previous reference implementation such as `UIKit_Another_Version` is
present, remove it before testing.

## Demo

The built-in demo is the manual smoke test. Enable it in `BepInEx/config/DiceModders.UiKit.cfg`:

```ini
[Demo]
Enabled = true

[Widgets]
Templates = PreferNative
```

Or launch the game with `--uikit-demo`. With BepInEx ConfigurationManager installed, `[Demo] Enabled` can be toggled
without restarting.

The demo includes:

- managed widget gallery and scroll view;
- layer probes;
- draggable, closable and fullscreen windows;
- top-bar buttons with ordering and interactability;
- a Settings tab;
- blocks on built-in Settings pages;
- a scoreboard column;
- a Rules civilization when a game tech tree is available.

After entering a match, use **Rebuild** in the demo to recreate managed widgets with native templates.

## Test checklist

Use a real game session; there are no automated in-game tests.

### Main menu

- UiKit loads without plugin errors.
- The demo opens and managed widgets render.
- The top-bar button appears.
- Opening and closing the demo window repeatedly leaves no duplicate windows.
- Settings opens, tabs appear and built-in page blocks can be edited.
- Scroll wheel works in the Settings screen and in a managed `UiScroll`.

### Match

- Scoreboard columns appear when the scoreboard is built/opened.
- A column with no cached values is empty, not missing.
- Updating values with `SetValue`/`SetValues` updates existing rows.
- Disposing/removing a column restores the game row's trail/send-button layout.
- Settings blocks on `Game` and `Lobby` pages still attach after the match transition.
- Rules civilization appears and its tech tree/skin lookup works when supplied.
- Scene transitions do not leave duplicate windows, native buttons or scoreboard cells.

### Lifecycle

- `scope.WhenReady` fires exactly once.
- Disposing a scope removes its managed windows and native handles.
- Reloading/disabling the demo does not leave stale native objects.
- Repeated scene loads do not produce duplicate Settings tabs or scoreboard columns.

## Log expectations

The BepInEx log is usually:

```text
BepInEx/LogOutput.log
```

The Unity player log is separate and is normally under the Wine/Proton prefix's `AppData/LocalLow` directory.

Expected external warning:

```text
[Warning:Il2CppInterop] Class::Init signatures have been exhausted, using a substitute!
```

This comes from the runtime/interop layer, not from UiKit.

UiKit's own diagnostics should be:

- no `TMP_MaterialManager`, `GetPreferredValues` or `Exception in IL2CPP-to-Managed trampoline` errors;
- no `SetAfterMeasure(System.Object)` or `Bind(System.Object, System.Object)` warnings from injected components;
- status messages such as a Settings block waiting for a page are informational, not failures;
- real failures are logged through the `UiGuard` / `UiLog.Error` paths.

### Useful checks

```bash
git diff --check
rg -n "SetAfterMeasure|Bind\\(System.Object|GetPreferredValues|TMP_MaterialManager" \
   /path/to/BepInEx/LogOutput.log
```

## Code map

| Path | What lives there |
|---|---|
| `Api/` | Public entry point, scope, specs, native states, theme and options |
| `Custom/` | Managed widgets and layouts |
| `Custom/Components/` | Injected Unity helpers and registries |
| `Native/` | Game hooks, native host/handles, settings/scoreboard/Rules internals |
| `Runtime/` | Shared assets/templates/layers, layout events, error boundary, staging |
| `Demo/` | Built-in demo controller |
| `Plugin.cs` | BepInEx plugin entry, config and lifecycle |

## Design rules for changes

- All UI and game-object access must stay on the main thread.
- Public factories return `null` on failure; fluent methods do not throw.
- Use `UiGuard` around game callbacks and public entry points.
- Do not add per-frame tickers if a one-shot layout/scene/screen/row event can do the work.
- Keep native additions add-only unless a new API explicitly changes that boundary.
- Keep managed-only types out of injected MonoBehaviour method signatures; use a registry.
- Do not expose internal implementation types through `scope.Custom` or `scope.Native`.
- Preserve stable handles across scene rebuilds; only native views should be recreated.
- When changing Settings/Scoreboard attachment logic, test both menu and match paths.

## Release checklist

1. `dotnet build` Debug and Release with zero warnings.
2. Run the main-menu and match smoke tests above.
3. Check `LogOutput.log` for UiKit errors or unexpected `Il2CppInterop` warnings.
4. Confirm that only one UiKit assembly is in `BepInEx/plugins`.
5. Update the [main README](../README.md) if public behavior changes.
6. Update [NATIVE.md](NATIVE.md) or [ARCHITECTURE.md](ARCHITECTURE.md) if internals or hook behavior changes.
