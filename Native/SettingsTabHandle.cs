using System;
using System.Collections.Generic;
using System.Globalization;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameLabel = DiceKingdoms.Interface.Label;
using GameSettingEntry = DiceKingdoms.Meta.SettingEntryInterface;
using GameSettingSet = DiceKingdoms.Meta.SettingSetInterface;
using GameSettings = DiceKingdoms.Meta.SettingSetsInterface;
using GameTabArea = DiceKingdoms.Interface.TabArea;
using GameTabTitle = DiceKingdoms.Interface.TabAreaTabTitle;
using GameToggle = DiceKingdoms.Interface.Toggle;

namespace DiceModders.UiKit.Native;

/// <summary>
/// A tab added to the game's Settings screen. It is owned by the creating scope but lives in the game's shared settings
/// hierarchy, so every mod and the game itself see it as a normal tab.
/// </summary>
public sealed class SettingsTabHandle : NativeHandle {
    private const float LabelInputGap = 40f;
    private const float SliderWidth = 400f;

    private readonly UiScope _scope;
    private readonly Action<SettingsTabHandle> _onDispose;
    private readonly List<SettingsRowHandle> _rows = new();

    private SettingsTabView? _view;

    internal SettingsTabHandle(UiScope scope, NativeUiHost host, string id, string title,
                               Action<SettingsTabHandle> onDispose)
        : base(scope, host, id) {
        _scope = scope;
        _onDispose = onDispose;
        Title = title;
    }

    public string Title { get; }
    private bool HasView => _view != null && _view.IsAlive;

    /// <summary>Selects this tab if the settings screen is currently available.</summary>
    public void Select() {
        if (IsDisposed) return;
        UiGuard.Run("settings.select", () => _view?.Select(), _scope.Id);
    }

    public SettingsRowHandle? AddHeader(string? text) {
        var row = NewRow(SettingsRowKind.Header, text);
        if (row == null) return null;
        if (HasView) BuildRow(row);
        return row;
    }

    public SettingsRowHandle? AddToggle(string? label, bool value, Action<bool>? changed) {
        var row = NewRow(SettingsRowKind.Toggle, label);
        if (row == null) return null;
        row.BoolValue = value;
        row.BoolChanged = changed;
        if (HasView) BuildRow(row);
        return row;
    }

    public SettingsRowHandle? AddSlider(string? label, float min, float max, float value, Action<float>? changed,
                                  string? format = "0.##") {
        var row = NewRow(SettingsRowKind.Slider, label);
        if (row == null) return null;
        row.Min = UiGuard.Finite(min, 0f);
        row.Max = UiGuard.Finite(max, row.Min + 1f);
        if (row.Max <= row.Min) row.Max = row.Min + 1f;
        row.FloatValue = Mathf.Clamp(UiGuard.Finite(value, row.Min), row.Min, row.Max);
        row.Format = SafeFormat(format);
        row.FloatChanged = changed;
        if (HasView) BuildRow(row);
        return row;
    }

    public SettingsRowHandle? AddDropdown(string? label, IEnumerable<string>? options, int index, Action<int>? changed) {
        var row = NewRow(SettingsRowKind.Dropdown, label);
        if (row == null) return null;
        try { row.Options = Normalize(options); }
        catch (Exception e) {
            UiGuard.Report("settings.dropdown-options", e, _scope.Id);
            row.Options = new List<string> { string.Empty };
        }
        row.Index = Mathf.Clamp(index, 0, Mathf.Max(0, row.Options.Count - 1));
        row.IntChanged = changed;
        if (HasView) BuildRow(row);
        return row;
    }

    public SettingsRowHandle? AddButton(string? label, string? buttonText, Action? clicked) {
        var row = NewRow(SettingsRowKind.Button, label);
        if (row == null) return null;
        row.ButtonText = string.IsNullOrWhiteSpace(buttonText) ? "Open" : buttonText;
        row.Clicked = clicked;
        if (HasView) BuildRow(row);
        return row;
    }

    private SettingsRowHandle? NewRow(SettingsRowKind kind, string? label) {
        if (IsDisposed) {
            UiLog.OnceError("settings:disposed", "cannot add a settings row to a disposed tab");
            return null;
        }
        var row = new SettingsRowHandle(this, kind, label);
        _rows.Add(row);
        return row;
    }

    internal override void Refresh(NativeScene scene) {
        if (IsDisposed || State == NativeState.Unsupported) return;

        var settings = scene.Settings;
        if (_view != null) {
            if (settings != null && _view.IsAlive && _view.Settings == settings) {
                MarkAttached("settings");
                return;
            }
            DetachCore();
        }

        if (settings == null) {
            MarkWaiting("waiting for the Settings screen");
            return;
        }

        try { Attach(settings); }
        catch (Exception e) {
            UiGuard.Report("settings.attach", e, _scope.Id);
            DetachCore();
            MarkFailed("settings tab could not be attached");
            return;
        }

        if (HasView) MarkAttached("settings");
        else MarkFailed(Status ?? "settings UI templates are unavailable");
    }

    private void Attach(GameSettings settings) {
        var tabsRoot = settings.transform.Find("Tabs");
        var tabArea = tabsRoot != null ? tabsRoot.GetComponent<GameTabArea>() : null;
        var titlesRoot = tabsRoot != null ? tabsRoot.Find("Titles") : null;
        var contentsRoot = tabsRoot != null ? tabsRoot.Find("Contents") : null;
        if (tabArea == null || titlesRoot == null || contentsRoot == null) {
            MissingTemplate();
            return;
        }

        var titleTemplate = FindTitleTemplate(titlesRoot);
        var pageTemplate = FindPageTemplate(contentsRoot, settings);
        if (titleTemplate == null || pageTemplate == null) {
            MissingTemplate();
            return;
        }

        GameObject? titleGo = null;
        GameObject? pageGo = null;
        GameTabTitle? tabTitle = null;

        try {
            titleGo = UnityEngine.Object.Instantiate(titleTemplate.gameObject, Staging.Root);
            titleGo.name = "DM Settings Tab " + Id;
            NativeButton.StripForeign(titleGo);
            tabTitle = titleGo.GetComponent<GameTabTitle>();
            if (tabTitle == null) {
                UnityEngine.Object.Destroy(titleGo);
                titleGo = null;
                MissingTemplate();
                return;
            }

            DestroyLabel(titleGo);
            SetText(titleGo, Title);
            tabTitle.Selected = false;
            titleGo.transform.SetParent(titlesRoot, false);
            titleGo.SetActive(true);

            pageGo = UnityEngine.Object.Instantiate(pageTemplate.gameObject, Staging.Root);
            pageGo.name = "DM Settings Page " + Id;
            var page = pageGo.GetComponent<GameSettingSet>();
            RectTransform? holder = page != null ? page.holder : null;
            GameSettingEntry? entryPrefab = page != null ? page.settingEntryInterfacePrefab : null;
            GameLabel? headerPrefab = page != null ? page.headerPrefab : null;
            if (page != null) UnityEngine.Object.DestroyImmediate(page);
            if (holder == null) holder = FindHolder(pageGo.transform);

            if (holder == null || entryPrefab == null) {
                UnityEngine.Object.Destroy(titleGo);
                titleGo = null;
                UnityEngine.Object.Destroy(pageGo);
                pageGo = null;
                MissingTemplate();
                return;
            }

            Clear(holder);
            pageGo.transform.SetParent(contentsRoot, false);
            pageGo.SetActive(false);

            var pageRt = pageGo.GetComponent<RectTransform>();
            if (pageRt == null) {
                UnityEngine.Object.Destroy(titleGo);
                titleGo = null;
                UnityEngine.Object.Destroy(pageGo);
                pageGo = null;
                MissingTemplate();
                return;
            }

            var view = new SettingsTabView(settings, tabArea, titleGo, pageGo, holder, entryPrefab, headerPrefab, tabTitle);
            AppendTab(tabArea, tabTitle, pageRt);
            _view = view;
            foreach (var row in _rows) BuildRow(row);
            MarkAttached("settings");
        }
        catch {
            if (_view != null) {
                DetachCore();
            }
            else {
                // AppendTab may have partially registered the tab before throwing. RemoveTab checks the current
                // definitions and destroys both objects, so a failed attach cannot leave a ghost native tab.
                UiGuard.Run("settings.rollback", () => RemoveTab(tabArea, tabTitle, titleGo, pageGo), _scope.Id);
            }
            throw;
        }
    }

    private void BuildRow(SettingsRowHandle row) {
        var tabView = _view;
        if (tabView == null || !tabView.IsAlive || row == null) return;

        SettingsRowView? view;
        try { view = BuildVisual(row, tabView); }
        catch (Exception e) {
            UiGuard.Report("settings.row", e, _scope.Id);
            return;
        }
        if (view == null) return;

        try {
            row.Attach(view);
            tabView.Views.Add(view);
        }
        catch (Exception e) {
            UiGuard.Report("settings.row.attach", e, _scope.Id);
            row.ResetView();
            view.Destroy();
        }
    }

    private SettingsRowView? BuildVisual(SettingsRowHandle row, SettingsTabView tabView) {
        var runtime = _scope.Runtime;
        var holder = tabView.Holder;

        if (row.Kind == SettingsRowKind.Header) {
            if (tabView.HeaderPrefab == null) {
                UiLog.OnceError("settings:no-header", "settings header template is unavailable");
                return null;
            }

            var go = UnityEngine.Object.Instantiate(tabView.HeaderPrefab.gameObject, Staging.Root);
            try {
                go.name = "DM Settings Header";
                NativeButton.StripForeign(go);
                DestroyLabel(go);
                SetText(go, row.Label);
                go.transform.SetParent(holder, false);
                go.SetActive(true);
                return new SettingsRowView { Go = go };
            }
            catch {
                if (go != null) UnityEngine.Object.Destroy(go);
                throw;
            }
        }

        var entryGo = UnityEngine.Object.Instantiate(tabView.EntryPrefab.gameObject, Staging.Root);
        try {
            entryGo.name = "DM Settings Row";
            NativeButton.StripForeign(entryGo);
            var entry = entryGo.GetComponent<GameSettingEntry>();
            var content = entry != null ? entry.contentHolder : null;
            DestroyLabel(entryGo);
            SetText(entryGo, row.Label);
            AddLabelGap(entryGo);
            if (content == null) {
                UnityEngine.Object.Destroy(entryGo);
                UiLog.OnceError("settings:no-content", "settings entry prefab has no content holder");
                return null;
            }

            entryGo.transform.SetParent(holder, false);
            entryGo.SetActive(true);

            var view = new SettingsRowView { Go = entryGo };
            switch (row.Kind) {
                case SettingsRowKind.Toggle: {
                    NativeToggle? toggle = null;
                    toggle = SkinPolicy.TryNative("settings toggle", () =>
                        NativeToggle.Create(runtime.Templates, content, row.BoolValue, value => {
                            row.BoolValue = value;
                            try { row.BoolChanged?.Invoke(value); }
                            catch (Exception e) { UiGuard.Report("settings.toggle", e, _scope.Id); }
                        }));
                    if (toggle != null) {
                        view.Toggle = toggle;
                        var toggleGo = toggle.Go;
                        var toggleRt = toggleGo != null ? toggleGo.GetComponent<RectTransform>() : null;
                        SetControlSize(toggleRt, 28f, 28f, false);
                        AlignControlLeft(toggleRt);
                    }
                    else {
                        UiLog.OnceError("settings:no-toggle", "native toggle template is unavailable");
                    }
                    break;
                }
                case SettingsRowKind.Slider: {
                    NativeSlider? slider = null;
                    slider = SkinPolicy.TryNative("settings slider", () =>
                        NativeSlider.Create(runtime.Templates, content, row.Min, row.Max, row.FloatValue));
                    if (slider != null) {
                        slider.SetFormat(row.Format);
                        slider.SetChanged(value => {
                            row.FloatValue = value;
                            try { row.FloatChanged?.Invoke(value); }
                            catch (Exception e) { UiGuard.Report("settings.slider", e, _scope.Id); }
                        });
                        view.Slider = slider;
                        SetControlSize(slider.Rt, SliderWidth, slider.Height, false);
                        AlignControlLeft(slider.Rt);
                    }
                    else {
                        UiLog.OnceError("settings:no-slider", "native slider template is unavailable");
                    }
                    break;
                }
                case SettingsRowKind.Dropdown: {
                    NativeDropdown? dropdown = null;
                    dropdown = SkinPolicy.TryNative("settings dropdown", () =>
                        NativeDropdown.Create(runtime.Templates, content, row.Options, row.Index));
                    if (dropdown != null) {
                        dropdown.SetChanged(index => {
                            row.Index = index;
                            try { row.IntChanged?.Invoke(index); }
                            catch (Exception e) { UiGuard.Report("settings.dropdown", e, _scope.Id); }
                        });
                        view.Dropdown = dropdown;
                        SetControlSize(dropdown.Rt, 220f, dropdown.Height, false);
                        AlignControlLeft(dropdown.Rt);
                    }
                    else {
                        UiLog.OnceError("settings:no-dropdown", "native dropdown template is unavailable");
                    }
                    break;
                }
                case SettingsRowKind.Button: {
                    NativeButton? button = null;
                    button = SkinPolicy.TryNative("settings button", () =>
                        NativeButton.CreateLabel(runtime.Templates, content, "DM Settings Button", row.ButtonText, 16f,
                            () => {
                                try { row.Clicked?.Invoke(); }
                                catch (Exception e) { UiGuard.Report("settings.button", e, _scope.Id); }
                            }));
                    if (button != null) {
                        button.SetSize(110f, NativeButton.SlicedFit);
                        AlignControlLeft(button.Rt);
                        button.SetInteractable(row.Enabled);
                        view.Button = button;
                    }
                    else {
                        UiLog.OnceError("settings:no-button", "native button template is unavailable");
                    }
                    break;
                }
            }

            return view;
        }
        catch {
            if (entryGo != null) UnityEngine.Object.Destroy(entryGo);
            throw;
        }
    }

    private static void SetControlSize(RectTransform? rt, float width, float height, bool flexibleWidth) {
        if (rt == null) return;
        var layout = UiFactory.Layout(rt);
        if (layout != null) {
            layout.minWidth = layout.preferredWidth = width;
            layout.minHeight = layout.preferredHeight = height;
            layout.flexibleWidth = flexibleWidth ? 1f : 0f;
            layout.flexibleHeight = 0f;
        }
        rt.sizeDelta = new Vector2(width, height);
    }

    /// <summary>
    /// Adds a fixed gutter between the game's right-aligned label and the input column. The label shrinks by the same
    /// amount that spacing grows, so the input column stays exactly where the native template put it.
    /// </summary>
    private static void AddLabelGap(GameObject entryGo) {
        if (entryGo == null) return;

        var row = entryGo.GetComponent<HorizontalLayoutGroup>();
        var labelGo = entryGo.transform.Find("Label");
        var label = labelGo != null ? labelGo.GetComponent<RectTransform>() : null;
        if (row == null || label == null) return;

        float oldSpacing = row.spacing;
        float gap = Mathf.Max(LabelInputGap, oldSpacing);
        float shrink = gap - oldSpacing;
        if (shrink <= 0f || label.sizeDelta.x <= shrink + 80f) return;

        label.sizeDelta = new Vector2(label.sizeDelta.x - shrink, label.sizeDelta.y);
        row.spacing = gap;
    }

    /// <summary>All settings inputs share one left edge, regardless of their donor prefab's anchors.</summary>
    private static void AlignControlLeft(RectTransform? rt) {
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
    }

    private static Transform? FindTitleTemplate(Transform titlesRoot) {
        if (titlesRoot == null) return null;
        for (int i = 0; i < titlesRoot.childCount; i++) {
            var child = titlesRoot.GetChild(i);
            if (child != null && child.GetComponent<GameTabTitle>() != null) return child;
        }
        return null;
    }

    private static GameSettingSet? FindPageTemplate(Transform? contentsRoot, GameSettings settings) {
        if (contentsRoot != null) {
            for (int i = 0; i < contentsRoot.childCount; i++) {
                var page = contentsRoot.GetChild(i).GetComponent<GameSettingSet>();
                if (page != null) return page;
            }
        }
        return settings.game ?? settings.video ?? settings.audio ?? settings.input;
    }

    private static RectTransform? FindHolder(Transform? page) {
        if (page == null) return null;
        var scroll = page.Find("Vertical Scroll");
        var viewport = scroll != null ? scroll.Find("Viewport") : null;
        return (viewport != null ? viewport.Find("Content") : null) as RectTransform;
    }

    private static void Clear(Transform holder) {
        for (int i = holder.childCount - 1; i >= 0; i--) {
            var child = holder.GetChild(i);
            if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
    }

    private static void DestroyLabel(GameObject go) {
        if (go == null) return;
        foreach (var label in go.GetComponentsInChildren<GameLabel>(true)) {
            if (label != null) UnityEngine.Object.DestroyImmediate(label);
        }
    }

    private static void SetText(GameObject go, string text) {
        if (go == null) return;
        var label = go.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = text ?? string.Empty;
    }

    private static void AppendTab(GameTabArea tabArea, GameTabTitle title, RectTransform content) {
        var old = tabArea.tabs;
        int count = old != null ? old.Length : 0;
        var next = new Il2CppReferenceArray<GameTabArea.TabDefinition>(count + 1);
        if (old != null) {
            for (int i = 0; i < count; i++) next[i] = old[i];
        }
        next[count] = new GameTabArea.TabDefinition { title = title, content = content };
        tabArea.tabs = next;

        if (title != null) {
            var index = tabArea.titleIndex;
            if (index == null) {
                index = new Il2CppSystem.Collections.Generic.Dictionary<GameObject, int>();
                tabArea.titleIndex = index;
            }
            index[title.gameObject] = count;
        }
    }

    private static void RemoveTab(GameTabArea? tabArea, GameTabTitle? title, GameObject? titleGo, GameObject? pageGo) {
        try {
            if (tabArea != null) {
                var old = tabArea.tabs;
                int found = -1;
                if (old != null) {
                    for (int i = 0; i < old.Length; i++) {
                        var candidate = old[i];
                        bool sameTitle = title != null && candidate.title != null &&
                                         candidate.title.gameObject == title.gameObject;
                        bool sameContent = pageGo != null && candidate.content != null &&
                                           candidate.content.gameObject == pageGo;
                        if (sameTitle || sameContent) {
                            found = i;
                            break;
                        }
                    }
                }

                if (found >= 0 && old != null) {
                    int count = old.Length - 1;
                    var next = new Il2CppReferenceArray<GameTabArea.TabDefinition>(Mathf.Max(0, count));
                    int dst = 0;
                    for (int i = 0; i < old.Length; i++) {
                        if (i == found) continue;
                        if (dst < next.Length) next[dst++] = old[i];
                    }
                    tabArea.tabs = next;

                    var index = tabArea.titleIndex;
                    if (index != null) {
                        index.Clear();
                        for (int i = 0; i < next.Length; i++) {
                            var candidate = next[i];
                            if (candidate.title != null) index[candidate.title.gameObject] = i;
                        }
                    }

                    if (pageGo != null && pageGo.activeSelf && next.Length > 0) {
                        try { tabArea.SelectTab(0); }
                        catch (Exception e) { UiLog.OnceError("settings:select-after-remove", "could not select another settings tab: " + e.Message); }
                    }
                }
            }
        }
        catch (Exception e) {
            UiLog.Error("settings tab removal failed: " + e.Message);
        }
        finally {
            if (titleGo != null) UnityEngine.Object.Destroy(titleGo);
            if (pageGo != null) UnityEngine.Object.Destroy(pageGo);
        }
    }

    protected override void DetachCore() {
        var view = _view;
        _view = null;
        if (view == null) return;

        for (int i = 0; i < _rows.Count; i++) _rows[i].ResetView();
        UiGuard.Run("settings.detach", view.Dispose, _scope.Id);
    }

    private void MissingTemplate() {
        MarkFailed("settings UI templates are unavailable");
        UiLog.OnceError("settings:no-template:" + Id,
            "settings UI templates are unavailable; the tab will be retried");
    }

    protected override void DisposeCore() {
        UiGuard.Run("settings.on-dispose", () => _onDispose?.Invoke(this), _scope.Id);
    }

    internal sealed class SettingsTabView {
        private readonly GameTabArea _tabArea;
        private readonly GameObject _titleGo;
        private readonly GameObject _pageGo;
        private readonly GameTabTitle? _title;

        public SettingsTabView(GameSettings settings, GameTabArea tabArea, GameObject titleGo, GameObject pageGo,
                               RectTransform holder, GameSettingEntry entryPrefab, GameLabel? headerPrefab,
                               GameTabTitle? title) {
            Settings = settings;
            _tabArea = tabArea;
            _titleGo = titleGo;
            _pageGo = pageGo;
            _title = title;
            Holder = holder;
            EntryPrefab = entryPrefab;
            HeaderPrefab = headerPrefab;
        }

        public GameSettings Settings { get; }
        public RectTransform Holder { get; }
        public GameSettingEntry EntryPrefab { get; }
        public GameLabel? HeaderPrefab { get; }
        public List<SettingsRowView> Views { get; } = new();
        public bool IsAlive => Settings != null && _tabArea != null && _titleGo != null && _pageGo != null;

        public void Select() {
            if (!IsAlive || _tabArea.tabs == null) return;
            try {
                for (int i = 0; i < _tabArea.tabs.Length; i++) {
                    var candidate = _tabArea.tabs[i];
                    if (candidate.title != null && candidate.title.gameObject == _titleGo) {
                        _tabArea.SelectTab(i);
                        return;
                    }
                }
            }
            catch (Exception e) {
                UiLog.OnceError("settings:select", "could not select settings tab: " + e.Message);
            }
        }

        public void Dispose() {
            foreach (var view in Views) {
                try { view.Destroy(); }
                catch (Exception e) { UiLog.Error("settings row cleanup failed: " + e.Message); }
            }
            Views.Clear();

            try { RemoveTab(_tabArea, _title, _titleGo, _pageGo); }
            catch (Exception e) { UiLog.Error("settings tab view cleanup failed: " + e.Message); }
        }
    }

    private static string SafeFormat(string? format) {
        if (string.IsNullOrWhiteSpace(format)) return "0.##";

        try {
            _ = 0f.ToString(format, CultureInfo.InvariantCulture);
            return format;
        }
        catch {
            UiLog.Once("settings:format", $"invalid settings number format '{format}'; using 0.##");
            return "0.##";
        }
    }

    private static List<string> Normalize(IEnumerable<string>? options) {
        var values = new List<string>();
        if (options != null) {
            foreach (var option in options) values.Add(option ?? string.Empty);
        }
        if (values.Count == 0) values.Add(string.Empty);
        return values;
    }
}
