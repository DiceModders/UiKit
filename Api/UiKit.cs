using System;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace DiceModders.UiKit;

/// <summary>
/// The single entry point of the library. The UiKit plugin calls <see cref="Initialize"/> once; each mod then calls
/// <see cref="CreateScope"/> and works through <c>scope.Custom</c> and <c>scope.Native</c>.
/// </summary>
public static class UiKit {
    private static UiKitRuntime _runtime;

    public static bool IsInitialized => _runtime != null;

    public static UiKitRuntime Initialize(BasePlugin plugin, UiOptions options = null)
        => Initialize(plugin != null ? plugin.Log : null, options);

    public static UiKitRuntime Initialize(ManualLogSource log, UiOptions options = null) {
        if (_runtime != null) {
            UiLog.Warn("UiKit.Initialize called twice; keeping the existing runtime");
            return _runtime;
        }

        _runtime = new UiKitRuntime(log, options);
        return _runtime;
    }

    public static UiScope CreateScope(string id) {
        var runtime = _runtime;
        if (runtime == null)
            throw new InvalidOperationException("UiKit is not initialized. The UiKit plugin must load before the mod calls CreateScope.");

        return runtime.CreateScope(id);
    }

    /// <summary>Shows the library's built-in test windows without installing another mod. Also available through the plugin config.</summary>
    public static void ShowDemo() => Runtime().ShowDemo();

    public static void HideDemo() => Runtime().HideDemo();

    public static void ToggleDemo() => Runtime().ToggleDemo();

    /// <summary>Recreates the built-in test windows, for example after entering a match where native templates are available.</summary>
    public static void RebuildDemo() => Runtime().RebuildDemo();

    public static bool IsDemoVisible => _runtime != null && _runtime.IsDemoVisible;

    public static void Shutdown() {
        var runtime = _runtime;
        _runtime = null;
        runtime?.Dispose();
    }

    private static UiKitRuntime Runtime() {
        var runtime = _runtime;
        if (runtime == null)
            throw new InvalidOperationException("UiKit is not initialized. The UiKit plugin must load first.");
        return runtime;
    }
}
