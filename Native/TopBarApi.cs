using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiceModders.UiKit.Native;

/// <summary>
/// Adds native icon buttons to the game's global top-right bar. A registration is created immediately and attached as
/// soon as the bar exists, so calling this from plugin <c>Load</c> is safe.
/// </summary>
public sealed class TopBarApi {
    private readonly UiScope _scope;
    private readonly NativeUiHost _host;
    private readonly Dictionary<string, TopBarButtonHandle> _buttons = new();
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
        if (_buttons.TryGetValue(id, out var existing) && !existing.IsDisposed) {
            UiLog.OnceError("topbar:duplicate:" + id, $"top-bar button '{id}' already exists in this scope");
            return existing;
        }
        _buttons.Remove(id);

        TopBarButtonHandle? handle = null;
        try {
            handle = new TopBarButtonHandle(_scope, _host, id, spec.IconName, spec.Icon, spec.Tooltip, spec.OnClick,
                spec.InitiallyVisible, spec.InitiallyInteractable, spec.Order, _host.NextTopBarSequence(),
                value => _buttons.Remove(value.Id));
            if (!SkinPolicy.AllowNative(_scope.Runtime)) {
                handle.MarkUnsupported("TemplatePolicy.ManagedOnly disables native top-bar buttons");
            }
            _buttons[id] = handle;
            return _host.Register(handle);
        }
        catch (Exception e) {
            UiGuard.Report("topbar.add", e, _scope.Id);
            _buttons.Remove(id);
            UiGuard.Run("topbar.rollback", () => handle?.Dispose(), _scope.Id);
            return null;
        }
    }

    /// <summary>Returns a live top-bar button registered in this scope, or <c>null</c>.</summary>
    public TopBarButtonHandle? Get(string? id) {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return _buttons.TryGetValue(id.Trim(), out var button) && !button.IsDisposed ? button : null;
    }

    /// <summary>
    /// Adds an icon from the shared theme. The icon is resolved lazily, so this is safe before the game's assets exist.
    /// Returns <c>null</c> only when the scope is gone or the description cannot be built.
    /// </summary>
    public TopBarButtonHandle? AddIconButton(string? iconName, string? tooltip = null, Action? onClick = null, int order = 0)
        => Add(new TopBarButtonSpec { IconName = iconName, Tooltip = tooltip, OnClick = onClick, Order = order });

    /// <summary>Adds a caller-supplied sprite to the game's top-right bar.</summary>
    public TopBarButtonHandle? AddIconButton(Sprite? icon, string? tooltip = null, Action? onClick = null, int order = 0)
        => Add(new TopBarButtonSpec { Icon = icon, Tooltip = tooltip, OnClick = onClick, Order = order });
}
