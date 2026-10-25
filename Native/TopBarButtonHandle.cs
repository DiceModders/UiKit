using System;
using UnityEngine;

namespace DiceModders.UiKit.Native;

/// <summary>
/// A stable handle to an icon button in the game's top-right bar. The native button is attached lazily and rebuilt after
/// scene changes; disposing the owning scope removes it for good.
/// </summary>
public sealed class TopBarButtonHandle : NativeHandle {
    private string? _iconName;
    private Sprite? _icon;
    private string? _tooltip;
    private Action? _click;
    private bool _visible = true;
    private bool _missingLogged;
    private bool _missingIconLogged;
    private NativeButton? _button;

    internal TopBarButtonHandle(UiScope scope, NativeUiHost host, string id, string? iconName, Sprite? icon,
                                string? tooltip, Action? click, bool visible)
        : base(scope, host, id) {
        _iconName = string.IsNullOrWhiteSpace(iconName) ? null : iconName.Trim();
        _icon = icon;
        _tooltip = tooltip;
        _click = click;
        _visible = visible;
    }

    /// <summary>
    /// Changes the icon by sprite name. The name is kept even when the asset is not loaded yet and is resolved on the
    /// next attach attempt.
    /// </summary>
    public void SetIcon(string? iconName) {
        if (IsDisposed || string.IsNullOrWhiteSpace(iconName)) return;

        _iconName = iconName.Trim();
        _missingIconLogged = false;
        var icon = ResolveIcon();
        if (icon == null) {
            _icon = null;
            if (State == NativeState.Attached) MarkFailed($"icon '{_iconName}' is not available yet");
            return;
        }

        _icon = icon;
        UiGuard.Run("topbar.set-icon", () => _button?.SetIcon(icon), OwnerId);
    }

    public void SetIcon(Sprite? icon) {
        if (IsDisposed || icon == null) return;
        _iconName = null;
        _icon = icon;
        _missingIconLogged = false;
        UiGuard.Run("topbar.set-icon", () => _button?.SetIcon(icon), OwnerId);
    }

    public void SetTooltip(string? tooltip) {
        if (IsDisposed) return;
        _tooltip = tooltip;
        UiGuard.Run("topbar.set-tooltip", () => _button?.SetTooltip(tooltip ?? string.Empty), OwnerId);
    }

    public void SetClick(Action? click) {
        if (IsDisposed) return;
        _click = click;
        UiGuard.Run("topbar.set-click", () => _button?.SetClick(click), OwnerId);
    }

    public void SetVisible(bool visible) {
        if (IsDisposed) return;
        _visible = visible;
        UiGuard.Run("topbar.set-visible", () => _button?.SetVisible(visible), OwnerId);
    }

    internal override void Refresh(NativeScene scene) {
        if (IsDisposed || State == NativeState.Unsupported) return;

        var bar = scene.TopBar;

        if (_button != null && !_button.IsAlive) DetachCore();

        var existingGo = _button != null ? _button.Go : null;
        if (_button != null && existingGo != null) {
            if (bar != null && existingGo.transform.parent == bar) {
                if (_icon == null && _iconName != null) {
                    var resolved = ResolveIcon();
                    if (resolved != null) {
                        _icon = resolved;
                        _missingIconLogged = false;
                    }
                    else {
                        MarkFailed($"icon '{_iconName}' is not available yet");
                        if (!_missingIconLogged) {
                            _missingIconLogged = true;
                            UiLog.OnceError("topbar:icon:" + Id + ":" + _iconName,
                                $"top-bar icon '{_iconName}' was not found; the button will be retried");
                        }
                    }
                }

                if (_icon != null) MarkAttached("top bar");
                UiGuard.Run("topbar.refresh-existing", () => {
                    if (_icon != null) _button.SetIcon(_icon);
                    _button.SetTooltip(_tooltip ?? string.Empty);
                    _button.SetVisible(_visible);
                }, OwnerId);
                return;
            }
            DetachCore();
        }

        if (bar == null) {
            MarkWaiting("waiting for the game's top bar");
            return;
        }

        if (_icon == null && _iconName != null) {
            var resolved = ResolveIcon();
            if (resolved != null) {
                _icon = resolved;
                _missingIconLogged = false;
            }
        }

        if (_icon == null) {
            MarkFailed(_iconName == null
                ? "no icon was supplied"
                : $"icon '{_iconName}' was not found");
            if (!_missingIconLogged) {
                _missingIconLogged = true;
                UiLog.OnceError("topbar:icon:" + Id + ":" + _iconName,
                    _iconName == null
                        ? "a top-bar button has no icon; it will be retried"
                        : $"top-bar icon '{_iconName}' was not found; the button will be retried");
            }
            return;
        }

        var created = UiGuard.Func("topbar.create", () =>
            NativeButton.CreateBarIcon(Scope.Runtime.Templates, bar, "DM Top Bar " + Id, _icon, Invoke, _tooltip),
            OwnerId);
        if (created == null) {
            MarkFailed("top-bar button template is unavailable");
            if (!_missingLogged) {
                _missingLogged = true;
                UiLog.OnceError("topbar:no-template:" + Id,
                    "top-bar button template is unavailable; the button will be retried");
            }
            return;
        }

        _button = created;
        _missingLogged = false;
        UiGuard.Run("topbar.attach", () => {
            var go = _button != null ? _button.Go : null;
            if (go != null) go.transform.SetAsLastSibling();
            _button?.SetVisible(_visible);
        }, OwnerId);
        MarkAttached("top bar");
    }

    protected override void DetachCore() {
        var button = _button;
        _button = null;
        if (button == null) return;
        UiGuard.Run("topbar.detach", button.Destroy, OwnerId);
    }

    private Sprite? ResolveIcon() {
        if (!Scope.IsAlive || string.IsNullOrWhiteSpace(_iconName)) return null;
        return UiGuard.Func("topbar.resolve-icon", () => Scope.Custom.Theme.Icon(_iconName), OwnerId);
    }

    private void Invoke() {
        try { _click?.Invoke(); }
        catch (Exception e) { UiGuard.Report("topbar.click", e, OwnerId); }
    }
}
