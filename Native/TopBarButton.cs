using System;
using UnityEngine;

namespace DiceModders.UiKit.Native;

/// <summary>
/// A handle to an icon button inserted into the game's top-right bar. The button is a real game button clone and is
/// shared with every other mod exactly like a built-in game button; disposing the owning scope removes it again.
/// </summary>
public sealed class TopBarButton : IDisposable {
    private readonly UiScope _scope;
    private readonly NativeUiHost _host;
    private readonly string _name;

    private Sprite _icon;
    private string _tooltip;
    private Action _click;
    private bool _visible = true;
    private bool _disposed;
    private bool _missingLogged;
    private NativeButton _button;

    internal TopBarButton(UiScope scope, NativeUiHost host, string name, Sprite icon, string tooltip, Action click) {
        _scope = scope;
        _host = host;
        _name = name;
        _icon = icon;
        _tooltip = tooltip;
        _click = click;
    }

    /// <summary>True while the button is present in the current scene's top bar.</summary>
    public bool IsAlive => !_disposed && _button != null && _button.IsAlive;

    public void SetIcon(string iconName) {
        if (_disposed) return;
        var icon = _scope.Custom.Theme.Icon(iconName);
        if (icon == null) {
            UiLog.Once("topbar:icon:" + iconName, $"top-bar icon '{iconName}' was not found");
            return;
        }
        SetIcon(icon);
    }

    public void SetIcon(Sprite icon) {
        if (_disposed || icon == null) return;
        _icon = icon;
        _button?.SetIcon(icon);
    }

    public void SetTooltip(string tooltip) {
        if (_disposed) return;
        _tooltip = tooltip;
        _button?.SetTooltip(tooltip);
    }

    public void SetClick(Action click) {
        if (_disposed) return;
        _click = click;
        _button?.SetClick(click);
    }

    public void SetVisible(bool visible) {
        if (_disposed) return;
        _visible = visible;
        _button?.SetVisible(visible);
    }

    internal void Refresh(Transform bar) {
        if (_disposed) return;

        if (_button != null && _button.IsAlive) {
            if (bar != null && _button.Go.transform.parent == bar) {
                _button.SetVisible(_visible);
                return;
            }
            Detach();
        }

        if (bar == null || _icon == null) return;

        _button = NativeButton.CreateBarIcon(_scope.Runtime.Templates, bar, _name, _icon, Invoke, _tooltip);
        if (_button == null) {
            if (!_missingLogged) {
                _missingLogged = true;
                UiLog.OnceError("topbar:no-template", "top-bar button template is unavailable; the button will be retried on the next scene");
            }
            return;
        }

        _missingLogged = false;
        _button.Go.transform.SetAsLastSibling();
        _button.SetVisible(_visible);
    }

    internal void Detach() {
        _button?.Destroy();
        _button = null;
    }

    private void Invoke() {
        try { _click?.Invoke(); }
        catch (Exception e) { UiLog.Error($"top-bar button '{_name}' failed: {e}"); }
    }

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;
        Detach();
        _host.RemoveTopBarButton(this);
        _scope.RemoveOwned(this);
    }
}
