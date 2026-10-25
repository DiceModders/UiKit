# DiceModders UiKit

A shared UI runtime for Dice Kingdoms mods. One plugin owns the canvases, the asset cache and the game hooks; mods only create scopes.

> **Status: P0 foundation (phase 3 continues).** Managed windows, native widget skins, top-bar icon buttons and add-only Settings tabs are wired. The runtime lifecycle is host-only, the public API follows a null/error contract, and TopBar icons are resolved lazily. Scoreboard and rules integrations are not part of the public API yet. Breaking changes are still expected before 1.0. A built-in demo makes an in-game smoke test possible without a separate test mod.

## Setup

1. Build the library and copy `DiceModders.UiKit.dll` to `BepInEx/plugins`.
   Only one assembly with this name can be installed: remove any older build (including `UIKit_Another_Version`) first.
2. Reference `DiceModders.UiKit.dll` from your mod.
3. Add a BepInEx dependency on the UiKit plugin.
4. Create one scope in `Load` and dispose it in `Unload`.

```csharp
using DiceModders.UiKit;
using DiceModders.UiKit.Custom;

public sealed class MyPlugin : BasePlugin {
    private UiScope? _ui;

    public override void Load() {
        _ui = UiKit.CreateScope("my.mod.id");
    }

    // Call this after the game UI is loaded: from your own game hook, a top-bar button,
    // or a menu screen callback. Creating windows during plugin Load works, but the game's
    // sprites and font may not be loaded yet and the managed fallback look is used.
    private void ShowMyWindow() {
        if (_ui == null) return;

        var window = _ui.Custom.Window(w => {
            w.Title("My window")
             .Size(420, 280)
             .Center()
             .Draggable()
             .Closable();

            w.Body.Paragraph("Everything created through this scope is removed when the scope is disposed.");

            w.Body.Row(row => {
                row.Button("OK", () => { /* ... */ }).FillWidth();
                row.Button("Cancel", w.Close).FillWidth();
            });
        });

        window?.BringToFront();
    }

    public override bool Unload() {
        _ui?.Dispose();
        return true;
    }
}
```

The UiKit plugin calls its internal `Initialize`/`Shutdown` exactly once. `UiKitRuntime` and `UiOptions` are internal; mods cannot create, replace or stop the shared runtime, scan assets or patch the game themselves.

## Lifecycle and error contract

* The UiKit plugin owns the runtime. A scope is the only lifetime token a mod creates.
* `UiKit.CreateScope(id)` returns `null` when the runtime is unavailable. A null or empty `id` is replaced by a generated diagnostic id.
* Every public factory can return `null` when the scope/runtime is gone or a widget cannot be created.
* Fluent methods never throw for an invalid state. After a scope or widget is disposed they become no-ops and return the same handle; `Dispose` is idempotent.
* User callbacks are wrapped by an error boundary. A failing callback is logged and cannot break the game loop or another mod.
* Missing game sprites, fonts and native templates are logged once and degrade to a managed fallback or a `null` widget; library errors never escape as exceptions.
* Native registrations use the same contract: they may return `null`, but calling them from plugin `Load` is safe and they attach on the next scene where the game UI exists.

The assembly uses nullable annotations for the public surface, so `UiScope?`, `Window?`, `Button?`, `TopBarButton?` and `SettingsTabHandle?` should be treated as nullable. Full nullable warning cleanup is a later task; the project deliberately enables annotations without turning on the complete warning set yet.

## Built-in demo

The library can show its own test windows without installing another mod. Enable them in `BepInEx/config/DiceModders.UiKit.cfg`:

```ini
[Demo]
Enabled = true

[Widgets]
Templates = PreferNative
```

Or launch the game with `--uikit-demo`. With BepInEx ConfigurationManager installed, `[Demo] Enabled` can also be toggled without restarting. The demo contains a scrollable widget gallery, a layer probe for `Layer.Game`, a layer probe for `Layer.BehindGame`, buttons for switching the window ornament, a hammer button in the game's top-right bar, and a **UiKit** tab in the Settings screen.

Windows created before the game's UI is loaded use the managed fallback. Open the demo after entering a match and press **Rebuild (re-scan native skins)** to recreate it with native templates. The same actions are available from code:

```csharp
UiKit.ShowDemo();
UiKit.HideDemo();
UiKit.ToggleDemo();
UiKit.RebuildDemo();
```

`Widgets/Templates` accepts `PreferNative`, `ManagedOnly` or `NativeOnly`, so all three policies can be tested from the config file. An unknown value falls back to `PreferNative` and is logged.

## Scopes

`UiKit.CreateScope(id)` is the only entry point. A scope owns:

* every `Window` created through `scope.Custom.Window(...)`;
* every native registration added through `scope.Native.TopBar` or `scope.Native.Settings`;
* nothing else. The shared canvases, asset cache and theme belong to the runtime.

IDs are scope-local diagnostics. They are not used as game IDs or network hashes. Disposing a scope removes all of its objects in reverse creation order; one failing removal does not stop the rest.

## Layers

| Layer | Meaning |
|---|---|
| `Layer.BehindGame` | Below the game UI (ambient panels, background overlays). |
| `Layer.Game` | At the game HUD's band (the game's popups with higher sorting orders stay above it). |
| `Layer.AboveGame` | Above the game UI. Default for custom windows. |

All three are shared canvases created lazily: at most three per process, no matter how many mods or windows. `Window.Layer(...)` can move a window at runtime; `Window.SortingOrder(...)` is the escape hatch for a per-window nested canvas.

```csharp
w.Layer(Layer.BehindGame);
w.Layer(Layer.AboveGame);
w.SortingOrder(10); // relative to the current layer; updated after a layer refresh
```

`SortingOrder` is stored as a layer-relative offset and is recalculated when the shared canvases re-read the game's sorting order after a scene change. `Window.Layer(...)` remains safe after the window is disposed.

## Windows

`Window` is both the builder and the handle. `CustomApi.Window` returns `null` if the scope is gone or the build callback fails; a failed window is disposed before returning.

```csharp
var window = _ui.Custom.Window(w => {
    w.Title("Settings")
     .Size(480, 360)
     .Center()
     .Draggable()
     .Fullscreenable()
     .Closable()
     .Ornament(OrnamentStyle.BorderWithFourCorners)
     .OnClose(() => { });

    w.Body.Toggle("Enabled", true, value => { /* ... */ });
    w.Body.Slider(0, 100, 50, value => { /* ... */ }).Format("0");
    w.Body.Button("Apply", Apply);
});

window?.BringToFront();
window?.Fullscreen();
window?.Restore();
window?.Close();
```

Useful handle methods: `Show`, `Hide`, `Toggle`, `BringToFront`, `Fullscreen`, `Restore`, `Close`, `Dispose`. `TitleBar` is a nullable `Container` (the title strip is created on demand). `Body` remains a container handle; after the window is disposed its factory methods return `null`.

`Position(x, y)` is measured from the top-left corner of the layer. `Center()` does the obvious thing.

## Widgets and layout

`Window.Body` is a vertical `Container`. Containers expose:

* `Text`, `Paragraph`
* `Button`, `IconButton`
* `Toggle`, `Slider`, `Dropdown`, `Image`
* `Scroll`
* `Row`, `Column`, `Grid`
* `Spacer`, `Separator`
* `Spacing`, `Padding`, `Columns`, `CellSize`
* `Clear`, `Remove(Element)`

All widgets derive from `Element` and can be chained with layout extensions:

```csharp
w.Body.Text("Title").Align(TextAlignmentOptions.Center).FillWidth();
w.Body.Paragraph("A long description...").Wrap().FillWidth();
w.Body.Button("OK", Close).Width(120);
w.Body.Image(texture).Height(240).FillWidth();
w.Body.Scroll(list => {
    for (int i = 0; i < 100; i++) list.Text($"Row {i}");
});
```

`Fixed`, `Width`, `Height`, `Fill`, `FillWidth` and `FillHeight` accept a possibly-null element and no-op when it is `null`, so a chain stays type-safe without an extra check. Widget factories return `null` when the container is disposed or the widget cannot be built.

### Text

* `Text` is one line, cut with `...` when it is too long.
* `Paragraph` wraps and measures its height automatically when its width changes.
* `Wrap()` converts a `Text` into an auto-height paragraph; `.Value`, `.Align`, `.Color` and `.Size` are chainable.

### Dropdown

The dropdown uses the game's own popup when the native dropdown template is available. With `TemplatePolicy.PreferNative` (the default) it falls back to a managed compact button that advances to the next option on click. `Index`, `Selected`, `Count`, `Set`, `Options` and `Interactable` work the same in both modes.

### Native skins

`Widgets/Templates` controls how widgets are dressed:

| Policy | Behavior |
|---|---|
| `PreferNative` | Clone the game's widget when its template is found; otherwise use the managed fallback. |
| `ManagedOnly` | Never clone game widgets; always use the managed versions. |
| `NativeOnly` | Use only game widgets. If a template is missing, the factory logs an error and returns `null`. |

The native skins are real clones of live game controls:

* buttons from the game's label/icon buttons, including hover, pressed, disabled, cursor and sounds; icon buttons keep the game's tooltip;
* toggles from the boolean setting control;
* sliders from the float setting control, including its value label;
* dropdowns from the language setting's `TMP_Dropdown` and popup;
* scrollbars from the game's vertical scroll views.

A widget chooses its skin when it is created. Native templates are cached per scene and re-discovered after a scene change, but an already-created widget is not re-skinned. Create windows after the game UI has loaded to get native skins; creating them during plugin `Load` normally uses the managed fallback.

The template search is deliberately best-effort because the game hierarchy is not a public API. Missing templates are logged once and never crash the UI. In `NativeOnly` mode, always null-check widget factory results:

```csharp
var button = w.Body.Button("Apply", Apply);
button?.Width(120);
```

### Images

`Body.Image(Texture)` creates a picture with `KeepAspect`, `Filter`, `Refresh`, `OnPointer` and `OnScroll`.

Pointer callbacks use Unity's `EventTrigger` (Il2CppInterop exposes the event-system handler "interfaces" as classes, so they cannot be implemented directly):

```csharp
image.OnPointer(e => {
    if (e.Kind == PointerKind.Drag) { /* e.Delta, e.Uv, e.Position */ }
});
```

Supported kinds: `Down`, `Up`, `Click`, `Drag`, `Enter`, `Exit`. `Move` is reserved for a future hover relay.

## Native integrations

Native additions are inserted into the game's own UI hierarchy. They are not separate mod overlays: the player and other mods see ordinary game buttons, tabs and rows, and the game's layout, sounds and hover states continue to work. The creating scope owns their lifetime, but the objects themselves belong to the shared game UI.

Native integrations require game templates. With `TemplatePolicy.ManagedOnly` they log an error and return `null`; with `NativeOnly` or `PreferNative` they attach as soon as a scene with the relevant game UI is available. Registrations survive scene changes and are reattached automatically. Calling these APIs from plugin `Load` is safe.

Each native registration is isolated in its own guard. If one mod's registration fails to attach, the remaining top-bar buttons and settings tabs are still refreshed.

### Top bar

```csharp
var button = _ui.Native.TopBar.AddIconButton("hammer", "Open my panel", TogglePanel);
button?.SetIcon("gear");
button?.SetTooltip("My panel");
button?.SetVisible(true);
button?.SetClick(TogglePanel);
```

`AddIconButton(name, ...)` stores the icon name and resolves it lazily. It is safe in plugin `Load`: the handle is returned immediately even before sprites are loaded, `IsAlive` becomes true when the bar exists and the icon has been resolved, and the icon is retried on scene refresh. `SetIcon(string)` follows the same lazy rule. `AddIconButton(Sprite, ...)` works with a caller-supplied sprite.

A `TopBarButton` is a clone of the game's bar button, so it keeps the native art, cursor and click sounds. `Dispose` removes it from the bar. A handle may be returned while `IsAlive` is still false; this is the intended lazy state, not an error.

### Settings (add-only)

Settings additions are strictly add-only: the API creates new tabs and new rows, never edits or removes built-in settings.

```csharp
var tab = _ui.Native.Settings.AddTab("my.mod", "My Mod", t => {
    t.AddHeader("My Mod options");
    t.AddToggle("Enabled", true, value => { /* ... */ });
    t.AddSlider("Volume", 0f, 100f, 50f, value => { /* ... */ }, "0");
    t.AddDropdown("Mode", new[] { "Easy", "Normal", "Hard" }, 1, index => { /* ... */ });
    t.AddButton("Apply", "Apply", Apply);
});

tab?.Select();
```

Each row keeps its current value and callback across scene changes; only the native visual is rebuilt. `SettingsRow` exposes `SetValue`, `SetEnabled` and `SetVisible`. `SettingsTabHandle.Select()` opens the added tab if the settings screen is available. The tab title and content are real clones of the game's own tabs and setting containers.

A tab id is scope-local. Duplicate ids return the existing live tab; a null or empty id gets a generated diagnostic id. A tab that exists before the Settings screen is loaded is queued and attaches later.

## Theme

`scope.Custom.Theme` is the shared read-only view of the game's look:

```csharp
var theme = _ui.Custom.Theme;

var font = theme.Font;                 // nullable
var material = theme.FontMaterial;     // nullable
var hammer = theme.Icon("hammer");     // nullable
var frame = theme.Ornament(OrnamentStyle.Classic);
var sprite = theme.Sprite("Background-3");
```

Asset scanning is done once by the runtime and invalidated on scene changes. Mods never scan assets. Theme lookups never throw and return `null` for not-yet-loaded or missing assets; `SpriteNames()` returns an empty sequence rather than throwing.

## What is not here yet

* Editing or removing existing game UI elements. Native additions are add-only for now, but the staging and cloning primitives are ready for future editing support.
* Scoreboard callback columns and Rules civilizations. The old public stubs were removed from P0; these will return as designed APIs with their own scopes and guards.
* Renaming the public `Button`/`Text`/`Image`/`Window` types that shadow common Unity names. This is a planned breaking change before 1.0, not part of P0.
* Automated in-game tests and final diagnostics beyond the built-in demo.
