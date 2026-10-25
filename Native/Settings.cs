using System;
using System.Collections.Generic;

namespace DiceModders.UiKit.Native;

/// <summary>
/// Adds native tabs and rows to the game's Settings screen. Additions are inserted into the game's shared settings hierarchy,
/// so they look like built-in tabs to the player and to other mods. The creating scope owns their lifetime.
/// </summary>
public sealed class Settings {
    private readonly UiScope _scope;
    private readonly Dictionary<string, SettingsTabHandle> _tabs = new();
    private int _nextId;

    internal Settings(UiScope scope) => _scope = scope;

    /// <summary>
    /// Adds a tab and lets the caller declaratively add its rows. The tab attaches as soon as a scene with the game's
    /// settings UI is available. Tabs are scope-local by id; a null or empty id gets a generated diagnostic id.
    /// Returns <c>null</c> when the scope is gone, native templates are disabled, or the build callback fails.
    /// </summary>
    public SettingsTabHandle? AddTab(string? id, string? title, Action<SettingsTabHandle>? build = null) {
        if (!_scope.IsAlive) {
            UiLog.OnceError("settings:disposed-scope", "cannot add a settings tab to a disposed scope");
            return null;
        }
        if (!SkinPolicy.AllowNative(_scope.Runtime)) {
            UiLog.OnceError("settings:managed-only",
                "settings tabs require native game templates; TemplatePolicy.ManagedOnly is active");
            return null;
        }

        string tabId;
        if (string.IsNullOrWhiteSpace(id)) {
            do { tabId = "tab-" + (++_nextId); } while (_tabs.ContainsKey(tabId));
        }
        else {
            tabId = id.Trim();
        }
        if (_tabs.TryGetValue(tabId, out var existing) && !existing.IsDisposed) {
            UiLog.OnceError("settings:duplicate:" + tabId, $"settings tab '{tabId}' already exists in this scope");
            return existing;
        }
        _tabs.Remove(tabId);

        var tab = UiGuard.Func("settings.create-tab",
            () => new SettingsTabHandle(_scope, _scope.Runtime.NativeUi, tabId,
                string.IsNullOrWhiteSpace(title) ? tabId : title, t => _tabs.Remove(t.Id)),
            _scope.Id);
        if (tab == null) return null;

        _tabs[tabId] = tab;
        _scope.Owned.Add(tab);

        try {
            build?.Invoke(tab);
        }
        catch (Exception e) {
            UiGuard.Report("settings.build", e, _scope.Id);
            RemoveTab(tabId, tab);
            return null;
        }

        if (tab.IsDisposed) {
            _tabs.Remove(tabId);
            return null;
        }

        try {
            _scope.Runtime.NativeUi.AddSettingsTab(tab);
            tab.Refresh(GameUi.FindSettings());
        }
        catch (Exception e) {
            UiGuard.Report("settings.attach", e, _scope.Id);
            RemoveTab(tabId, tab);
            return null;
        }

        return tab;
    }

    private void RemoveTab(string id, SettingsTabHandle tab) {
        _tabs.Remove(id);
        UiGuard.Run("settings.cleanup", tab.Dispose, _scope.Id);
    }
}
