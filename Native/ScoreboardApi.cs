using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiceModders.UiKit.Native;

/// <summary>
/// Adds columns to the in-match scoreboard. A column is a stable registration; its values are pushed by the mod with
/// <see cref="ScoreboardColumnHandle.SetValue"/> or <see cref="ScoreboardColumnHandle.SetValues"/>.
/// </summary>
public sealed class ScoreboardApi {
    private readonly UiScope _scope;
    private readonly NativeUiHost _host;
    private readonly Dictionary<string, ScoreboardColumnHandle> _columns = new();

    internal ScoreboardApi(UiScope scope) {
        _scope = scope;
        _host = scope.Runtime.NativeUi;
    }

    /// <summary>Adds a column described by <paramref name="spec"/>.</summary>
    public ScoreboardColumnHandle? Add(ScoreboardColumnSpec? spec)
        => spec == null ? null : AddColumn(spec.Id, spec.Tooltip, spec.Icon);

    /// <summary>
    /// Adds a column to every scoreboard row. The cell is cloned from the game's own stat cell, so it looks native.
    /// Returns a stable handle; call <see cref="ScoreboardColumnHandle.SetValue"/> whenever the displayed value changes.
    /// </summary>
    public ScoreboardColumnHandle? AddColumn(string? id, string? tooltip = null, Sprite? icon = null) {
        if (!_scope.IsAlive) {
            UiLog.OnceError("scoreboard:disposed-scope", "cannot add a scoreboard column to a disposed scope");
            return null;
        }
        if (string.IsNullOrWhiteSpace(id)) {
            UiLog.OnceError("scoreboard:no-id", "a scoreboard column needs a stable id");
            return null;
        }

        string columnId = id.Trim();
        if (_columns.TryGetValue(columnId, out var existing) && !existing.IsDisposed) {
            UiLog.OnceError("scoreboard:duplicate:" + columnId,
                $"scoreboard column '{columnId}' already exists in this scope");
            return existing;
        }
        _columns.Remove(columnId);

        ScoreboardColumnHandle? handle = null;
        try {
            handle = new ScoreboardColumnHandle(_scope, _host, columnId, tooltip, icon,
                value => _columns.Remove(value.Id));
            if (!SkinPolicy.AllowNative(_scope.Runtime)) {
                handle.MarkUnsupported("TemplatePolicy.ManagedOnly disables native scoreboard columns");
            }
            _columns[columnId] = handle;
            return _host.Register(handle);
        }
        catch (Exception e) {
            UiGuard.Report("scoreboard.add", e, _scope.Id);
            UiGuard.Run("scoreboard.rollback", () => handle?.Dispose(), _scope.Id);
            _columns.Remove(columnId);
            return null;
        }
    }
}
