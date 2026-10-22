using System;
using UnityEngine;

namespace DiceModders.UiKit.Native;

/// <summary>
/// Adds native icon buttons to the game's global top-right bar. Additions are part of the game UI itself: other mods see
/// them as ordinary game buttons. The scope owns their lifetime; the bar and its layout are shared by every scope.
/// </summary>
public sealed class TopBar {
    private readonly UiScope _scope;

    internal TopBar(UiScope scope) => _scope = scope;

    /// <summary>
    /// Adds an icon from the shared theme to the game's top-right bar. The button attaches as soon as a scene with the
    /// game bar is loaded, so this is safe to call from plugin <c>Load</c>.
    /// </summary>
    public TopBarButton AddIconButton(string iconName, string tooltip, Action onClick) {
        if (!CheckScope()) return null;

        var icon = _scope.Custom.Theme.Icon(iconName);
        if (icon == null) {
            UiLog.OnceError("topbar:icon:" + iconName, $"top-bar icon '{iconName}' was not found");
            return null;
        }
        return AddIconButton(icon, tooltip, onClick);
    }

    /// <summary>Adds a caller-supplied sprite to the game's top-right bar.</summary>
    public TopBarButton AddIconButton(Sprite icon, string tooltip, Action onClick) {
        if (!CheckScope() || icon == null) return null;
        if (!SkinPolicy.AllowNative(_scope.Runtime)) {
            UiLog.OnceError("topbar:managed-only", "top-bar buttons require native game templates; TemplatePolicy.ManagedOnly is active");
            return null;
        }

        return _scope.Runtime.NativeUi.AddTopBarButton(_scope, icon, tooltip, onClick);
    }

    private bool CheckScope() {
        if (!_scope.IsDisposed) return true;
        UiLog.OnceError("topbar:disposed", "cannot add a top-bar button to a disposed scope");
        return false;
    }
}
