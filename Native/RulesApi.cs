using System;
using System.Collections.Generic;
using GameTechTree = DiceKingdoms.Game.TechTree;

namespace DiceModders.UiKit.Native;

/// <summary>
/// Adds civilizations to the game's Rules screen. The library owns the stable ids and the game hooks that make the
/// civilization's tech tree and borrowed biome work.
/// </summary>
public sealed class RulesApi {
    private readonly UiScope _scope;
    private readonly NativeUiHost _host;
    private readonly Dictionary<string, RulesCivilizationHandle> _items = new(StringComparer.Ordinal);

    internal RulesApi(UiScope scope) {
        _scope = scope;
        _host = scope.Runtime.NativeUi;
    }

    /// <summary>Adds a civilization described by <paramref name="spec"/>.</summary>
    public RulesCivilizationHandle? Add(RulesCivilizationSpec? spec)
        => spec == null ? null : AddCivilization(spec.Key, spec.Name, spec.TechTree, spec.SkinsLike);

    /// <summary>
    /// Adds a civilization to the Rules drop-down. The key is stable across machines; the library derives the
    /// civilization number from it, so two mods never need to coordinate ids.
    /// </summary>
    public RulesCivilizationHandle? AddCivilization(string? key, string? name, Func<GameTechTree?>? techTree = null,
                                                     int skinsLike = 0) {
        if (!_scope.IsAlive) {
            UiLog.OnceError("rules:disposed-scope", "cannot add a civilization to a disposed scope");
            return null;
        }
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(name)) {
            UiLog.OnceError("rules:missing-name", "a civilization needs a stable key and a display name");
            return null;
        }

        string stableKey = key.Trim();
        if (_items.TryGetValue(stableKey, out var existing) && !existing.IsDisposed) {
            UiLog.OnceError("rules:duplicate:" + stableKey,
                $"civilization '{stableKey}' already exists in this scope");
            return existing;
        }
        _items.Remove(stableKey);

        if (_host.FindCivilizationByKey(stableKey) is { } other) {
            UiLog.OnceError("rules:duplicate-global:" + stableKey,
                $"civilization '{stableKey}' is already registered by another scope");
            return null;
        }

        int id = _host.AllocateCivilizationId(stableKey);
        RulesCivilizationHandle? handle = null;
        try {
            handle = new RulesCivilizationHandle(_scope, _host, stableKey, id, name.Trim(), techTree, skinsLike);
            if (!SkinPolicy.AllowNative(_scope.Runtime)) {
                handle.MarkUnsupported("TemplatePolicy.ManagedOnly disables native Rules-screen additions");
            }
            _items[stableKey] = handle;
            return _host.Register(handle);
        }
        catch (Exception e) {
            UiGuard.Report("rules.add", e, _scope.Id);
            UiGuard.Run("rules.rollback", () => handle?.Dispose(), _scope.Id);
            _items.Remove(stableKey);
            return null;
        }
    }
}
