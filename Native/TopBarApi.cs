using System;
using UnityEngine;

namespace DiceModders.UiKit.Native;

/// <summary>
/// Adds native icon buttons to the game's global top-right bar. A registration is created immediately and attached as
/// soon as the bar exists, so calling this from plugin <c>Load</c> is safe.
/// </summary>
public sealed class TopBarApi {
    private readonly UiScope _scope;
    private readonly NativeUiHost _host;
    private int _index;

    internal TopBarApi(UiScope scope) {
        _scope = scope;
        _host = scope.Runtime.NativeUi;
    }

    /// <summary>Adds a button described by <paramref name="spec"/>.</summary>
    public TopBarButtonHandle? Add(TopBarButtonSpec? spec) {
        if (!_scope.IsAlive) {
            UiLog.OnceError("topbar:disposed", "cannot add a top-bar button to a disposed scope");
            return null;
        }
        if (spec == null || (string.IsNullOrWhiteSpace(spec.IconName) && spec.Icon == null)) {
            UiLog.OnceError("topbar:no-icon", "a top-bar button needs an icon name or a sprite");
            return null;
        }

        string id = string.IsNullOrWhiteSpace(spec.Id) ? "topbar-" + (++_index) : spec.Id.Trim();
        TopBarButtonHandle? handle = null;
        try {
            handle = new TopBarButtonHandle(_scope, _host, id, spec.IconName, spec.Icon, spec.Tooltip, spec.OnClick,
                spec.InitiallyVisible);
            if (!SkinPolicy.AllowNative(_scope.Runtime)) {
                handle.MarkUnsupported("TemplatePolicy.ManagedOnly disables native top-bar buttons");
            }
            return _host.Register(handle);
        }
        catch (Exception e) {
            UiGuard.Report("topbar.add", e, _scope.Id);
            UiGuard.Run("topbar.rollback", () => handle?.Dispose(), _scope.Id);
            return null;
        }
    }

    /// <summary>
    /// Adds an icon from the shared theme. The icon is resolved lazily, so this is safe before the game's assets exist.
    /// Returns <c>null</c> only when the scope is gone or the description cannot be built.
    /// </summary>
    public TopBarButtonHandle? AddIconButton(string? iconName, string? tooltip = null, Action? onClick = null)
        => Add(new TopBarButtonSpec { IconName = iconName, Tooltip = tooltip, OnClick = onClick });

    /// <summary>Adds a caller-supplied sprite to the game's top-right bar.</summary>
    public TopBarButtonHandle? AddIconButton(Sprite? icon, string? tooltip = null, Action? onClick = null)
        => Add(new TopBarButtonSpec { Icon = icon, Tooltip = tooltip, OnClick = onClick });
}
