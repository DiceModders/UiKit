using System;
using DiceModders.UiKit.Custom;

namespace DiceModders.UiKit;

/// <summary>The custom-window half of a scope: windows, widgets and the shared theme.</summary>
public sealed class CustomApi {
    private readonly UiScope _scope;

    internal CustomApi(UiScope scope) => _scope = scope;

    public UiTheme Theme => _scope.Runtime.Theme;

    /// <summary>
    /// Creates a window and lets the caller build its content. Returns <c>null</c> when the scope is gone or the build
    /// callback fails; a failed window is disposed before returning, so no half-built window leaks.
    /// </summary>
    public UiWindow? Window(Action<UiWindow>? build = null) {
        if (!_scope.IsAlive) {
            UiLog.OnceError("custom:disposed-scope", "cannot create a window in a disposed scope");
            return null;
        }

        var window = UiGuard.Func("window.create", () => new UiWindow(_scope), _scope.Id);
        if (window == null) return null;

        _scope.Owned.Add(window);

        try {
            build?.Invoke(window);
            window.FinalizeBuild();
            if (window.IsAlive) return window;

            UiGuard.Run("window.cleanup", window.Dispose, _scope.Id);
            return null;
        }
        catch (Exception e) {
            UiGuard.Report("window.build", e, _scope.Id);
            UiGuard.Run("window.cleanup", window.Dispose, _scope.Id);
            return null;
        }
    }
}
