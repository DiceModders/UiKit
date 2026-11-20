using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameEntry = DiceKingdoms.Meta.SettingEntryInterface;
using GameSettingSet = DiceKingdoms.Meta.SettingSetInterface;

namespace DiceModders.UiKit.Native;

/// <summary>
/// Mod section titles stay centred on the row like the game's own titles. A title wide enough to reach over the label
/// column is moved right so it starts where the controls start (or as far as the right edge allows). Measured on the
/// visible page after layout: the lobby box and the Settings pages use different row templates, and a hidden page has
/// no sizes yet.
/// </summary>
internal static class SettingsHeaderAlign {
    internal const string HeaderName = "DM Settings Header";
    private const float RightReserve = 24f;
    // the template's margin of each title; the instance id guards against a later object at the same native address
    private static readonly Dictionary<IntPtr, (int id, Vector4 margin)> Original = new();

    public static void Apply(GameSettingSet? set) {
        var holder = set != null ? set.holder : null;
        if (holder == null || !holder.gameObject.activeInHierarchy) return;

        float column = ControlLeft(holder);
        if (column <= 0f) return;

        for (int i = 0; i < holder.childCount; i++) {
            var child = holder.GetChild(i);
            if (child == null || child.name != HeaderName || !child.gameObject.activeSelf) continue;
            var title = child.GetComponentInChildren<TMP_Text>(true);
            if (title != null) Fit(title, holder, column);
        }
    }

    public static void Forget() => Original.Clear();

    /// <summary>The x (in holder space) where the first visible control starts, read from the laid-out rows.</summary>
    private static float ControlLeft(RectTransform holder) {
        for (int i = 0; i < holder.childCount; i++) {
            var row = holder.GetChild(i);
            if (row == null || !row.gameObject.activeSelf) continue;
            var entry = row.GetComponent<GameEntry>();
            var content = entry != null ? entry.contentHolder : null;
            if (content == null || !content.gameObject.activeInHierarchy) continue;

            float best = float.MaxValue;
            foreach (var graphic in content.GetComponentsInChildren<Graphic>(false)) {
                if (graphic == null || graphic.transform == content) continue;
                var rect = graphic.rectTransform;
                if (rect.rect.width < 4f || rect.rect.height < 4f) continue;
                float x = holder.InverseTransformPoint(rect.TransformPoint(new Vector3(rect.rect.xMin, 0f, 0f))).x;
                if (x < best) best = x;
            }
            if (best < float.MaxValue) return best - holder.rect.xMin;
        }
        return 0f;
    }

    private static void Fit(TMP_Text title, RectTransform holder, float column) {
        var rt = title.rectTransform;
        float s = rt.lossyScale.x / Mathf.Max(1e-6f, holder.lossyScale.x);
        if (s <= 0f) return;

        int id = title.GetInstanceID();
        Vector4 m0;
        if (Original.TryGetValue(title.Pointer, out var known) && known.id == id) m0 = known.margin;
        else Original[title.Pointer] = (id, m0 = title.margin);

        float text = title.textBounds.size.x * s;
        if (text <= 1f) {
            title.ForceMeshUpdate();
            text = title.textBounds.size.x * s;
        }
        if (text <= 1f) return;

        float left = holder.InverseTransformPoint(rt.TransformPoint(new Vector3(rt.rect.xMin, 0f, 0f))).x - holder.rect.xMin;
        float right = holder.InverseTransformPoint(rt.TransformPoint(new Vector3(rt.rect.xMax, 0f, 0f))).x - holder.rect.xMin;

        // where the text starts when centred with the template's own margins
        float baseLeft = (left + m0.x * s + right - m0.z * s - text) * 0.5f;
        float target = Mathf.Max(baseLeft, Mathf.Min(column, right - RightReserve - text));
        float shift = target - baseLeft;

        var want = shift > 0.5f ? new Vector4(m0.x + 2f * shift / s, m0.y, m0.z, m0.w) : m0;
        if (Mathf.Abs(title.margin.x - want.x) < 0.5f) return;
        if (shift > 0.5f) {
            title.enableWordWrapping = false;
            title.overflowMode = TextOverflowModes.Overflow;
        }
        title.margin = want;
    }
}
