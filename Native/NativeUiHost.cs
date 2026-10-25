using System;
using System.Collections.Generic;
using UnityEngine;
using GameRules = DiceKingdoms.Meta.Rules;
using GameScoreboardRow = DiceKingdoms.Interface.ScoreboardRow;
using GameSettings = DiceKingdoms.Meta.SettingSetsInterface;
using GameTechTree = DiceKingdoms.Game.TechTree;

namespace DiceModders.UiKit.Native;

/// <summary>
/// Owns every registration that inserts objects into the game's own UI. Module APIs create stable handles; this host
/// decides when they are attached, reattached or retired as scenes and game screens change.
/// </summary>
internal sealed class NativeUiHost {
    private readonly List<NativeHandle> _bindings = new();
    private readonly List<GameScoreboardRow> _scoreboardRows = new();
    private GameSettings? _settingsScreen;
    private GameRules? _rulesScreen;
    private bool _disposed;

    internal bool HasRegistrations => _bindings.Count > 0;

    internal bool HasPending {
        get {
            for (int i = 0; i < _bindings.Count; i++) {
                var state = _bindings[i].State;
                if (state is NativeState.Pending or NativeState.Failed or NativeState.Detached) return true;
            }
            return false;
        }
    }

    internal bool HasScoreboardColumns {
        get {
            for (int i = 0; i < _bindings.Count; i++) {
                if (_bindings[i] is ScoreboardColumnHandle column &&
                    !column.IsDisposed && column.State != NativeState.Unsupported) return true;
            }
            return false;
        }
    }

    /// <summary>Registers a stable handle and makes the first attach attempt immediately.</summary>
    internal T Register<T>(T handle) where T : NativeHandle {
        if (handle == null) throw new ArgumentNullException(nameof(handle));
        if (_disposed) {
            handle.MarkUnsupported("UiKit is shutting down");
            return handle;
        }
        if (!_bindings.Contains(handle)) {
            _bindings.Add(handle);
            try { handle.Scope.Owned.Add(handle); }
            catch (Exception e) { UiLog.OnceError("native:register", "could not track a native registration: " + e); }
        }

        UiGuard.Run("native.register", () => RefreshOne(handle, CaptureScene()), handle.OwnerId);
        return handle;
    }

    internal void Unregister(NativeHandle handle) {
        if (handle != null) _bindings.Remove(handle);
    }

    /// <summary>Scene load: forget old targets, discover the new scene and attach every registration.</summary>
    internal void Refresh() {
        if (_disposed) return;

        _settingsScreen = UiGuard.Func("native.find-settings", GameUi.FindSettings);
        _rulesScreen = UiGuard.Func("native.find-rules", GameUi.FindRules);
        _scoreboardRows.Clear();
        var rows = UiGuard.Func("native.find-scoreboard", GameUi.FindScoreboardRows);
        if (rows != null) _scoreboardRows.AddRange(rows);
        RefreshAll();
    }

    /// <summary>Scene unload: drop live objects but keep every registration and its cached data.</summary>
    internal void Detach() {
        var bindings = _bindings.ToArray();
        for (int i = 0; i < bindings.Length; i++) {
            var handle = bindings[i];
            if (handle == null) continue;
            UiGuard.Run("native.detach", () => handle.Detach(NativeDetachReason.SceneChanged), handle.OwnerId);
        }

        _scoreboardRows.Clear();
        _settingsScreen = null;
        _rulesScreen = null;
    }

    /// <summary>Retry pump: tries registrations that failed or were waiting for a target.</summary>
    internal void Retry() {
        if (_disposed || _bindings.Count == 0) return;
        RefreshAll();
    }

    /// <summary>Refreshes all scoreboard cells and keeps the board within the screen.</summary>
    internal void TickScoreboard() {
        if (_disposed) return;

        int cells = 0;
        Transform? sample = null;
        var bindings = _bindings.ToArray();
        for (int i = 0; i < bindings.Length; i++) {
            if (bindings[i] is not ScoreboardColumnHandle column || column.IsDisposed ||
                column.State == NativeState.Unsupported) continue;
            cells += UiGuard.Value("scoreboard.tick", column.TickCells, 0, column.OwnerId);
            sample ??= UiGuard.Func("scoreboard.sample", () => column.FirstCell, column.OwnerId);
        }

        UiGuard.Run("scoreboard.fit", () => ScoreboardLayout.FitBoard(cells, sample), "runtime");
    }

    internal void OnSettingsScreen(GameSettings? settings) {
        if (_disposed || settings == null) return;
        _settingsScreen = settings;
        RefreshMatching(handle => handle is SettingsTabHandle);
    }

    internal void OnScoreboardRowReady(GameScoreboardRow? row) {
        if (_disposed || row == null) return;
        if (!_scoreboardRows.Contains(row)) _scoreboardRows.Add(row);
        RefreshMatching(handle => handle is ScoreboardColumnHandle);
    }

    internal void OnScoreboardRowDestroyed(GameScoreboardRow? row) {
        if (row == null) return;
        _scoreboardRows.Remove(row);
        RefreshMatching(handle => handle is ScoreboardColumnHandle);
    }

    internal void OnRulesScreen(GameRules? rules) {
        if (_disposed || rules == null) return;
        _rulesScreen = rules;
        RefreshMatching(handle => handle is RulesCivilizationHandle);
    }

    internal RulesCivilizationHandle? FindCivilizationByKey(string key) {
        for (int i = 0; i < _bindings.Count; i++) {
            if (_bindings[i] is RulesCivilizationHandle civ && !civ.IsDisposed &&
                string.Equals(civ.Key, key, StringComparison.Ordinal)) return civ;
        }
        return null;
    }

    /// <summary>Stable id from a key, advanced past ids used by other keys.</summary>
    internal int AllocateCivilizationId(string key) {
        uint hash = 2166136261;
        for (int i = 0; i < key.Length; i++) {
            hash ^= key[i];
            hash *= 16777619;
        }

        int id = 1000 + (int)(hash % 1_000_000_000u);
        while (FindCivilizationById(id) is { } taken && !string.Equals(taken.Key, key, StringComparison.Ordinal)) {
            id++;
        }
        return id;
    }

    internal bool TryGetRulesTree(int id, out GameTechTree? tree) {
        tree = null;
        for (int i = 0; i < _bindings.Count; i++) {
            if (_bindings[i] is not RulesCivilizationHandle civ || civ.IsDisposed || civ.CivilizationId != id) continue;
            if (civ.TryGetTree(out var candidate) && candidate != null) {
                tree = candidate;
                return true;
            }
        }
        return false;
    }

    internal bool TryGetSkinsLike(int id, out int skinsLike) {
        for (int i = 0; i < _bindings.Count; i++) {
            if (_bindings[i] is not RulesCivilizationHandle civ || civ.IsDisposed || civ.CivilizationId != id) continue;
            skinsLike = civ.SkinsLike;
            return true;
        }
        skinsLike = 0;
        return false;
    }

    private RulesCivilizationHandle? FindCivilizationById(int id) {
        for (int i = 0; i < _bindings.Count; i++) {
            if (_bindings[i] is RulesCivilizationHandle civ && !civ.IsDisposed && civ.CivilizationId == id) return civ;
        }
        return null;
    }

    private void RefreshAll() {
        if (_disposed) return;
        var scene = UiGuard.Func("native.capture", CaptureScene);
        if (scene == null) return;
        RefreshMatching(_ => true, scene);
    }

    private void RefreshMatching(Func<NativeHandle, bool> predicate) {
        var scene = UiGuard.Func("native.capture", CaptureScene);
        if (scene == null) return;
        RefreshMatching(predicate, scene);
    }

    private void RefreshMatching(Func<NativeHandle, bool> predicate, NativeScene scene) {
        var bindings = _bindings.ToArray();
        for (int i = 0; i < bindings.Length; i++) {
            var handle = bindings[i];
            if (handle == null || handle.IsDisposed) continue;
            if (!UiGuard.Value("native.match", () => predicate(handle), false, handle.OwnerId)) continue;
            RefreshOne(handle, scene);
        }
    }

    private static void RefreshOne(NativeHandle handle, NativeScene scene) {
        UiGuard.Run("native.refresh", () => handle.Refresh(scene), handle.OwnerId);
    }

    private NativeScene CaptureScene() {
        PruneTargets();

        if (_scoreboardRows.Count == 0) {
            var rows = UiGuard.Func("native.find-scoreboard", GameUi.FindScoreboardRows);
            if (rows != null) _scoreboardRows.AddRange(rows);
        }
        if (_rulesScreen == null) _rulesScreen = UiGuard.Func("native.find-rules", GameUi.FindRules);
        if (_settingsScreen == null) _settingsScreen = UiGuard.Func("native.find-settings", GameUi.FindSettings);

        return new NativeScene {
            TopBar = UiGuard.Func("native.find-bar", GameUi.FindActiveBar),
            Settings = _settingsScreen,
            ScoreboardRows = _scoreboardRows.ToArray(),
            RulesScreen = _rulesScreen,
        };
    }

    private void PruneTargets() {
        for (int i = _scoreboardRows.Count - 1; i >= 0; i--) {
            if (_scoreboardRows[i] == null) _scoreboardRows.RemoveAt(i);
        }
    }

    internal void Dispose() {
        if (_disposed) return;
        _disposed = true;

        var bindings = _bindings.ToArray();
        _bindings.Clear();
        for (int i = bindings.Length - 1; i >= 0; i--) {
            var handle = bindings[i];
            if (handle == null) continue;
            UiGuard.Run("native.dispose", handle.Dispose, handle.OwnerId);
        }

        _scoreboardRows.Clear();
        _settingsScreen = null;
        _rulesScreen = null;
    }
}
