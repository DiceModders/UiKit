using System;
using UnityEngine;
using UnityEngine.UI;

namespace DiceModders.UiKit.Native;

/// <summary>A vertical scrollbar cloned from the game's own scroll view, with the donor's value listeners removed.</summary>
internal static class NativeScrollbar {
    public static Scrollbar? Create(TemplateCatalog? templates, RectTransform? parent) {
        var template = templates?.VerticalScrollbar();
        if (template == null) return null;

        try {
            var go = UnityEngine.Object.Instantiate(template.gameObject, Staging.Root);
            go.name = "DM Scrollbar";
            NativeButton.StripForeign(go);
            NativeButton.RepairBaseButtons(go);

            var bar = go.GetComponent<Scrollbar>();
            if (bar == null || bar.handleRect == null) {
                UnityEngine.Object.Destroy(go);
                return null;
            }

            bar.onValueChanged = new Scrollbar.ScrollEvent();
            bar.direction = Scrollbar.Direction.BottomToTop;

            var found = go.GetComponent<RectTransform>();
            if (found == null) { UnityEngine.Object.Destroy(go); return null; }
            var rect = found;
            float width = Mathf.Clamp(rect.rect.width > 1f ? rect.rect.width : 10f, 8f, 24f);

            go.transform.SetParent(parent, false);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(width, 0f);
            rect.anchoredPosition = Vector2.zero;

            go.SetActive(true);
            return bar;
        }
        catch (Exception e) {
            UiLog.Once("native:scrollbar", "native scrollbar failed: " + e.Message);
            return null;
        }
    }
}
