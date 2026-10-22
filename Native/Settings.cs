using System;
using System.Collections.Generic;

namespace DiceModders.UiKit.Native;

/// <summary>Tabs of the game's Settings screen.</summary>
public enum SettingsTab { Video, Audio, Input, Game, Lobby }

/// <summary>
/// Adds native tabs and rows to the game's Settings screen. Additions are inserted into the game's shared settings hierarchy,
/// so they look like built-in tabs to the player and to other mods. The creating scope owns their lifetime.
/// </summary>
public sealed class Settings {
    private readonly UiScope _scope;
    private readonly Dictionary<string, SettingsTabHandle> _tabs = new();

    internal Settings(UiScope scope) => _scope = scope;

    /// <summary>
    /// Adds a tab and lets the caller declaratively add its rows. The tab attaches as soon as a scene with the game's
    /// settings UI is available. Tabs are scope-local by id; use a unique id per mod.
    /// </summary>
    public SettingsTabHandle AddTab(string id, string title, Action<SettingsTabHandle> build = null) {
        if (_scope.IsDisposed) {
            UiLog.OnceError("settings:disposed-scope", "cannot add a settings tab to a disposed scope");
            return null;
        }
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A tab id is required.", nameof(id));
        if (!SkinPolicy.AllowNative(_scope.Runtime)) {
            UiLog.OnceError("settings:managed-only", "settings tabs require native game templates; TemplatePolicy.ManagedOnly is active");
            return null;
        }
        if (_tabs.TryGetValue(id, out var existing)) {
            UiLog.OnceError("settings:duplicate:" + id, $"settings tab '{id}' already exists in this scope");
            return existing;
        }

        SettingsTabHandle tab = null;
        tab = new SettingsTabHandle(_scope, _scope.Runtime.NativeUi, id,
            string.IsNullOrWhiteSpace(title) ? id : title, t => _tabs.Remove(t.Id));
        _tabs.Add(id, tab);
        _scope.Owned.Add(tab);

        try {
            build?.Invoke(tab);
        }
        catch (Exception e) {
            UiLog.Error($"settings tab '{id}' build failed: {e}");
            tab.Dispose();
            _tabs.Remove(id);
            return null;
        }

        _scope.Runtime.NativeUi.AddSettingsTab(tab);
        tab.Refresh(GameUi.FindSettings());
        return tab;
    }
}
