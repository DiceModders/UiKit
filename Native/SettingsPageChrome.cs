using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using GameEntry = DiceKingdoms.Meta.SettingEntryInterface;
using GameSettingSet = DiceKingdoms.Meta.SettingSetInterface;
using GameSettings = DiceKingdoms.Meta.SettingSetsInterface;

namespace DiceModders.UiKit.Native;

/// <summary>
/// Narrow, library-owned fixes that make long mod blocks usable inside the game's own Settings pages. They are applied
/// only while a page has at least one mod block and are restored when the last block leaves. No public editing API.
/// </summary>
internal static class SettingsPageChrome {
    private const float WheelSensitivity = 12f;
    private const float BottomSpacerFallback = 60f;

    private static readonly List<GameSettingSet> Active = new();
    private static readonly List<WheelTarget> Targets = new();
    private static readonly List<WheelSpeed> Speeds = new();
    private static readonly List<LobbyFit> LobbyFits = new();
    private static bool _refreshQueued;

    public static void Apply(GameSettingSet? set, bool hasMods) {
        if (set == null || set.holder == null) return;

        if (!hasMods) {
            Restore(set);
            return;
        }

        if (!Contains(set)) Active.Add(set);
        EnsureWheelTarget(set.holder);
        SetWheelSpeed(set);
        if (SettingsPages.IsLobby(set)) {
            FitLobbyScroll(FindScroll(set));
        }
        else {
            AddBottomSpacer(set);
            SettingsViewport.Update(set, true);
            if (set.gameObject.activeInHierarchy) SettingsCloseShift.Update(set, true);
        }

        LayoutRebuilder.MarkLayoutForRebuild(set.holder);
        RequestRefresh();
    }

    /// <summary>Schedules the chrome adjustment for after the game's next layout pass. Requests are coalesced.</summary>
    public static void RequestRefresh() {
        if (_refreshQueued) return;
        _refreshQueued = true;
        LayoutOnce.Request(FlushRefresh);
    }

    private static void FlushRefresh() {
        _refreshQueued = false;
        RefreshVisible();
    }

    /// <summary>Shows or hides the shared Close-button shift for whichever page is visible.</summary>
    public static void RefreshVisible() {
        for (int i = Active.Count - 1; i >= 0; i--) {
            if (Active[i] == null) Active.RemoveAt(i);
        }

        GameSettingSet? active = null;
        for (int i = 0; i < Active.Count; i++) {
            var candidate = Active[i];
            if (candidate != null && candidate.gameObject.activeInHierarchy) {
                active = candidate;
                break;
            }
        }

        if (active == null) {
            SettingsCloseShift.Hide();
            return;
        }

        SettingsCloseShift.Update(active, true);
        SettingsViewport.Update(active, true);
        if (SettingsPages.IsLobby(active)) FitLobbyScroll(FindScroll(active));
    }

    /// <summary>The Settings screen was hidden or destroyed; hide the shared Close-button shift.</summary>
    public static void OnScreenHidden() {
        SettingsCloseShift.Hide();
        RequestRefresh();
    }

    public static void Restore(GameSettingSet? set) {
        if (set == null) return;

        Remove(set);
        if (SettingsPages.IsLobby(set)) RestoreLobbyScroll(FindScroll(set));
        else SettingsViewport.Update(set, false);
        RestoreWheelSpeed(set);
        RemoveWheelTarget(set.holder);
        RemoveBottomSpacer(set.holder);
        if (set.holder != null) LayoutRebuilder.MarkLayoutForRebuild(set.holder);

        // Re-evaluate the shared Close button against whichever page is actually visible now.
        RequestRefresh();
        RefreshVisible();
    }

    public static void Reset() {
        _refreshQueued = false;
        Active.Clear();
        for (int i = 0; i < Targets.Count; i++) {
            var image = Targets[i].Image;
            if (image != null) UnityEngine.Object.Destroy(image);
        }
        Targets.Clear();
        for (int i = 0; i < Speeds.Count; i++) {
            var speed = Speeds[i];
            if (speed.Scroll != null) speed.Scroll.scrollSensitivity = speed.Original;
        }
        Speeds.Clear();
        for (int i = LobbyFits.Count - 1; i >= 0; i--) RestoreLobbyScroll(LobbyFits[i].Scroll);
        LobbyFits.Clear();
        SettingsViewport.Reset();
        SettingsCloseShift.Hide();
        SettingsCloseShift.Reset();
    }

    public static bool IsActive(GameSettingSet? set) => set != null && Contains(set);

    public static float MeasureContentLeft(GameSettingSet set) {
        float result = 0f;
        try {
            var holder = set != null ? set.holder : null;
            if (holder == null) return 0f;
            LayoutRebuilder.ForceRebuildLayoutImmediate(holder);
            for (int i = 0; i < holder.childCount; i++) {
                var row = holder.GetChild(i);
                if (row == null || row.name.StartsWith("DM")) continue;
                var entry = row.GetComponent<GameEntry>();
                var column = entry != null ? entry.contentHolder : null;
                if (column == null) continue;

                float best = float.MaxValue;
                foreach (var graphic in column.GetComponentsInChildren<Graphic>(false)) {
                    if (graphic == null || graphic.transform == column) continue;
                    var rect = graphic.rectTransform;
                    if (rect.rect.width < 4f || rect.rect.height < 4f) continue;
                    float left = column.InverseTransformPoint(rect.TransformPoint(new Vector3(rect.rect.xMin, 0f, 0f))).x -
                                 column.rect.xMin;
                    if (left >= -0.5f && left < best) best = left;
                }
                if (best < 400f) {
                    result = Mathf.Max(0f, best);
                    return result;
                }
            }
        }
        catch (Exception e) {
            UiLog.Once("settings:content-left", "settings: could not measure native rows: " + e.Message);
        }
        return result;
    }

    internal static ScrollRect? FindScroll(GameSettingSet? set) {
        if (set == null || set.holder == null) return null;

        var sets = set.GetComponentInParent<GameSettings>(true);
        if (sets != null) {
            foreach (var scroll in sets.GetComponentsInChildren<ScrollRect>(true)) {
                if (scroll != null && scroll.content != null && scroll.content.Pointer == set.holder.Pointer) return scroll;
            }
        }
        return set.holder.GetComponentInParent<ScrollRect>(true);
    }

    private static void AddBottomSpacer(GameSettingSet set) {
        var holder = set != null ? set.holder : null;
        if (holder == null) return;

        for (int i = holder.childCount - 1; i >= 0; i--) {
            var child = holder.GetChild(i);
            if (child != null && child.name == "DM Settings Spacer") UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        var spacer = UiFactory.NewRect("DM Settings Spacer", holder);
        var layout = UiFactory.Layout(spacer);
        if (layout == null) return;
        layout.minHeight = layout.preferredHeight = CloseButtonHeight(set);
        layout.flexibleWidth = 0f;
    }

    private static void RemoveBottomSpacer(Transform? holder) {
        if (holder == null) return;
        for (int i = holder.childCount - 1; i >= 0; i--) {
            var child = holder.GetChild(i);
            if (child != null && child.name == "DM Settings Spacer") UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
    }

    private static float CloseButtonHeight(GameSettingSet? set) {
        try {
            var sets = set != null ? set.GetComponentInParent<GameSettings>(true) : null;
            var close = sets != null ? sets.closeButton : null;
            var rect = close != null ? close.GetComponent<RectTransform>() : null;
            if (rect != null && rect.rect.height > 4f) return rect.rect.height + 16f;
        }
        catch (Exception e) {
            UiLog.Once("settings:close-height", "settings: could not measure Close: " + e.Message);
        }
        return BottomSpacerFallback;
    }

    private static void EnsureWheelTarget(RectTransform? holder) {
        if (holder == null || holder.GetComponent<Graphic>() != null) return;
        var image = holder.gameObject.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;
        Targets.Add(new WheelTarget(holder, image));
        UiLog.InfoOnce("settings:wheeltarget", "settings: scroll content now accepts raycasts, so the game's ScrollRect gets the wheel");
    }

    private static void RemoveWheelTarget(RectTransform? holder) {
        if (holder == null) return;
        for (int i = Targets.Count - 1; i >= 0; i--) {
            var target = Targets[i];
            if (target.Holder == null || target.Image == null) {
                Targets.RemoveAt(i);
                continue;
            }
            if (!Same(target.Holder, holder)) continue;
            Targets.RemoveAt(i);
            UnityEngine.Object.Destroy(target.Image);
        }
    }

    private static void SetWheelSpeed(GameSettingSet set) {
        var scroll = FindScroll(set);
        if (scroll == null) return;
        for (int i = 0; i < Speeds.Count; i++) {
            if (Same(Speeds[i].Scroll, scroll)) {
                if (scroll.scrollSensitivity < WheelSensitivity) scroll.scrollSensitivity = WheelSensitivity;
                return;
            }
        }
        Speeds.Add(new WheelSpeed(scroll, scroll.scrollSensitivity));
        if (scroll.scrollSensitivity < WheelSensitivity) scroll.scrollSensitivity = WheelSensitivity;
    }

    private static void RestoreWheelSpeed(GameSettingSet set) {
        var scroll = FindScroll(set);
        if (scroll == null) return;
        for (int i = Speeds.Count - 1; i >= 0; i--) {
            if (!Same(Speeds[i].Scroll, scroll)) continue;
            var speed = Speeds[i];
            Speeds.RemoveAt(i);
            if (speed.Scroll != null) speed.Scroll.scrollSensitivity = speed.Original;
        }
    }

    internal static bool Same(UnityEngine.Object? left, UnityEngine.Object? right) {
        if (left == null || right == null) return false;
        return left.GetInstanceID() == right.GetInstanceID();
    }

    private static void FitLobbyScroll(ScrollRect? scroll) {
        if (scroll == null) return;
        if (scroll.GetComponentInParent<GameSettings>() != null) return;

        for (int i = LobbyFits.Count - 1; i >= 0; i--) {
            if (LobbyFits[i].Scroll == null) {
                LobbyFits.RemoveAt(i);
                continue;
            }
            if (Same(LobbyFits[i].Scroll, scroll)) return;
        }

        try {
            var mask = scroll.viewport != null ? scroll.viewport.GetComponent<RectMask2D>() : null;
            var bar = scroll.verticalScrollbar != null ? scroll.verticalScrollbar.GetComponent<RectTransform>() : null;
            var fit = new LobbyFit(scroll, mask, bar);

            if (mask != null) mask.padding = new Vector4(0f, 2f, 0f, 2f);
            if (bar != null) {
                bar.offsetMin = new Vector2(bar.offsetMin.x, bar.offsetMin.y + 4f);
                bar.offsetMax = new Vector2(bar.offsetMax.x, bar.offsetMax.y - 4f);
            }
            LobbyFits.Add(fit);
            UiLog.Info("settings: lobby box clip and scrollbar pulled inside the frame");
        }
        catch (Exception e) {
            UiLog.Warn("settings: lobby scroll fit failed: " + e.Message);
        }
    }

    private static void RestoreLobbyScroll(ScrollRect? scroll) {
        if (scroll == null) return;
        for (int i = LobbyFits.Count - 1; i >= 0; i--) {
            var fit = LobbyFits[i];
            if (fit.Scroll == null) {
                LobbyFits.RemoveAt(i);
                continue;
            }
            if (!Same(fit.Scroll, scroll)) continue;
            LobbyFits.RemoveAt(i);
            try {
                if (fit.Mask != null) fit.Mask.padding = fit.MaskPadding;
                if (fit.Bar != null) {
                    fit.Bar.offsetMin = fit.BarOffsetMin;
                    fit.Bar.offsetMax = fit.BarOffsetMax;
                }
            }
            catch (Exception e) {
                UiLog.Once("settings:lobby-restore", "settings: could not restore the Lobby scroll: " + e.Message);
            }
            return;
        }
    }

    private static bool Contains(GameSettingSet set) {
        for (int i = 0; i < Active.Count; i++) {
            if (Active[i] != null && Active[i].Pointer == set.Pointer) return true;
        }
        return false;
    }

    private static void Remove(GameSettingSet set) {
        for (int i = Active.Count - 1; i >= 0; i--) {
            if (Active[i] == null || Active[i].Pointer == set.Pointer) Active.RemoveAt(i);
        }
    }

    private sealed class WheelTarget {
        public WheelTarget(RectTransform holder, Image image) {
            Holder = holder;
            Image = image;
        }

        public RectTransform Holder { get; }
        public Image Image { get; }
    }

    private sealed class WheelSpeed {
        public WheelSpeed(ScrollRect scroll, float original) {
            Scroll = scroll;
            Original = original;
        }

        public ScrollRect Scroll { get; }
        public float Original { get; }
    }

    private sealed class LobbyFit {
        public LobbyFit(ScrollRect scroll, RectMask2D? mask, RectTransform? bar) {
            Scroll = scroll;
            Mask = mask;
            Bar = bar;
            MaskPadding = mask != null ? mask.padding : default;
            BarOffsetMin = bar != null ? bar.offsetMin : default;
            BarOffsetMax = bar != null ? bar.offsetMax : default;
        }

        public ScrollRect Scroll { get; }
        public RectMask2D? Mask { get; }
        public RectTransform? Bar { get; }
        public Vector4 MaskPadding { get; }
        public Vector2 BarOffsetMin { get; }
        public Vector2 BarOffsetMax { get; }
    }
}

/// <summary>Moves the game's Close button left while a page shows a scrollbar.</summary>
internal static class SettingsCloseShift {
    private const string Marker = "DM Shifted";

    private static GameSettingSet? _set;
    private static bool _hasMods;

    public static void Update(GameSettingSet? set, bool hasMods) {
        _set = set;
        _hasMods = hasMods;
        Evaluate();
    }

    public static void Hide() {
        if (_set == null) return;
        _hasMods = false;
        Evaluate();
    }

    public static void Reset() {
        _set = null;
        _hasMods = false;
    }

    private static void Evaluate() {
        if (_set == null) return;
        try {
            var sets = _set.GetComponentInParent<GameSettings>(true);
            var close = sets != null ? sets.closeButton : null;
            if (close == null) return;

            var bar = FindBar(sets);
            bool needs = _hasMods && bar != null && bar.size < 0.995f;
            float width = bar != null ? Mathf.Max(8f, bar.GetComponent<RectTransform>().rect.width) : 24f;

            var rect = close.GetComponent<RectTransform>();
            var marker = rect.Find(Marker);
            if (needs && marker == null) {
                rect.anchoredPosition += new Vector2(-width, 0f);
                var shift = UiFactory.NewRect(Marker, rect);
                shift.anchorMin = shift.anchorMax = new Vector2(0.5f, 0.5f);
                shift.sizeDelta = new Vector2(width, 0f);
            }
            else if (!needs && marker != null) {
                var shift = marker.GetComponent<RectTransform>();
                if (shift != null) rect.anchoredPosition += new Vector2(shift.sizeDelta.x, 0f);
                UnityEngine.Object.DestroyImmediate(marker.gameObject);
            }
        }
        catch (Exception e) {
            UiLog.Once("settings:closeshift", "settings: close button shift: " + e.Message);
        }
    }

    private static Scrollbar? FindBar(GameSettings? sets) {
        if (sets == null) return null;
        foreach (var bar in sets.GetComponentsInChildren<Scrollbar>(false)) {
            if (bar == null) continue;
            var rect = bar.GetComponent<RectTransform>();
            if (rect.rect.height > 100f && rect.rect.height > rect.rect.width * 2f) return bar;
        }
        return null;
    }
}

/// <summary>Pulls the settings viewport clip above the Close button while a page has mod rows.</summary>
internal static class SettingsViewport {
    private const float Gap = 8f;
    private static readonly List<ViewportState> Viewports = new();

    public static void Reset() {
        for (int i = Viewports.Count - 1; i >= 0; i--) RestoreViewport(Viewports[i].Mask);
        Viewports.Clear();
    }

    public static void Update(GameSettingSet? set, bool hasMods) {
        if (set == null) return;
        try {
            var scroll = SettingsPageChrome.FindScroll(set);
            if (scroll == null) return;

            var mask = scroll.GetComponentInChildren<RectMask2D>(true);
            if (mask == null) return;

            if (!hasMods) {
                RestoreViewport(mask);
                return;
            }

            var sets = set.GetComponentInParent<GameSettings>(true);
            var close = sets != null ? sets.closeButton : null;
            if (close == null) return;

            var rect = mask.GetComponent<RectTransform>();
            if (rect.rect.width < 1f || rect.rect.height < 1f) return;

            EnsureViewport(mask);

            var closeCorners = new Vector3[4];
            var maskCorners = new Vector3[4];
            close.GetComponent<RectTransform>().GetWorldCorners(closeCorners);
            rect.GetWorldCorners(maskCorners);
            float scale = Mathf.Abs(rect.lossyScale.y);
            float want = 0f;
            if (scale > 0.00001f) {
                float closeTop = closeCorners[1].y;
                float maskBottom = maskCorners[0].y;
                float maskTop = maskCorners[1].y;
                float needed = (closeTop - maskBottom) / scale + Gap;
                if (needed > Gap && closeTop < maskTop) want = Mathf.Min(needed, rect.rect.height * 0.5f);
            }

            var padding = mask.padding;
            if (Mathf.Abs(padding.y - want) < 0.5f) return;
            padding.y = want;
            mask.padding = padding;
        }
        catch (Exception e) {
            UiLog.Once("settings:viewport", "settings: viewport update: " + e.Message);
        }
    }

    private static void EnsureViewport(RectMask2D mask) {
        for (int i = Viewports.Count - 1; i >= 0; i--) {
            if (Viewports[i].Mask == null) {
                Viewports.RemoveAt(i);
                continue;
            }
            if (SettingsPageChrome.Same(Viewports[i].Mask, mask)) return;
        }
        Viewports.Add(new ViewportState(mask, mask.padding));
    }

    private static void RestoreViewport(RectMask2D? mask) {
        if (mask == null) return;
        for (int i = Viewports.Count - 1; i >= 0; i--) {
            var state = Viewports[i];
            if (state.Mask == null) {
                Viewports.RemoveAt(i);
                continue;
            }
            if (!SettingsPageChrome.Same(state.Mask, mask)) continue;
            Viewports.RemoveAt(i);
            try { state.Mask.padding = state.Original; }
            catch (Exception e) { UiLog.Once("settings:viewport-restore", "settings: could not restore the viewport: " + e.Message); }
            return;
        }
    }

    private sealed class ViewportState {
        public ViewportState(RectMask2D mask, Vector4 original) {
            Mask = mask;
            Original = original;
        }

        public RectMask2D Mask { get; }
        public Vector4 Original { get; }
    }
}
