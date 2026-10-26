using System;
using System.Collections.Generic;

namespace DiceModders.UiKit.Native;

/// <summary>
/// Adds native tabs and rows to the game's Settings screen, and stable row blocks to its built-in pages. Additions are
/// inserted into the game's shared settings hierarchy, so they look like built-in tabs and rows to the player and to other
/// mods.
/// </summary>
public sealed class SettingsApi {
    private readonly UiScope _scope;
    private readonly NativeUiHost _host;
    private readonly Dictionary<string, SettingsTabHandle> _tabs = new();
    private readonly Dictionary<string, SettingsBlockHandle> _blocks = new();
    private int _nextId;
    private int _nextBlockId;

    internal SettingsApi(UiScope scope) {
        _scope = scope;
        _host = scope.Runtime.NativeUi;
    }

    /// <summary>Adds a tab described by <paramref name="spec"/> and lets it build its rows.</summary>
    public SettingsTabHandle? Add(SettingsTabSpec? spec)
        => spec == null ? null : AddTab(spec.Id, spec.Title, spec.Build);

    /// <summary>Adds a block described by <paramref name="spec"/> to one of the game's built-in Settings pages.</summary>
    public SettingsBlockHandle? AddBlock(SettingsBlockSpec? spec) {
        if (spec == null) return null;

        var block = CreateBlock(spec.Id, spec.Page, spec.Title, spec.Order, out bool created);
        if (block == null) return block;
        if (!created) return block;

        if (spec.Build != null) {
            try { spec.Build(block); }
            catch (Exception e) {
                UiGuard.Report("settings.block.build", e, _scope.Id);
                UiGuard.Run("settings.block.build-cleanup", block.Dispose, _scope.Id);
                return null;
            }
        }

        return RegisterBlock(block);
    }

    /// <summary>
    /// Adds rows to one of the game's own Settings pages. The block is stable across scene changes and Settings rebuilds;
    /// rows are appended with the game's own prefabs. The block is scoped to this UiKit scope.
    /// </summary>
    public SettingsBlockHandle? AddBlock(SettingsPage page, string? title = null, int order = 0)
        => AddBlock(null, page, title, order);

    /// <summary>Adds a block with an explicit scope-local id.</summary>
    public SettingsBlockHandle? AddBlock(string? id, SettingsPage page, string? title = null, int order = 0) {
        var block = CreateBlock(id, page, title, order, out bool created);
        return block == null || !created ? block : RegisterBlock(block);
    }

    private SettingsBlockHandle? CreateBlock(string? id, SettingsPage page, string? title, int order, out bool created) {
        created = false;
        if (!_scope.IsAlive) {
            UiLog.OnceError("settings:disposed-scope", "cannot add a settings block to a disposed scope");
            return null;
        }

        string blockId;
        if (string.IsNullOrWhiteSpace(id)) {
            do { blockId = "block-" + (++_nextBlockId); } while (_blocks.ContainsKey(blockId));
        }
        else {
            blockId = id.Trim();
        }

        if (_blocks.TryGetValue(blockId, out var existing) && !existing.IsDisposed) {
            UiLog.OnceError("settings:duplicate-block:" + blockId,
                $"settings block '{blockId}' already exists in this scope");
            return existing;
        }
        _blocks.Remove(blockId);

        SettingsBlockHandle? block;
        try {
            block = new SettingsBlockHandle(_scope, _host, blockId, page, title, order,
                _host.NextSettingsBlockSequence(), value => _blocks.Remove(value.Id));
            if (!SkinPolicy.AllowNative(_scope.Runtime)) {
                block.MarkUnsupported("TemplatePolicy.ManagedOnly disables native Settings pages");
            }
        }
        catch (Exception e) {
            UiGuard.Report("settings.create-block", e, _scope.Id);
            return null;
        }

        _blocks[blockId] = block;
        created = true;
        return block;
    }

    private SettingsBlockHandle? RegisterBlock(SettingsBlockHandle block) {
        if (block == null) return null;
        if (block.IsDisposed) {
            _blocks.Remove(block.Id);
            return null;
        }

        try {
            return _host.Register(block);
        }
        catch (Exception e) {
            UiGuard.Report("settings.attach-block", e, _scope.Id);
            _blocks.Remove(block.Id);
            UiGuard.Run("settings.block-cleanup", block.Dispose, _scope.Id);
            return null;
        }
    }

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
