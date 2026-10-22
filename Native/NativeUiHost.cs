using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiceModders.UiKit.Native;

/// <summary>
/// Owns registrations for UI that is inserted directly into the game's own screens. The inserted objects are game-owned
/// and global: every mod sees them in the normal game hierarchy, and a scope only owns the right to remove its entries.
/// </summary>
internal sealed class NativeUiHost {
    private readonly UiKitRuntime _runtime;
    private readonly List<TopBarButton> _topBar = new();
    private readonly List<SettingsTabHandle> _settings = new();
    private int _topBarIndex;

    public NativeUiHost(UiKitRuntime runtime) => _runtime = runtime;

    public TopBarButton AddTopBarButton(UiScope scope, Sprite icon, string tooltip, Action click) {
        var button = new TopBarButton(scope, this, "DM TopBar " + (_topBarIndex++), icon, tooltip, click);
        _topBar.Add(button);
        scope.Owned.Add(button);
        button.Refresh(GameUi.FindActiveBar());
        return button;
    }

    public void RemoveTopBarButton(TopBarButton button) => _topBar.Remove(button);

    public void AddSettingsTab(SettingsTabHandle tab) {
        if (tab != null) _settings.Add(tab);
    }

    public void RemoveSettingsTab(SettingsTabHandle tab) => _settings.Remove(tab);

    /// <summary>Drops references to objects from the previous scene, then attaches registrations to the new scene's bar.</summary>
    public void Refresh() {
        var bar = GameUi.FindActiveBar();
        for (int i = _topBar.Count - 1; i >= 0; i--) {
            if (_topBar[i] == null) {
                _topBar.RemoveAt(i);
                continue;
            }
            _topBar[i].Refresh(bar);
        }

        var settings = GameUi.FindSettings();
        for (int i = _settings.Count - 1; i >= 0; i--) {
            if (_settings[i] == null) {
                _settings.RemoveAt(i);
                continue;
            }
            _settings[i].Refresh(settings);
        }
    }

    public void Detach() {
        foreach (var button in _topBar) button?.Detach();
        foreach (var tab in _settings) tab?.DetachForSceneChange();
    }

    public void Dispose() {
        for (int i = _settings.Count - 1; i >= 0; i--) {
            try { _settings[i]?.Dispose(); }
            catch (Exception e) { UiLog.Error("settings registration dispose failed: " + e); }
        }
        _settings.Clear();

        for (int i = _topBar.Count - 1; i >= 0; i--) {
            try { _topBar[i]?.Dispose(); }
            catch (Exception e) { UiLog.Error("top-bar registration dispose failed: " + e); }
        }
        _topBar.Clear();
    }
}
