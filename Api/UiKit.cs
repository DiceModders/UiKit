using System;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace DiceModders.UiKit;

/// <summary>
/// The single public entry point of the library. The UiKit plugin owns initialization and shutdown; a mod only calls
/// <see cref="CreateScope"/> and works through <c>scope.Custom</c> and <c>scope.Native</c>.
/// </summary>
public static class UiKit {
    private static UiKitRuntime? _runtime;

    public static bool IsInitialized => _runtime != null;

    /// <summary>Host-only. Called by the UiKit plugin; mods must not create or stop the shared runtime.</summary>
    internal static UiKitRuntime? Initialize(BasePlugin? plugin, UiOptions? options = null)
        => Initialize(plugin != null ? plugin.Log : null, options);

    /// <summary>Host-only. Called by the UiKit plugin; mods must not create or stop the shared runtime.</summary>
    internal static UiKitRuntime? Initialize(ManualLogSource? log, UiOptions? options = null) {
        if (_runtime != null) {
            UiLog.Warn("UiKit.Initialize called twice; keeping the existing runtime");
            return _runtime;
        }

        UiLog.SetLogger(log);
        UiLog.ResetOnce();

        try {
            _runtime = new UiKitRuntime(log, options);
            return _runtime;
        }
        catch (Exception e) {
            _runtime = null;
            UiLog.Error("UiKit initialization failed: " + e);
            return null;
        }
    }

    /// <summary>
    /// Creates a mod-owned scope. Returns <c>null</c> when the runtime is not available; a null or empty
    /// <paramref name="id"/> is replaced by a generated diagnostic id.
    /// </summary>
    public static UiScope? CreateScope(string? id = null) {
        var runtime = _runtime;
        if (runtime == null) {
            UiLog.OnceError("uikit:not-initialized",
                "UiKit.CreateScope called before the UiKit plugin initialized the runtime; returning null");
            return null;
        }

        return UiGuard.Func("scope.create", () => runtime.CreateScope(id), "runtime");
    }

    public static void ShowDemo() => RunDemo("demo.show", runtime => runtime.ShowDemo());

    public static void HideDemo() => RunDemo("demo.hide", runtime => runtime.HideDemo());

    public static void ToggleDemo() => RunDemo("demo.toggle", runtime => runtime.ToggleDemo());

    /// <summary>Recreates the built-in test windows, for example after entering a match where native templates are available.</summary>
    public static void RebuildDemo() => RunDemo("demo.rebuild", runtime => runtime.RebuildDemo());

    public static bool IsDemoVisible {
        get {
            var runtime = _runtime;
            return runtime != null && UiGuard.Value("demo.visible", () => runtime.IsDemoVisible, false, "runtime");
        }
    }

    /// <summary>Host-only. Stops the shared runtime and disposes every mod scope.</summary>
    internal static void Shutdown() {
        var runtime = _runtime;
        _runtime = null;
        if (runtime != null) UiGuard.Run("runtime.shutdown", runtime.Dispose, "runtime");
    }

    private static void RunDemo(string operation, Action<UiKitRuntime> action) {
        var runtime = _runtime;
        if (runtime == null) {
            UiLog.OnceError("uikit:not-initialized", "the UiKit runtime is not available; demo command ignored");
            return;
        }

        UiGuard.Run(operation, () => action(runtime), "runtime");
    }
}
