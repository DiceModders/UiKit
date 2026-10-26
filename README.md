# DiceModders UiKit

A shared UI runtime for Dice Kingdoms mods. One plugin owns the canvases, assets and game hooks; mods only create scopes.

> **Status: P0.5 native layer.** The managed widgets and all four native integrations are in place: top bar, Settings,
> scoreboard columns and Rules civilizations. Native registrations are declarative and lifecycle-managed by the library.
> The public API is nullable-annotated and follows a null/no-throw error contract. Breaking renames from the P0 names have
> been applied without compatibility aliases. Native additions are add-only; editing existing game UI is deferred.
> Startup content uses `scope.WhenReady`; a mod does not need its own UI-ready hook.

## Setup

1. Build the library and copy `DiceModders.UiKit.dll` to `BepInEx/plugins`.
   Only one assembly with this name can be installed: remove any older build (including `UIKit_Another_Version`) first.
2. Reference `DiceModders.UiKit.dll` from your mod.
3. Add a BepInEx dependency on the UiKit plugin.
4. Create exactly one scope in `Load`, register content with `WhenReady`, and dispose the scope in `Unload`.

```csharp
using DiceModders.UiKit;
using DiceModders.UiKit.Custom;

public sealed class MyPlugin : BasePlugin {
    private UiScope? _ui;

    public override void Load() {
        _ui = UiKit.CreateScope("my.mod.id");
        _ui?.WhenReady(ui => {
            ui.Native.TopBar.AddIconButton("hammer", "Open my window", () => ShowMyWindow(ui));
        });
    }

    private static void ShowMyWindow(UiScope ui) {
        var window = ui.Custom.Window(w => {
            w.Title("My window")
             .Size(420, 280)
             .Center()
             .Draggable()
             .Closable();

            w.Body.Paragraph("Everything created through this scope is removed when the scope is disposed.");

            w.Body.Row(row => {
                row.Button("OK", () => { /* ... */ })?.FillWidth();
                row.Button("Cancel", w.Close)?.FillWidth();
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

`UiKit.Initialize`/`Shutdown` are plugin-only. `UiKitRuntime` and `UiOptions` are internal; mods cannot create, replace or
stop the shared runtime, scan assets or patch the game themselves. All UI calls must run on the main thread; there is no
dispatcher.

## Startup registration

`scope.WhenReady(Action<UiScope>)` is the library's single guaranteed UI-readiness registration point. It is a scope
method, not a static event:

* each registered callback runs once after the first ready game UI (normally the main menu, not the first match);
* it fires immediately when the scope registers after the runtime has marked the UI ready;
* callbacks run in registration order on the main thread through the error boundary; one failure does not stop the others;
* disposing the scope drops a callback that has not run yet;
* it does not repeat for a match, scene, tab or screen.

Readiness is a one-time process state, not a per-scene signal: after it, a late registration runs immediately and native
handles deal with the current scene. "Ready" means the runtime has an active game top bar, the shared layer canvases exist
and the native host has been refreshed.
`WhenReady` does not wait for Settings, the scoreboard, the Rules screen or match-only templates; those native handles remain
`Pending` and attach when their screen appears. A callback may run before a match is loaded, so build match-specific windows
lazily or in a later game callback instead of assuming match art exists.

Registering UI from your own Harmony or scene hooks is allowed, but UiKit cannot guarantee the game UI is ready there:
such a hook may run before the canvases, assets or templates exist. `WhenReady` is the one library-provided entry point that
guarantees the first ready game UI. Native handles may still be registered directly from `Load`; they are stable and attach
later.

## Lifecycle and error contract

* The UiKit plugin owns the runtime. A scope is the only lifetime token a mod creates.
* `UiKit.CreateScope(id)` returns `null` when the runtime is unavailable. A null or empty `id` gets a generated diagnostic id.
* Public factories return `null` when the scope/runtime is gone, a required argument is missing, or the object cannot be built.
* Fluent methods no-op for an invalid state and return the same handle. `Dispose` is idempotent.
* User callbacks run through an error boundary. A failing callback is logged and cannot break the game loop or another mod.
* Missing game sprites, fonts or native templates are logged once and degrade to a managed fallback or a `null` widget.
  Library errors never escape as exceptions.
* Native registrations are stable handles. An unavailable target, template or policy is represented by the handle's
  `NativeState`, not by throwing. Calling native APIs from plugin `Load` is safe.
* `scope.WhenReady(Action<UiScope>)` is the one guaranteed startup registration point; see **Startup registration**.
  Registering UI from your own hooks is allowed, but UiKit does not guarantee readiness there.
* The assembly has nullable annotations enabled (`#nullable enable`). Treat factory results and optional properties as
  nullable (`UiScope?`, `UiWindow?`, `UiText?`, `TopBarButtonHandle?`, ...).

## Breaking renames (P0 to P0.5)

The old public type names were removed. There are no compatibility aliases.

### Managed UI

| Old type | New type |
|---|---|
| `Window` | `UiWindow` |
| `Button` | `UiButton` |
| `Text` | `UiText` |
| `Image` | `UiImage` |
| `Container` | `UiContainer` |
| `Element` | `UiElement` |
| `Scroll` | `UiScroll` |
| `Slider` | `UiSlider` |
| `Toggle` | `UiToggle` |
| `Dropdown` | `UiDropdown` |
| `Layout` | `UiLayout` |
| `PointerEvent` / `PointerKind` | `UiPointerEvent` / `UiPointerKind` |

The scope property and factory method names did not change: `scope.Custom.Window`, `Body`, `TitleBar`, `Button`, `Text`,
etc. retain their names. Their return and parameter types now use the `Ui*` names. For example, `CustomApi.Window` returns
`UiWindow?`, `UiWindow.Body` is a `UiContainer`, and `UiWindow.TitleBar` is a `UiContainer?`.

### Native integrations

| Old type | New type |
|---|---|
| `TopBar` | `TopBarApi` |
| `Settings` | `SettingsApi` |
| `TopBarButton` | `TopBarButtonHandle` |
| `SettingsRow` | `SettingsRowHandle` |

The scope properties kept their names: `scope.Native.TopBar` is now a `TopBarApi`, and `scope.Native.Settings` is a
`SettingsApi`. Two new properties expose the native screens: `scope.Native.Scoreboard` (`ScoreboardApi`) and
`scope.Native.Rules` (`RulesApi`).

## Built-in demo

The library can show its own test windows without installing another mod. Enable them in
`BepInEx/config/DiceModders.UiKit.cfg`:

```ini
[Demo]
Enabled = true

[Widgets]
Templates = PreferNative
```

Or launch the game with `--uikit-demo`. With BepInEx ConfigurationManager installed, `[Demo] Enabled` can be toggled
without restarting. The demo contains a scrollable widget gallery, layer probes for `Layer.Game` and `Layer.BehindGame`,
window-ornament switching, a hammer button in the game's top bar, a **UiKit** Settings tab, a scoreboard column, and a
Rules civilization when a game tech tree can be found.

Windows created before the game's UI is loaded use managed fallbacks. The demo registers its top-bar button through
`WhenReady`. Open the demo after entering a match and press **Rebuild (re-scan native skins)** to recreate managed widgets
with native templates. The same actions are available from code:

```csharp
UiKit.ShowDemo();
UiKit.HideDemo();
UiKit.ToggleDemo();
UiKit.RebuildDemo();
```

`Widgets/Templates` accepts `PreferNative`, `ManagedOnly` or `NativeOnly`. An unknown value falls back to
`PreferNative` and is logged.

## Scopes

`UiKit.CreateScope(id)` is the only entry point. A scope owns:

* every managed window and widget created through `scope.Custom`;
* every native handle created through `scope.Native.TopBar`, `Settings`, `Scoreboard` and `Rules`;
* nothing else. Shared canvases, assets and theme belong to the runtime.

Disposing a scope removes all owned objects in reverse creation order; one failing removal does not stop the rest.
Native handles can also be disposed individually. IDs are scope-local diagnostics unless explicitly documented otherwise.

## Layers

| Layer | Meaning |
|---|---|
| `Layer.BehindGame` | Below the game UI (ambient panels, background overlays). |
| `Layer.Game` | At the game HUD's band (the game's popups with higher sorting orders stay above it). |
| `Layer.AboveGame` | Above the game UI. Default for custom windows. |

All three are shared canvases created lazily: at most three per process, no matter how many mods or windows.
`UiWindow.Layer(...)` can move a window at runtime; `UiWindow.SortingOrder(...)` is the per-window escape hatch.

```csharp
w.Layer(Layer.BehindGame);
w.Layer(Layer.AboveGame);
w.SortingOrder(10); // relative to the current layer; recalculated after a layer refresh
```

`SortingOrder` is stored as a layer-relative offset and is recalculated when the shared canvases re-read the game's
sorting order after a scene change. `UiWindow.Layer(...)` remains safe after the window is disposed.

## Windows

`UiWindow` is both the builder and the handle. `CustomApi.Window` returns `null` if the scope is gone or the build callback
fails; a failed window is disposed before returning.

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
    w.Body.Slider(0, 100, 50, value => { /* ... */ })?.Format("0");
    w.Body.Button("Apply", Apply);
});

window?.BringToFront();
window?.Fullscreen();
window?.Restore();
window?.Close();
```

Useful handle methods: `Show`, `Hide`, `Toggle`, `BringToFront`, `Fullscreen`, `Restore`, `Close`, `Dispose`.
`TitleBar` is a nullable `UiContainer` (the title strip is created on demand). `Body` is a `UiContainer`; after the window
is disposed its factory methods return `null`.

`Position(x, y)` is measured from the top-left corner of the layer. `Center()` does the obvious thing.

## Widgets and layout

`UiWindow.Body` is a vertical `UiContainer`. Containers expose:

* `Text`, `Paragraph`
* `Button`, `IconButton`
* `Toggle`, `Slider`, `Dropdown`, `Image`
* `Scroll`
* `Row`, `Column`, `Grid`
* `Spacer`, `Separator`
* `Spacing`, `Padding`, `Columns`, `CellSize`
* `Clear`, `Remove(UiElement?)`

All widgets derive from `UiElement` and can be chained with layout extensions:

```csharp
w.Body.Text("Title")?.Align(TextAlignmentOptions.Center).FillWidth();
w.Body.Paragraph("A long description...")?.Wrap().FillWidth();
w.Body.Button("OK", Close)?.Width(120);
w.Body.Image(texture)?.Height(240).FillWidth();
w.Body.Scroll(list => {
    for (int i = 0; i < 100; i++) list.Text($"Row {i}");
});

w.Body.Row(row => {
    row.Column(sidebar => { /* ... */ })?.FixedW(160f);
    row.Column(content => { /* ... */ })?.WeightW(1f);
})?.FixedH(240f);
```

`Fixed(px)` fixes both dimensions to the same size, while `Fixed(width, height)` does the same with separate values.
`FixedW`/`Width` and `FixedH`/`Height` fix one axis. `Weight`/`WeightW`/`WeightH` give an element a proportional share of
the free space along the main axis of a `Row`, `Column` or `UiWindow.Body`; fixed sizes are reserved first. `Fill*` is
shorthand for weight `1`; a weight of `0` means no flexible share.

Weights do not affect top-level `UiWindow.Size(...)` — windows stay fixed — and grid cells, which keep `CellSize`.
All layout helpers accept a possibly-null element and no-op when it is `null`, so a chain stays type-safe without an extra
check. Widget factories return `null` when the container is disposed or the widget cannot be built.

### Text

* `UiContainer.Text` is one line, cut with `...` when it is too long.
* `UiContainer.Paragraph` wraps and measures its height automatically when its width changes.
* `UiText.Wrap()` converts a text into an auto-height paragraph; `.Value`, `.Align`, `.Color` and `.Size` are chainable.

### Dropdown

The dropdown uses the game's own popup when the native dropdown template is available. With `TemplatePolicy.PreferNative`
(the default) it falls back to a managed compact button that advances to the next option on click. `Index`, `Selected`,
`Count`, `Set`, `Options` and `Interactable` work the same in both modes.

### Native widget skins

`Widgets/Templates` controls how managed widgets are dressed:

| Policy | Behavior |
|---|---|
| `PreferNative` | Clone the game's widget when its template is found; otherwise use the managed fallback. |
| `ManagedOnly` | Never clone game widgets; always use the managed versions. |
| `NativeOnly` | Use only game widgets. If a template is missing, the factory logs once and returns `null`. |

The native skins are real clones of live game controls:

* buttons from the game's label/icon buttons, including hover, pressed, disabled, cursor and sounds;
* toggles from the boolean setting control;
* sliders from the float setting control, including its value label;
* dropdowns from the language setting's `TMP_Dropdown` and popup;
* scrollbars from the game's vertical scroll views.

A managed widget chooses its skin when it is created. Native templates are cached per scene and re-discovered after a
scene change, but an already-created widget is not re-skinned. Build windows in `WhenReady` (or later) to get native skins;
creating them during plugin `Load` normally uses the managed fallback. `WhenReady` still sees only templates available at
the first ready UI, usually the main menu, so open match-specific windows lazily. Use the demo's **Rebuild** action or
`UiKit.RebuildDemo()` to test after entering a match.

Missing templates are logged once and never crash the UI. In `NativeOnly` mode, always null-check managed widget factory
results:

```csharp
var button = w.Body.Button("Apply", Apply);
button?.Width(120);
```

### Images and pointer input

`UiContainer.Image(Texture)` creates a picture with `KeepAspect`, `Filter`, `Refresh`, `SetTexture`, `OnPointer` and
`OnScroll`.

Pointer callbacks use Unity's `EventTrigger` (Il2CppInterop exposes the event-system handler "interfaces" as classes, so
they cannot be implemented directly):

```csharp
image.OnPointer(e => {
    if (e.Kind == UiPointerKind.Drag) { /* e.Button, e.Delta, e.Uv, e.Position */ }
});
```

Supported kinds: `Down`, `Up`, `Click`, `Drag`, `Enter`, `Exit`. `Move` is reserved for a future hover relay.

## Native lifecycle

All four native modules share one registration, lifecycle and state model. The library owns discovery, attachment,
reattachment and retry; a mod registers a description and receives a stable handle.

```csharp
public abstract class NativeHandle : IDisposable {
    public string Id { get; }
    public NativeState State { get; }
    public string? Status { get; }
    public bool IsAttached { get; }
    public bool IsDisposed { get; }
}
```

`NativeState` has six values:

| State | Meaning |
|---|---|
| `Pending` | Waiting for a scene, game screen, asset or template. Never attached yet. |
| `Attached` | Live objects exist in the game's current UI. |
| `Detached` | Attached earlier, now waiting to attach again after a scene/screen change. |
| `Failed` | The last attach attempt failed. The library retries automatically; `Status` says why. |
| `Unsupported` | The active `TemplatePolicy` or the game does not support this registration. |
| `Disposed` | The registration was disposed and can never attach again. |

`Id` is a stable diagnostic id unique inside the owning scope (Rules uses its stable key as the id). `Status` is a
human-readable detail for the current state. A handle is stable for its whole lifetime; scene changes replace only the
game objects behind it.

`Add(...)` returns `null` only when the scope is disposed or the spec cannot be built (for example, a top-bar button
with no icon). An unavailable target, missing template or `ManagedOnly` policy produces a handle in
`Pending`, `Failed` or `Unsupported`; it never throws.

The host retries pending/failed/detached registrations once per second, refreshes scoreboard layout four times per second
while columns exist, and sweeps delegate holders every ten seconds. Until the first game UI is ready, the pump also drives
the one-time readiness check. Scene changes invalidate templates and assets and refresh all native registrations. No mod
needs a dispatcher or its own game hooks merely to keep native registrations attached; `WhenReady` is the only lifecycle
callback for content readiness.

Native additions are inserted into the game's own hierarchy. The player and other mods see ordinary game buttons, tabs,
rows and option entries; the game's layout, sound and hover behavior continue to work. Native integrations require game
templates. With `TemplatePolicy.ManagedOnly` they are marked `Unsupported`; there is no managed fallback because the
objects are part of the game's UI. Each registration is isolated in its own guard: one failure does not stop the others.

### Top bar

```csharp
var button = _ui.Native.TopBar.Add(new TopBarButtonSpec {
    Id = "my.mod.open",
    IconName = "hammer",
    Tooltip = "Open my panel",
    OnClick = TogglePanel,
    InitiallyVisible = true,
});

button?.SetIcon("gear");
button?.SetTooltip("My panel");
button?.SetVisible(true);
button?.SetClick(TogglePanel);
```

`TopBarButtonSpec` fields:

* `Id` — optional diagnostic id.
* `IconName` — icon name in the shared UiKit theme. Resolved lazily.
* `Icon` — caller-supplied sprite. Takes precedence over `IconName`; the name remains as a fallback if the sprite is lost.
* `Tooltip`, `OnClick`, `InitiallyVisible`.

`AddIconButton(name, ...)` and `AddIconButton(Sprite, ...)` are convenience overloads. Both return immediately, so they
are safe in plugin `Load` before sprites exist. `SetIcon(string)` follows the same lazy rule: the new name is cached and
resolved by the retry pump; if the asset is not available while the button is already attached, the old icon stays visible
until the new one can be resolved. Passing a `null`/empty icon name is a no-op. `SetTooltip(null)` clears an existing
tooltip.

A `TopBarButtonHandle` is a clone of the game's bar button, so it keeps the native art, cursor and click sounds. `Dispose`
removes it from the bar. A handle may be returned while `IsAttached` is still false; that is the intended lazy state, not
an error.

### Settings (add-only)

Settings additions are strictly add-only: the API creates new tabs and new rows, never edits or removes built-in settings.

```csharp
var tab = _ui.Native.Settings.Add(new SettingsTabSpec {
    Id = "my.mod",
    Title = "My Mod",
    Build = t => {
        t.AddHeader("My Mod options");
        t.AddToggle("Enabled", true, value => { /* ... */ });
        t.AddSlider("Volume", 0f, 100f, 50f, value => { /* ... */ }, "0");
        t.AddDropdown("Mode", new[] { "Easy", "Normal", "Hard" }, 1, index => { /* ... */ });
        t.AddButton("Apply", "Apply", Apply);
    },
});

tab?.Select();
```

`SettingsTabSpec` fields: `Id`, `Title`, `Build`. `Build` runs synchronously once, immediately after the tab is created and
before it attaches, so rows added there exist by the first attach. `AddTab(id, title, build)` is the direct equivalent.
`SettingsTabHandle` exposes `Title`, `Select`, `AddHeader`, `AddToggle`, `AddSlider`, `AddDropdown` and `AddButton`.

Each `SettingsRowHandle` caches its value and callback while the Settings screen is rebuilt on a scene change; only the
native visual is recreated. Rows can also be added after the tab is attached, in which case their visual is built
immediately. Row handles expose `IsAttached`, `SetValue(bool/float/int)`, `SetEnabled` and `SetVisible`. A null label is
shown as an empty label; a dropdown with no options gets one empty entry.

A tab id is scope-local. Duplicate ids return the existing live tab; a null or empty id gets a generated diagnostic id.
A tab that exists before the Settings screen is loaded is queued and attaches later. `Select()` only acts while a live
tab is attached.

### Scoreboard columns

```csharp
using GameIsland = DiceKingdoms.Game.Island;

var column = _ui.Native.Scoreboard.Add(new ScoreboardColumnSpec {
    Id = "my.mod.score",
    Tooltip = "My score",
});

// Any time your data changes:
column?.SetValue(island, ValueFor(island));
```

`ScoreboardColumnSpec` fields: `Id`, `Tooltip`, `Icon`. `AddColumn(id, tooltip, icon)` is the direct equivalent. The
column is cloned from the game's own stat cell, so it matches the row's art, font and tooltip behavior. It is appended to
every current and future scoreboard row through the library's row-start hook.

Values are pushed explicitly; the library never polls a callback. The cache is keyed by island:

* `SetValue(GameIsland? island, string? value)` — set one value. A null value displays an empty cell.
* `SetValues(IEnumerable<KeyValuePair<GameIsland, string>>? values)` — replace the whole cache. Rows without an entry are
  empty.
* `ClearValues()` — clear the cache and every attached cell.

Values are cached and re-applied when a row is rebuilt or the scene changes, so a mod can set values before the
scoreboard exists and they appear when it does. `AttachedCells` reports how many live rows currently carry the column.
`SetTooltip(...)` and `SetIcon(...)` update existing cells; `SetIcon(null)` hides the donor icon. Tooltip and icon are
stored and reused on new rows. The column handle is a `NativeHandle`: it reattaches across scenes and is removed when the
scope (or the handle) is disposed.

### Rules civilizations

```csharp
var civ = _ui.Native.Rules.Add(new RulesCivilizationSpec {
    Key = "my.mod.civ",              // stable across players and saves
    Name = "My Civilization",
    TechTree = () => _myTechTree,    // required unless the game already knows the civ
    SkinsLike = 0,                   // borrowed biome/visuals: 0 Europe, 1 Middle East, 2 Viking, 3 Ancient, 4 Maya
});
```

`RulesCivilizationSpec` fields: `Key` (required), `Name` (required), `TechTree`, `SkinsLike`. `AddCivilization(key, name,
techTree, skinsLike)` is the direct equivalent. The library appends the civilization to the Rules drop-down and keeps the
option in sync across scene/screen rebuilds.

The numeric `RulesCivilizationHandle.CivilizationId` is derived from `Key` with a stable hash and starts above the
built-in ids. Use `CivilizationId` in saves and multiplayer state; use `Key` as the diagnostic `Id`. The same key always
produces the same number on every machine. Keys are globally unique while registered: registering the same key from
another scope returns `null`.

The library installs the game hooks needed by a custom civilization:

* `Technologies.TechTree(civilization)` is answered by the registered `TechTree` provider. Without a provider, the game's
  own lookup can throw for an unknown civilization, so provide one unless the game knows the id.
* `PlayerSkins.Civilization` borrows the built-in civilization selected by `SkinsLike`, then stores the real custom id.

`SetName(string)` updates the drop-down label while keeping the stable id. `SetTechTree(provider)` replaces the provider.
A civilization registered before the Rules screen exists is `Pending` and attaches when the screen appears. Disposing the
handle (or its scope) permanently removes the option and its matching value. While the handle is alive, a Rules-screen
rebuild detaches the option from the old screen and adds it to the new one automatically.

Do not rely on the exact hash algorithm as a public format; store and compare `CivilizationId` from the handle, or map it
back to your `Key`.

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

Asset scanning is done once by the runtime and invalidated on scene changes. Mods never scan assets. Theme lookups never
throw and return `null` for not-yet-loaded or missing assets; `SpriteNames()` returns an empty sequence rather than
throwing.

## Strings, threads and game updates

* All labels are plain strings. The library does not wrap them in a localization type; mods that need localized text
  should pass the already-localized result.
* Everything is main-thread-only. Callbacks run on Unity's main thread, and the retry/layout pump is a Unity component.
* Scoreboard data is pushed by the mod (`SetValue`/`SetValues`/`ClearValues`); Rules tech-tree and skin lookup are answered
  on demand by the library's hooks. There are no per-frame mod callbacks.
* There are no per-match, per-tab or per-screen callbacks. `WhenReady` fires once, and native handles reattach themselves.
  If you need a game event, use your own hook, but that hook has no UiKit readiness guarantee.
* Native game hooks are best-effort. If a future game update changes a signature, the library logs the failure and keeps
  running; native integrations may remain `Pending`/`Failed` instead of crashing the game.

## What is not here yet

* Editing or removing existing game UI elements. Native additions are add-only for now; the staging and cloning
  primitives are ready for future editing support.
* Automatic reskinning of managed windows that were created before game templates loaded. Recreate the window or use the
  demo's rebuild action.
* A managed fallback for native integrations. `ManagedOnly` produces `Unsupported` native handles.
* A localization wrapper for strings.
* Automated in-game tests; the built-in demo is the manual smoke test. Native hook behavior, scoreboard layout,
  Settings rebuilds and Rules selection still need a real-match pass after game updates.
