using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameButton = DiceKingdoms.Interface.BaseButton;
using GameIsland = DiceKingdoms.Game.Island;
using GameRow = DiceKingdoms.Interface.ScoreboardRow;
using GameStat = DiceKingdoms.Interface.ScoreboardStat;
using GameToolTip = DiceKingdoms.Interface.HasToolTip;

namespace DiceModders.UiKit.Native;

/// <summary>
/// A column added to every row of the in-match scoreboard. Values are pushed explicitly with <see cref="SetValue"/> or
/// <see cref="SetValues"/>; the library caches them and re-applies them whenever a row is rebuilt or the scene changes.
/// </summary>
public sealed class ScoreboardColumnHandle : NativeHandle {
    private readonly Dictionary<int, string> _values = new();
    private readonly Dictionary<int, Cell> _cells = new();

    private string? _tooltip;
    private Sprite? _icon;

    internal ScoreboardColumnHandle(UiScope scope, NativeUiHost host, string id, string? tooltip, Sprite? icon)
        : base(scope, host, id) {
        _tooltip = tooltip;
        _icon = icon;
    }

    /// <summary>Tooltip shown when the player hovers a cell.</summary>
    public string? Tooltip => _tooltip;

    /// <summary>Icon shown in each cell, or <c>null</c> to hide the donor icon.</summary>
    public Sprite? Icon => _icon;

    /// <summary>Number of scoreboard rows currently carrying this column.</summary>
    public int AttachedCells {
        get {
            int count = 0;
            foreach (var cell in _cells.Values) {
                if (cell.Go != null && cell.Row != null) count++;
            }
            return count;
        }
    }

    internal Transform? FirstCell {
        get {
            foreach (var cell in _cells.Values) {
                if (cell.Go != null) return cell.Go.transform;
            }
            return null;
        }
    }

    /// <summary>Sets the cell text for one kingdom. A null value clears the cell.</summary>
    public void SetValue(GameIsland? island, string? value) {
        if (IsDisposed || island == null) return;
        int key;
        try { key = island.GetInstanceID(); }
        catch (Exception e) {
            UiGuard.Report("scoreboard.value", e, OwnerId);
            return;
        }

        _values[key] = value ?? string.Empty;
        ApplyValue(key);
    }

    /// <summary>Replaces every cached value. Rows without an entry show an empty cell.</summary>
    public void SetValues(IEnumerable<KeyValuePair<GameIsland, string>>? values) {
        if (IsDisposed) return;

        var next = new Dictionary<int, string>();
        if (values != null) {
            try {
                foreach (var pair in values) {
                    if (pair.Key == null) continue;
                    next[pair.Key.GetInstanceID()] = pair.Value ?? string.Empty;
                }
            }
            catch (Exception e) {
                // A failing caller enumerable must not leave a half-replaced cache behind.
                UiGuard.Report("scoreboard.values", e, OwnerId);
                return;
            }
        }

        _values.Clear();
        foreach (var pair in next) _values[pair.Key] = pair.Value;

        foreach (var cell in _cells.Values) ApplyCellText(cell);
    }

    /// <summary>Removes all cached values and clears every attached cell.</summary>
    public void ClearValues() => SetValues(null);

    /// <summary>Changes the hover text and updates existing cells.</summary>
    public void SetTooltip(string? tooltip) {
        if (IsDisposed) return;
        _tooltip = tooltip;
        foreach (var cell in _cells.Values) ApplyTooltip(cell);
    }

    /// <summary>Changes the cell icon and updates existing cells.</summary>
    public void SetIcon(Sprite? icon) {
        if (IsDisposed) return;
        _icon = icon;
        foreach (var cell in _cells.Values) ApplyIcon(cell);
    }

    internal override void Refresh(NativeScene scene) {
        if (IsDisposed || State == NativeState.Unsupported) return;

        PruneCells();
        var rows = scene.ScoreboardRows;
        if (rows == null || rows.Count == 0) {
            DetachCore();
            MarkWaiting("waiting for the scoreboard");
            return;
        }

        int attached = 0;
        int missing = 0;
        for (int i = 0; i < rows.Count; i++) {
            var row = rows[i];
            if (row == null) continue;

            int key;
            try { key = row.GetInstanceID(); }
            catch { continue; }

            if (_cells.TryGetValue(key, out var existing) && existing.Go != null) {
                attached++;
                continue;
            }

            if (TryAttach(row, key)) attached++;
            else missing++;
        }

        if (attached > 0) MarkAttached(missing > 0 ? $"{missing} scoreboard row(s) could not be extended" : "scoreboard");
        else MarkFailed("no scoreboard row has a stat cell to copy");
    }

    /// <summary>Collapses the empty trail cell and returns the number of live cells after a row rebuild.</summary>
    internal int PrepareLayout() {
        if (IsDisposed || State == NativeState.Unsupported) return 0;

        PruneCells();
        foreach (var cell in _cells.Values) {
            if (!cell.TrailCollapsed && cell.Trail != null) {
                cell.TrailCollapsed = ScoreboardLayout.CollapseTrail(cell.Trail);
            }
        }
        return _cells.Count;
    }

    /// <summary>Re-reads the cached value after the game changed which island a row represents.</summary>
    internal void ReapplyRow(GameRow row) {
        if (IsDisposed || State == NativeState.Unsupported || row == null) return;

        int key;
        try { key = row.GetInstanceID(); }
        catch { return; }

        if (_cells.TryGetValue(key, out var cell) && cell.Go != null) ApplyCellText(cell);
    }

    protected override void DisposeCore() {
        Scope.Runtime.NativeUi.OnScoreboardColumnDisposed();
    }

    protected override void DetachCore() {
        foreach (var cell in _cells.Values) {
            if (cell.Go != null) UiGuard.Run("scoreboard.destroy", () => UnityEngine.Object.Destroy(cell.Go), OwnerId);
        }
        _cells.Clear();
    }

    private bool TryAttach(GameRow row, int key) {
        GameObject? go = null;
        try {
            var stats = row.stats;
            GameStat? donor = stats != null && stats.Length > 0 ? stats[stats.Length - 1] : row.firstStat;
            if (donor == null || donor.gameObject == null) {
                UiLog.OnceError("scoreboard:nodonor:" + Id,
                    "a scoreboard row has no stat cell to copy; the column will be retried");
                return false;
            }

            go = UnityEngine.Object.Instantiate(donor.gameObject, Staging.Root);
            go.name = "DM Scoreboard " + Id;
            NativeButton.StripForeign(go);

            var logic = go.GetComponent<GameStat>();
            if (logic != null) UnityEngine.Object.DestroyImmediate(logic);
            var gameButton = go.GetComponent<GameButton>();
            if (gameButton != null) UnityEngine.Object.DestroyImmediate(gameButton);
            var unityButton = go.GetComponent<Button>();
            if (unityButton != null) UnityEngine.Object.DestroyImmediate(unityButton);

            var text = FindText(go);
            if (text == null) {
                UiLog.OnceError("scoreboard:notext:" + Id, "the scoreboard stat template has no text; the column will be retried");
                UnityEngine.Object.Destroy(go);
                return false;
            }

            var tip = ConfigureTooltip(go, go.GetComponent<GameToolTip>());
            var cell = new Cell {
                Row = row,
                Go = go,
                Text = text,
                Tip = tip,
                Trail = donor.gameObject,
            };
            _cells[key] = cell;

            int donorIndex = donor.transform.GetSiblingIndex();
            go.transform.SetParent(donor.transform.parent, false);
            go.transform.SetSiblingIndex(donorIndex);
            go.SetActive(true);

            FitText(text);
            ApplyIcon(cell);
            ApplyTooltip(cell);
            ApplyCellText(cell);
            return true;
        }
        catch (Exception e) {
            if (go != null) UiGuard.Run("scoreboard.rollback", () => UnityEngine.Object.Destroy(go), OwnerId);
            UiGuard.Report("scoreboard.attach", e, OwnerId);
            return false;
        }
    }

    private void ApplyValue(int key) {
        foreach (var cell in _cells.Values) {
            if (cell.Row == null) continue;
            int rowKey;
            try { rowKey = cell.Row.GetInstanceID(); }
            catch { continue; }
            if (rowKey == key) ApplyCellText(cell);
        }
    }

    private void ApplyCellText(Cell cell) {
        if (cell.Text == null || cell.Row == null) return;

        string value = string.Empty;
        try {
            var island = cell.Row.island;
            if (island != null && _values.TryGetValue(island.GetInstanceID(), out var cached)) value = cached;
        }
        catch (Exception e) {
            UiGuard.Report("scoreboard.read-value", e, OwnerId);
        }

        value ??= string.Empty;
        if (cell.Shown == value) return;
        cell.Shown = value;
        UiGuard.Run("scoreboard.text", () => {
            if (cell.Text != null) cell.Text.text = value;
        }, OwnerId);
    }

    private void ApplyTooltip(Cell cell) {
        if (cell.Go == null) return;
        UiGuard.Run("scoreboard.tooltip", () => {
            var tip = ConfigureTooltip(cell.Go, cell.Tip);
            cell.Tip = tip;
            if (tip != null) tip.UpdateInitialText(_tooltip ?? string.Empty);
        }, OwnerId);
    }

    private void ApplyIcon(Cell cell) {
        if (cell.Go == null) return;
        UiGuard.Run("scoreboard.icon", () => {
            foreach (var image in cell.Go.GetComponentsInChildren<Image>(true)) {
                if (image == null || image.gameObject == cell.Go) continue;
                if (image.name.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (_icon == null) {
                    image.enabled = false;
                }
                else {
                    image.enabled = true;
                    image.sprite = _icon;
                    image.preserveAspect = true;
                }
            }
        }, OwnerId);
    }

    private GameToolTip? ConfigureTooltip(GameObject go, GameToolTip? tip) {
        if (string.IsNullOrWhiteSpace(_tooltip)) {
            if (tip != null) UnityEngine.Object.DestroyImmediate(tip);
            return null;
        }

        if (tip == null) tip = go.AddComponent<GameToolTip>();
        tip.localizedInitialText = new UnityEngine.Localization.LocalizedString();
        tip.initialText = _tooltip;
        return tip;
    }

    private static TMP_Text? FindText(GameObject go) {
        foreach (var text in go.GetComponentsInChildren<TMP_Text>(true)) {
            if (text != null) return text;
        }
        return null;
    }

    private static void FitText(TMP_Text text) {
        if (text == null) return;
        text.enableWordWrapping = false;
        text.enableAutoSizing = true;
        text.fontSizeMin = 8f;
        text.fontSizeMax = text.fontSize;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void PruneCells() {
        if (_cells.Count == 0) return;

        var dead = new List<int>();
        foreach (var pair in _cells) {
            if (pair.Value.Go == null || pair.Value.Row == null) dead.Add(pair.Key);
        }
        for (int i = 0; i < dead.Count; i++) {
            if (_cells.TryGetValue(dead[i], out var cell) && cell.Go != null) {
                var go = cell.Go;
                UiGuard.Run("scoreboard.prune", () => UnityEngine.Object.Destroy(go), OwnerId);
            }
            _cells.Remove(dead[i]);
        }
    }

    private sealed class Cell {
        public GameRow? Row;
        public GameObject? Go;
        public TMP_Text? Text;
        public GameToolTip? Tip;
        public GameObject? Trail;
        public bool TrailCollapsed;
        public string? Shown;
    }
}
