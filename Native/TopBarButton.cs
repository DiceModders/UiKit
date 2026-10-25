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

    private string? _iconName;
    private Sprite? _icon;
    private string? _tooltip;
    private Action? _click;
    private bool _visible = true;
    private bool _disposed;
    private bool _missingLogged;
    private bool _missingIconLogged;
    private NativeButton? _button;

    internal TopBarButton(UiScope scope, NativeUiHost host, string name, string? iconName, Sprite? icon,
                          string? tooltip, Action? click) {
        _scope = scope;
        _host = host;
        _name = name;
        _iconName = iconName;
        _icon = icon;
        _tooltip = tooltip;
        _click = click;
    }

    internal string OwnerId => _scope.Id;

    /// <summary>True while the button is present in the current scene's top bar.</summary>
    public bool IsAlive => !_disposed && _button != null && _button.IsAlive;

    /// <summary>
    /// Changes the icon by sprite name. The name is stored even when the asset is not loaded yet and is resolved on the
    /// next scene refresh.
    /// </summary>
    public void SetIcon(string? iconName) {
        if (_disposed) return;
        if (string.IsNullOrWhiteSpace(iconName)) return;

        _iconName = iconName.Trim();
        var icon = ResolveIcon();
        if (icon == null) {
            _missingIconLogged = false;
            return;
        }

        _icon = icon;
        _missingIconLogged = false;
        UiGuard.Run("topbar.set-icon", () => _button?.SetIcon(icon), _scope.Id);
    }

    public void SetIcon(Sprite? icon) {
        if (_disposed || icon == null) return;
        _iconName = null;
        _icon = icon;
        _missingIconLogged = false;
        UiGuard.Run("topbar.set-icon", () => _button?.SetIcon(icon), _scope.Id);
    }

    public void SetTooltip(string? tooltip) {
        if (_disposed) return;
        _tooltip = tooltip;
        UiGuard.Run("topbar.set-tooltip", () => _button?.SetTooltip(tooltip), _scope.Id);
    }

    public void SetClick(Action? click) {
        if (_disposed) return;
        _click = click;
        UiGuard.Run("topbar.set-click", () => _button?.SetClick(click), _scope.Id);
    }

    public void SetVisible(bool visible) {
        if (_disposed) return;
        _visible = visible;
        UiGuard.Run("topbar.set-visible", () => _button?.SetVisible(visible), _scope.Id);
    }

    internal void Refresh(Transform? bar) {
        if (_disposed) return;

        if (bar != null && !string.IsNullOrWhiteSpace(_iconName)) {
            var resolved = ResolveIcon();
            if (resolved != null) {
                _icon = resolved;
                _missingIconLogged = false;
            }
        }

        if (_button != null && _button.IsAlive) {
            if (bar != null && _button.Go.transform.parent == bar) {
                UiGuard.Run("topbar.refresh-existing", () => {
                    if (_icon != null) _button.SetIcon(_icon);
                    _button.SetVisible(_visible);
                }, _scope.Id);
                return;
            }
            Detach();
        }

        if (bar == null) return;
        if (_icon == null) {
            if (!_missingIconLogged) {
                _missingIconLogged = true;
                UiLog.OnceError("topbar:icon:" + _iconName,
                    $"top-bar icon '{_iconName}' was not found; the button will be retried on the next scene");
            }
            return;
        }

        if (!_scope.IsAlive) return;

        _button = UiGuard.Func("topbar.create", () =>
            NativeButton.CreateBarIcon(_scope.Runtime.Templates, bar, _name, _icon, Invoke, _tooltip), _scope.Id);

        if (_button == null) {
            if (!_missingLogged) {
                _missingLogged = true;
                UiLog.OnceError("topbar:no-template",
                    "top-bar button template is unavailable; the button will be retried on the next scene");
            }
            return;
        }

        _missingLogged = false;
        UiGuard.Run("topbar.attach", () => {
            _button.Go.transform.SetAsLastSibling();
            _button.SetVisible(_visible);
        }, _scope.Id);
    }

    internal void Detach() {
        var button = _button;
        _button = null;
        if (button == null) return;
        UiGuard.Run("topbar.detach", button.Destroy, _scope.Id);
    }

    private Sprite? ResolveIcon() {
        if (!_scope.IsAlive || string.IsNullOrWhiteSpace(_iconName)) return null;
        return UiGuard.Func("topbar.resolve-icon",
            () => _scope.Custom.Theme.Icon(_iconName), _scope.Id);
    }

    private void Invoke() {
        try { _click?.Invoke(); }
        catch (Exception e) { UiGuard.Report("topbar.click", e, _scope.Id); }
    }

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;

        Detach();
        UiGuard.Run("topbar.unregister", () => _host.RemoveTopBarButton(this), _scope.Id);
        UiGuard.Run("topbar.untrack", () => _scope.RemoveOwned(this), _scope.Id);
    }
}
