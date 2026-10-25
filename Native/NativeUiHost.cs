using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiceModders.UiKit.Native;

/// <summary>
/// Owns registrations for UI that is inserted directly into the game's own screens. The inserted objects are game-owned
/// and global: every mod sees them in the normal game hierarchy, and a scope only owns the right to remove its entries.
/// </summary>
internal sealed class NativeUiHost {
    private readonly List<TopBarButton> _topBar = new();
    private readonly List<SettingsTabHandle> _settings = new();
    private int _topBarIndex;

    public TopBarButton? AddTopBarButton(UiScope scope, string? iconName, Sprite? icon, string? tooltip, Action? click) {
        if (scope == null || !scope.IsAlive) return null;

        var button = UiGuard.Func("topbar.create-handle",
            () => new TopBarButton(scope, this, "DM TopBar " + (_topBarIndex++), iconName, icon, tooltip, click),
            scope.Id);
        if (button == null) return null;

        _topBar.Add(button);
        scope.Owned.Add(button);
        UiGuard.Run("topbar.initial-refresh", () => button.Refresh(GameUi.FindActiveBar()), scope.Id);
        return button;
    }

    public void RemoveTopBarButton(TopBarButton button) {
        if (button != null) _topBar.Remove(button);
    }

    public void AddSettingsTab(SettingsTabHandle tab) {
        if (tab != null) _settings.Add(tab);
    }

    public void RemoveSettingsTab(SettingsTabHandle tab) {
        if (tab != null) _settings.Remove(tab);
    }

    /// <summary>Drops references to objects from the previous scene, then attaches registrations to the new scene's bar.</summary>
    public void Refresh() {
        var bar = UiGuard.Func("native.find-bar", GameUi.FindActiveBar);
        var topBar = _topBar.ToArray();
        for (int i = topBar.Length - 1; i >= 0; i--) {
            var button = topBar[i];
            if (button == null) {
                _topBar.Remove(button);
                continue;
            }
            UiGuard.Run("topbar.refresh", () => button.Refresh(bar), ButtonScope(button));
        }

        var settings = UiGuard.Func("native.find-settings", () => GameUi.FindSettings());
        var tabs = _settings.ToArray();
        for (int i = tabs.Length - 1; i >= 0; i--) {
            var tab = tabs[i];
            if (tab == null) {
                _settings.Remove(tab);
                continue;
            }
            UiGuard.Run("settings.refresh", () => tab.Refresh(settings), tab.Id);
        }
    }

    private static string? ButtonScope(TopBarButton button) {
        try { return button.OwnerId; }
        catch { return null; }
    }

    public void Detach() {
        foreach (var button in _topBar.ToArray()) {
            if (button != null) UiGuard.Run("topbar.detach", button.Detach, ButtonScope(button));
        }
        foreach (var tab in _settings.ToArray()) {
            if (tab != null) UiGuard.Run("settings.detach", tab.DetachForSceneChange, tab.Id);
        }
    }

    public void Dispose() {
        var tabs = _settings.ToArray();
        _settings.Clear();
        for (int i = tabs.Length - 1; i >= 0; i--) {
            var tab = tabs[i];
            if (tab == null) continue;
            UiGuard.Run("settings.dispose", tab.Dispose, tab.Id);
        }

        var buttons = _topBar.ToArray();
        _topBar.Clear();
        for (int i = buttons.Length - 1; i >= 0; i--) {
            var button = buttons[i];
            if (button == null) continue;
            UiGuard.Run("topbar.dispose", button.Dispose, ButtonScope(button));
        }
    }
}
