using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DiceModders.UiKit.Native;

/// <summary>Shared layout work for mod scoreboard columns: collapse the empty send-button cell and fit the board.</summary>
internal static class ScoreboardLayout {
    private const float SideMargin = 260f;
    private static RectTransform? _board;
    private static readonly Dictionary<GameObject, TrailState> Trails = new();

    /// <summary>
    /// Forgets the cached board after the scoreboard is destroyed or a scene changes. Trail states stay with their
    /// cells until the column releases them, so an in-place scoreboard refresh does not lose the restore information.
    /// </summary>
    public static void Reset() => _board = null;

    /// <summary>
    /// The game's row ends with an empty cell reserved for the send-resources button. Extra columns take that place
    /// while the button is hidden, keeping the board at its original width.
    /// </summary>
    public static bool CollapseTrail(GameObject? trail) {
        if (trail == null) return false;
        try {
            if (Trails.TryGetValue(trail, out var existing)) {
                existing.Count++;
                return true;
            }

            var rect = trail.TryCast<RectTransform>();
            if (rect == null) return false;

            var layout = trail.GetComponent<LayoutElement>();
            bool addedLayout = layout == null;
            if (layout == null) layout = trail.AddComponent<LayoutElement>();

            var state = new TrailState(rect, layout, addedLayout);
            state.Collapse();
            Trails[trail] = state;
            return true;
        }
        catch (Exception e) {
            UiLog.Once("scoreboard:trail", "scoreboard trail: " + e.Message);
            return false;
        }
    }

    /// <summary>Restores a donor cell once the last mod column using it has been detached.</summary>
    public static void ReleaseTrail(GameObject? trail) {
        if (trail == null || !Trails.TryGetValue(trail, out var state)) return;

        state.Count--;
        if (state.Count > 0) return;

        Trails.Remove(trail);
        try { state.Restore(); }
        catch (Exception e) { UiLog.Once("scoreboard:trail-restore", "scoreboard trail restore: " + e.Message); }
    }

    /// <summary>Center the board and shrink it just enough to stay clear of the game's side panels.</summary>
    public static void FitBoard(int cellCount, Transform? anyCell) {
        try {
            if (cellCount <= 0) {
                if (_board != null) _board.localScale = Vector3.one;
                return;
            }

            if (_board == null && anyCell != null) {
                for (var t = anyCell.parent; t != null; t = t.parent) {
                    if (t.name != "Scores") continue;
                    _board = t.TryCast<RectTransform>();
                    break;
                }
            }
            if (_board == null) return;

            var root = _board.root.TryCast<RectTransform>();
            if (root == null) return;

            float current = Mathf.Max(0.01f, _board.localScale.x);
            float width = RectTransformUtility.CalculateRelativeRectTransformBounds(root, _board).size.x / current;
            if (width < 1f) return;

            float scale = Mathf.Clamp((root.rect.width - 2f * SideMargin) / width, 0.5f, 1f);
            if (Mathf.Abs(_board.localScale.x - scale) > 0.005f)
                _board.localScale = new Vector3(scale, scale, 1f);
        }
        catch (Exception e) {
            UiLog.Once("scoreboard:fit", "scoreboard fit: " + e.Message);
        }
    }

    private sealed class TrailState {
        public TrailState(RectTransform rect, LayoutElement layout, bool addedLayout) {
            Rect = rect;
            Layout = layout;
            AddedLayout = addedLayout;
            PreferredWidth = layout.preferredWidth;
            MinWidth = layout.minWidth;
            FlexibleWidth = layout.flexibleWidth;
            SizeDelta = rect.sizeDelta;
        }

        private RectTransform Rect { get; }
        private LayoutElement Layout { get; }
        private bool AddedLayout { get; }
        private float PreferredWidth { get; }
        private float MinWidth { get; }
        private float FlexibleWidth { get; }
        private Vector2 SizeDelta { get; }
        public int Count { get; set; } = 1;

        public void Collapse() {
            Layout.preferredWidth = 0f;
            Layout.minWidth = 0f;
            Layout.flexibleWidth = 0f;
            Rect.sizeDelta = new Vector2(0f, Rect.sizeDelta.y);
            MarkParent();
        }

        public void Restore() {
            if (Layout != null) {
                Layout.preferredWidth = PreferredWidth;
                Layout.minWidth = MinWidth;
                Layout.flexibleWidth = FlexibleWidth;
            }
            if (Rect != null) Rect.sizeDelta = SizeDelta;
            MarkParent();
            if (AddedLayout && Layout != null) UnityEngine.Object.Destroy(Layout);
        }

        private void MarkParent() {
            var parent = Rect != null ? Rect.parent : null;
            if (parent != null) LayoutRebuilder.MarkLayoutForRebuild(parent.TryCast<RectTransform>());
        }
    }
}
