using System;
using DiceModders.UiKit.Custom;

namespace DiceModders.UiKit;

/// <summary>The custom-window half of a scope: windows, widgets and the shared theme.</summary>
public sealed class CustomApi {
    private readonly UiScope _scope;

    internal CustomApi(UiScope scope) => _scope = scope;

    public UiTheme Theme => _scope.Runtime.Theme;

    public Window Window(Action<Window> build = null) {
        if (_scope.IsDisposed) throw new ObjectDisposedException(nameof(UiScope));

        var window = new Window(_scope);
        _scope.Owned.Add(window);

        try {
            build?.Invoke(window);
            window.FinalizeBuild();
            return window;
        }
        catch (Exception e) {
            UiLog.Error($"window build failed: {e}");
            window.Dispose();
            throw;
        }
    }
}
