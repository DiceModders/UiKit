using UnityEngine;
using UnityEngine.UI;

namespace DiceModders.UiKit.Native;

/// <summary>Shared layout work for mod scoreboard columns: collapse the empty send-button cell and fit the board.</summary>
internal static class ScoreboardLayout {
    private const float SideMargin = 260f;
    private static RectTransform? _board;

    /// <summary>
    /// The game's row ends with an empty cell reserved for the send-resources button. Extra columns take that place
    /// while the button is hidden, keeping the board at its original width.
    /// </summary>
    public static bool CollapseTrail(GameObject? trail) {
        if (trail == null) return false;
        try {
            var rect = trail.TryCast<RectTransform>();
            if (rect == null) return false;

            var layout = trail.GetComponent<LayoutElement>();
            if (layout == null) layout = trail.AddComponent<LayoutElement>();
            layout.preferredWidth = 0f;
            layout.minWidth = 0f;
            layout.flexibleWidth = 0f;
            rect.sizeDelta = new Vector2(0f, rect.sizeDelta.y);
            LayoutRebuilder.MarkLayoutForRebuild(rect.parent.TryCast<RectTransform>());
            return true;
        }
        catch (System.Exception e) {
            UiLog.Once("scoreboard:trail", "scoreboard trail: " + e.Message);
            return false;
        }
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
        catch (System.Exception e) {
            UiLog.Once("scoreboard:fit", "scoreboard fit: " + e.Message);
        }
    }
}
