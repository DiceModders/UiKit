using System;

namespace DiceModders.UiKit;

/// <summary>
/// The library's error boundary. Public entry points and game callbacks run through it so a failure in one mod cannot
/// escape into the game loop or another mod.
/// </summary>
internal static class UiGuard {
    public static void Run(string operation, Action? action, string? scope = null) {
        if (action == null) return;
        try {
            action();
        }
        catch (Exception e) {
            Report(operation, e, scope);
        }
    }

    public static T? Func<T>(string operation, Func<T>? action, string? scope = null) where T : class? {
        if (action == null) return null;
        try {
            return action();
        }
        catch (Exception e) {
            Report(operation, e, scope);
            return null;
        }
    }

    public static T Value<T>(string operation, Func<T>? action, T fallback, string? scope = null) {
        if (action == null) return fallback;
        try {
            return action();
        }
        catch (Exception e) {
            Report(operation, e, scope);
            return fallback;
        }
    }

    /// <summary>Runs a user callback without letting it escape into Unity's event system.</summary>
    public static void Callback(string operation, Action? callback) {
        if (callback == null) return;
        try {
            callback();
        }
        catch (Exception e) {
            Report("callback." + operation, e);
        }
    }

    public static void Report(string operation, Exception exception, string? scope = null) {
        string prefix = string.IsNullOrWhiteSpace(scope) ? "uikit" : "uikit[" + scope + "]";
        try {
            UiLog.OnceError("guard:" + prefix + ":" + operation, $"{prefix}: {operation} failed: {exception}");
        }
        catch {
            // Logging must never be the reason a UI call fails.
        }
    }

    public static string Text(string? value) => value ?? string.Empty;

    public static float Finite(float value, float fallback) =>
        float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
}
