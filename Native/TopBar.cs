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
    /// Adds an icon from the shared theme to the game's top-right bar. The icon is resolved lazily, so this is safe to call
    /// from plugin <c>Load</c> before the game's assets are available. Returns <c>null</c> only when the scope or the
    /// template policy makes the registration impossible.
    /// </summary>
    public TopBarButton? AddIconButton(string? iconName, string? tooltip, Action? onClick) {
        if (!CanRegister()) return null;
        if (string.IsNullOrWhiteSpace(iconName)) {
            UiLog.OnceError("topbar:no-icon-name", "a top-bar icon name is required");
            return null;
        }

        return UiGuard.Func("topbar.add", () =>
            _scope.Runtime.NativeUi.AddTopBarButton(_scope, iconName.Trim(), null, tooltip, onClick), _scope.Id);
    }

    /// <summary>Adds a caller-supplied sprite to the game's top-right bar.</summary>
    public TopBarButton? AddIconButton(Sprite? icon, string? tooltip, Action? onClick) {
        if (!CanRegister() || icon == null) return null;

        return UiGuard.Func("topbar.add", () =>
            _scope.Runtime.NativeUi.AddTopBarButton(_scope, null, icon, tooltip, onClick), _scope.Id);
    }

    private bool CanRegister() {
        if (!_scope.IsAlive) {
            UiLog.OnceError("topbar:disposed", "cannot add a top-bar button to a disposed scope");
            return false;
        }

        if (!SkinPolicy.AllowNative(_scope.Runtime)) {
            UiLog.OnceError("topbar:managed-only",
                "top-bar buttons require native game templates; TemplatePolicy.ManagedOnly is active");
            return false;
        }

        return true;
    }
}
