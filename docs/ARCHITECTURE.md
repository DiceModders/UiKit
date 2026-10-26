# Architecture

This document describes the library's internal architecture. The public API and usage examples are in the
[main README](../README.md). Native game integration details are in [NATIVE.md](NATIVE.md).

## Goals and boundaries

UiKit exists to solve these problems once for every mod:

- one process-wide UI runtime instead of one canvas/asset/hook stack per mod;
- one shared presentation (game font, materials, sprites and native skins);
- one lifecycle for UI that must survive scenes, settings rebuilds and match transitions;
- one error boundary between game callbacks and mod callbacks;
- a public surface that is safe to call from plugin `Load`, `WhenReady` and later main-thread events.

The library is deliberately **add-only** at the native boundary. It inserts mod UI into the game's UI; it does not
edit, move or remove existing game elements. Managed windows are fully owned by the library.

The public boundary is `UiKit`, `UiScope`, `CustomApi`, `NativeApi`, the widget/handle types under `Custom` and the
public handles under `Native`. Everything else is internal.

## Runtime ownership

`Plugin.Load` creates the singleton `UiKitRuntime` through the host-only `UiKit.Initialize` path. A mod must never
create or stop the runtime.

The runtime owns:

- `AssetCatalog` (sprites, materials, fonts);
- `TemplateCatalog` (native widget templates);
- `UiTheme`;
- `LayerCanvases` (shared canvases);
- `NativeUiHost` (native registrations);
- scene subscriptions and the one-shot layout event sources;
- every `UiScope` created by mods.

`UiKit.Shutdown` disposes the runtime. The runtime disposes each scope and its owned widgets/native handles in reverse
creation order. Disposal is best-effort and idempotent: one failed object must not skip the rest.

## Scopes

`UiScope` is the only ownership token a mod creates.

- `scope.Custom` creates managed windows and widgets.
- `scope.Native` creates stable native registrations.
- `scope.WhenReady` queues one-shot startup content.
- `scope.Dispose()` removes owned objects, drops a queued `WhenReady` callback and unregisters from the runtime.

A scope does not create canvases or scan assets. That work is shared by the runtime.

## UI readiness and `WhenReady`

Readiness is a process-level one-time state, not a per-scene or per-match state.

`TryMarkUiReady` requires two consecutive successful checks. A usable UI means:

- a game top bar exists;
- the requested default layer canvas can be created;
- layer orders can be refreshed.

`RequestReadyProbe` schedules one additional check after a layout pass. When ready is reached:

1. `NativeUi.Refresh` discovers the current native targets;
2. queued `WhenReady` callbacks run once in registration order;
3. a callback registered after readiness runs immediately.

`WhenReady` is the only library-guaranteed startup registration point. It is not a match/settings/scoreboard event;
native handles attach later when their target exists.

## Scene lifecycle

The runtime subscribes to `SceneManager.sceneLoaded` and `SceneManager.sceneUnloaded`.

On scene load:

- invalidate asset and template caches;
- refresh shared layer canvases/orders;
- refresh the native host;
- let the demo react;
- run the readiness check/one-shot probe.

On scene unload:

- invalidate caches;
- sweep managed/IL2CPP delegate holders whose Unity owner is gone;
- detach every native registration and clear cached targets;
- request one delayed native refresh. The delayed refresh handles additive or ordered scene transitions where
  `sceneLoaded` for the new scene arrives before `sceneUnloaded` for the old scene.

## One-shot layout work

There is no persistent per-frame retry pump. Deferred work uses two event sources.

### `LayoutOnce`

`LayoutOnce` subscribes to `Canvas.willRenderCanvases` for a single pass, runs pending actions, and unsubscribes.
Callers that can receive repeated events add their own queued flags so the same work is not queued repeatedly:

- `NativeUiHost.RequestRefresh` / `RequestScoreboardRefit`;
- `SettingsPageChrome.RequestRefresh`;
- `NativeUiHost.RequestScoreboardSync`.

`LayoutOnce` is a scheduling primitive, not a frame ticker. It is disabled during runtime disposal.

### `LayoutEvents`

`LayoutEvents` is a shared canvas-size signal. A `CanvasLayoutWatcher` is attached to a root canvas and forwards
`OnRectTransformDimensionsChange` into `LayoutOnce`. The runtime uses it for:

- refitting managed windows after resolution/aspect changes;
- retrying waiting native registrations;
- refitting scoreboard columns and Settings chrome.

## Error boundary

`UiGuard` wraps public entry points and game callbacks:

- `Run` for `Action`;
- `Func`/`Value` for nullable or fallback results;
- `Callback` for user callbacks;
- `Report` for consistent logging.

The error contract is:

- mod callback exceptions are logged and contained;
- factory failures produce `null` where the public API is nullable;
- native attachment failures are represented by handle `State`/`Status`, not exceptions;
- game callback failures in native patches must not escape into the game loop.

`UiLog.Once` suppresses repeated identical diagnostics. `UiLog.Error`/`Warn` remain for real failures.

## Layers and sorting

`LayerCanvases` owns three root canvases:

- `Layer.BehindGame`;
- `Layer.Game`;
- `Layer.AboveGame` (default).

Orders are computed from the active game HUD and other canvases, then copied to the shared layers. Render mode and
canvas scaler settings are copied from the HUD when available. Windows never create their own canvas unless an
explicit `SortingOrder` is set; that path adds a local canvas/raycaster for ordering.

`UiWindow` also handles:

- fixed normal size at the top level;
- fit-to-screen after canvas changes;
- fullscreen layout;
- drag clamping;
- close/fullscreen buttons;
- layer changes and optional sorting-order subscriptions.

## Assets and templates

### `AssetCatalog`

The asset catalog lazily scans `Resources.FindObjectsOfTypeAll` for sprites, materials and `TMP_FontAsset`s. Missing
asset lookups are throttled (a rescan at most every few seconds). Scene changes invalidate the cache.

`UiTheme` is the public read-only facade. Lookups return `null` and never throw.

### `TemplateCatalog`

The template catalog caches live donors for:

- top-bar icon buttons;
- settings label buttons;
- native bool/float settings controls;
- native dropdowns;
- vertical scrollbars.

Each template search is throttled to at most once per second. Unity-null checks revalidate entries after scene changes.
`SkinPolicy` applies the configured `TemplatePolicy`:

- `PreferNative` — native when usable, managed fallback otherwise;
- `ManagedOnly` — no game templates, native handles become `Unsupported`;
- `NativeOnly` — native only, missing templates are reported and widgets are skipped.

Managed widgets and native integrations share these catalogs but do not share ownership of the cloned objects.

## Pointer input

`PointerTracker` is the managed bridge for `UiImage` move and wheel callbacks.

- It subscribes to `InputSystem.onAfterUpdate` only while at least one pointer-aware widget is registered.
- It reads `Mouse.current.position` and `Mouse.current.scroll` directly; it does not use
  `InputAction.CallbackContext`, which Il2CppInterop cannot marshal.
- Entries are removed when their Unity owner is destroyed.
- Move dispatch iterates registered entries; wheel dispatch gives lower-priority entries a chance to consume first and
  falls through to the surrounding `UiScroll`.

`PointerReceiver` is the injected component attached to `UiImage`. It uses Unity's `EventTrigger` for pointer events
because the event-system handler interfaces are exposed as classes by IL2CppInterop. Managed handlers live in
`PointerRegistry`, outside the injected component.

`UiScroll` uses its own `ScrollRect`/`ScrollPin` and a `PointerTracker` entry with wheel priority `1`. An image with an
`OnScroll` handler has priority `0` while hovered.

## Delegate lifetime

`Il2CppDelegates` holds converted managed delegates while a native/Unity owner needs them. `DelegateLifetime` on the
owner's GameObject releases the holder when Unity destroys the object. Scene events also sweep dead owners. This avoids
both premature GC of delegates and permanent static retention.

## Main-thread model

All runtime, native and game-object work runs on Unity's main thread. The library does not implement a dispatcher.
Mods must not call managed UI APIs from worker threads.

## Code map

| Folder | Responsibility |
|---|---|
| `Api/` | Public entry points, scopes, options, native specs/states and theme |
| `Custom/` | Managed windows, containers and widgets |
| `Custom/Components/` | Injected Unity helpers: relays, drag, pointer, width/auto-height, canvas watchers |
| `Native/` | Native game integration, hooks, handles, skins, Settings/scoreboard/Rules internals |
| `Runtime/` | Runtime services: assets, templates, layers, layout events, error boundary, staging |
| `Demo/` | Built-in manual smoke-test UI |
