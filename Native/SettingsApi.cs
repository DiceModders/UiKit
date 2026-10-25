using System;
using System.Collections.Generic;

namespace DiceModders.UiKit.Native;

/// <summary>
/// Adds native tabs and rows to the game's Settings screen. Additions are inserted into the game's shared settings
/// hierarchy, so they look like built-in tabs to the player and to other mods.
/// </summary>
public sealed class SettingsApi {
    private readonly UiScope _scope;
    private readonly NativeUiHost _host;
    private readonly Dictionary<string, SettingsTabHandle> _tabs = new();
    private int _nextId;

    internal SettingsApi(UiScope scope) {
        _scope = scope;
        _host = scope.Runtime.NativeUi;
    }

    /// <summary>Adds a tab described by <paramref name="spec"/> and lets it build its rows.</summary>
    public SettingsTabHandle? Add(SettingsTabSpec? spec)
        => spec == null ? null : AddTab(spec.Id, spec.Title, spec.Build);

    /// <summary>
    /// Adds a tab and lets the caller declaratively add its rows. The tab attaches as soon as a scene with the game's
    /// settings UI is available. Tabs are scope-local by id; a null or empty id gets a generated diagnostic id.
    /// </summary>
    public SettingsTabHandle? AddTab(string? id, string? title, Action<SettingsTabHandle>? build = null) {
        if (!_scope.IsAlive) {
            UiLog.OnceError("settings:disposed-scope", "cannot add a settings tab to a disposed scope");
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

        SettingsTabHandle? tab;
        try {
            tab = new SettingsTabHandle(_scope, _host, tabId,
                string.IsNullOrWhiteSpace(title) ? tabId : title!, t => _tabs.Remove(t.Id));
            if (!SkinPolicy.AllowNative(_scope.Runtime)) {
                tab.MarkUnsupported("TemplatePolicy.ManagedOnly disables native settings tabs");
            }
        }
        catch (Exception e) {
            UiGuard.Report("settings.create-tab", e, _scope.Id);
            return null;
        }

        _tabs[tabId] = tab;

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
            return _host.Register(tab);
        }
        catch (Exception e) {
            UiGuard.Report("settings.attach", e, _scope.Id);
            RemoveTab(tabId, tab);
            return null;
        }
    }

    private void RemoveTab(string id, SettingsTabHandle tab) {
        _tabs.Remove(id);
        UiGuard.Run("settings.cleanup", tab.Dispose, _scope.Id);
    }
}
