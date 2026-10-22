using System;
using System.Collections.Generic;
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
public sealed class SettingsTabHandle : IDisposable {
    private const float LabelInputGap = 40f;
    private const float SliderWidth = 400f;

    private readonly UiScope _scope;
    private readonly NativeUiHost _host;
    private readonly Action<SettingsTabHandle> _onDispose;
    private readonly List<SettingsRow> _rows = new();

    private SettingsTabView _view;
    private bool _disposed;

    internal SettingsTabHandle(UiScope scope, NativeUiHost host, string id, string title,
                               Action<SettingsTabHandle> onDispose) {
        _scope = scope;
        _host = host;
        _onDispose = onDispose;
        Id = id;
        Title = title;
    }

    public string Id { get; }
    public string Title { get; }
    public bool IsAlive => !_disposed && _view != null && _view.IsAlive;

    /// <summary>Selects this tab if the settings screen is currently available.</summary>
    public void Select() => _view?.Select();

    public SettingsRow AddHeader(string text) {
        var row = NewRow(SettingsRowKind.Header, text);
        if (row == null) return null;
        if (IsAlive) BuildRow(row);
        return row;
    }

    public SettingsRow AddToggle(string label, bool value, Action<bool> changed) {
        var row = NewRow(SettingsRowKind.Toggle, label);
        if (row == null) return null;
        row.BoolValue = value;
        row.BoolChanged = changed;
        if (IsAlive) BuildRow(row);
        return row;
    }

    public SettingsRow AddSlider(string label, float min, float max, float value, Action<float> changed,
                                 string format = "0.##") {
        var row = NewRow(SettingsRowKind.Slider, label);
        if (row == null) return null;
        row.Min = min;
        row.Max = max > min ? max : min + 1f;
        row.FloatValue = Mathf.Clamp(value, row.Min, row.Max);
        row.Format = string.IsNullOrEmpty(format) ? "0.##" : format;
        row.FloatChanged = changed;
        if (IsAlive) BuildRow(row);
        return row;
    }

    public SettingsRow AddDropdown(string label, IEnumerable<string> options, int index, Action<int> changed) {
        var row = NewRow(SettingsRowKind.Dropdown, label);
        if (row == null) return null;
        row.Options = Normalize(options);
        row.Index = Mathf.Clamp(index, 0, Mathf.Max(0, row.Options.Count - 1));
        row.IntChanged = changed;
        if (IsAlive) BuildRow(row);
        return row;
    }

    public SettingsRow AddButton(string label, string buttonText, Action clicked) {
        var row = NewRow(SettingsRowKind.Button, label);
        if (row == null) return null;
        row.ButtonText = string.IsNullOrWhiteSpace(buttonText) ? "Open" : buttonText;
        row.Clicked = clicked;
        if (IsAlive) BuildRow(row);
        return row;
    }

    private SettingsRow NewRow(SettingsRowKind kind, string label) {
        if (_disposed) {
            UiLog.OnceError("settings:disposed", "cannot add a settings row to a disposed tab");
            return null;
        }
        var row = new SettingsRow(this, kind, label);
        _rows.Add(row);
        return row;
    }

    internal void Refresh(GameSettings settings) {
        if (_disposed) return;

        if (_view != null && _view.IsAlive) {
            if (_view.Settings == settings) return;
            Detach();
        }

        if (settings == null) return;

        try { Attach(settings); }
        catch (Exception e) {
            UiLog.OnceError("settings:attach:" + Id, $"could not add settings tab '{Id}': {e}");
            Detach();
        }
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

        var titleGo = UnityEngine.Object.Instantiate(titleTemplate.gameObject, Staging.Root);
        titleGo.name = "DM Settings Tab " + Id;
        NativeButton.StripForeign(titleGo);
        var tabTitle = titleGo.GetComponent<GameTabTitle>();
        DestroyLabel(titleGo);
        SetText(titleGo, Title);
        if (tabTitle != null) tabTitle.Selected = false;
        titleGo.transform.SetParent(titlesRoot, false);
        titleGo.SetActive(true);

        var pageGo = UnityEngine.Object.Instantiate(pageTemplate.gameObject, Staging.Root);
        pageGo.name = "DM Settings Page " + Id;
        var page = pageGo.GetComponent<GameSettingSet>();
        RectTransform holder = page != null ? page.holder : null;
        GameSettingEntry entryPrefab = page != null ? page.settingEntryInterfacePrefab : null;
        GameLabel headerPrefab = page != null ? page.headerPrefab : null;
        if (page != null) UnityEngine.Object.DestroyImmediate(page);
        if (holder == null) holder = FindHolder(pageGo.transform);

        if (holder == null || entryPrefab == null) {
            UnityEngine.Object.Destroy(titleGo);
            UnityEngine.Object.Destroy(pageGo);
            MissingTemplate();
            return;
        }

        Clear(holder);
        pageGo.transform.SetParent(contentsRoot, false);
        pageGo.SetActive(false);

        var pageRt = pageGo.GetComponent<RectTransform>();
        AppendTab(tabArea, tabTitle, pageRt);

        _view = new SettingsTabView(settings, tabArea, titleGo, pageGo, holder, entryPrefab, headerPrefab, tabTitle);
        foreach (var row in _rows) BuildRow(row);
    }

    private void BuildRow(SettingsRow row) {
        if (_view == null || !_view.IsAlive || row == null) return;

        SettingsRowView view;
        try { view = BuildVisual(row); }
        catch (Exception e) {
            UiLog.OnceError("settings:row:" + Id, $"could not build settings row '{row.Label}': {e}");
            return;
        }
        if (view == null) return;

        row.Attach(view);
        _view.Views.Add(view);
    }

    private SettingsRowView BuildVisual(SettingsRow row) {
        var runtime = _scope.Runtime;
        var holder = _view.Holder;

        if (row.Kind == SettingsRowKind.Header) {
            if (_view.HeaderPrefab == null) {
                UiLog.OnceError("settings:no-header", "settings header template is unavailable");
                return null;
            }

            var go = UnityEngine.Object.Instantiate(_view.HeaderPrefab.gameObject, Staging.Root);
            go.name = "DM Settings Header";
            NativeButton.StripForeign(go);
            DestroyLabel(go);
            SetText(go, row.Label);
            go.transform.SetParent(holder, false);
            go.SetActive(true);
            return new SettingsRowView { Go = go };
        }

        var entryGo = UnityEngine.Object.Instantiate(_view.EntryPrefab.gameObject, Staging.Root);
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
                NativeToggle toggle = null;
                toggle = SkinPolicy.TryNative("settings toggle", () =>
                    NativeToggle.Create(runtime.Templates, content, row.BoolValue, value => {
                        row.BoolValue = value;
                        try { row.BoolChanged?.Invoke(value); }
                        catch (Exception e) { UiLog.Error("settings toggle: " + e); }
                    }));
                if (toggle != null) {
                    view.Toggle = toggle;
                    var toggleRt = toggle.Go.GetComponent<RectTransform>();
                    SetControlSize(toggleRt, 28f, 28f, false);
                    AlignControlLeft(toggleRt);
                }
                else {
                    UiLog.OnceError("settings:no-toggle", "native toggle template is unavailable");
                }
                break;
            }
            case SettingsRowKind.Slider: {
                NativeSlider slider = null;
                slider = SkinPolicy.TryNative("settings slider", () =>
                    NativeSlider.Create(runtime.Templates, content, row.Min, row.Max, row.FloatValue));
                if (slider != null) {
                    slider.SetFormat(row.Format);
                    slider.SetChanged(value => {
                        row.FloatValue = value;
                        try { row.FloatChanged?.Invoke(value); }
                        catch (Exception e) { UiLog.Error("settings slider: " + e); }
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
                NativeDropdown dropdown = null;
                dropdown = SkinPolicy.TryNative("settings dropdown", () =>
                    NativeDropdown.Create(runtime.Templates, content, row.Options, row.Index));
                if (dropdown != null) {
                    dropdown.SetChanged(index => {
                        row.Index = index;
                        try { row.IntChanged?.Invoke(index); }
                        catch (Exception e) { UiLog.Error("settings dropdown: " + e); }
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
                NativeButton button = null;
                button = SkinPolicy.TryNative("settings button", () =>
                    NativeButton.CreateLabel(runtime.Templates, content, "DM Settings Button", row.ButtonText, 16f,
                        () => {
                            try { row.Clicked?.Invoke(); }
                            catch (Exception e) { UiLog.Error("settings button: " + e); }
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

    private static void SetControlSize(RectTransform rt, float width, float height, bool flexibleWidth) {
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
    private static void AlignControlLeft(RectTransform rt) {
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
    }

    private static Transform FindTitleTemplate(Transform titlesRoot) {
        if (titlesRoot == null) return null;
        for (int i = 0; i < titlesRoot.childCount; i++) {
            var child = titlesRoot.GetChild(i);
            if (child != null && child.GetComponent<GameTabTitle>() != null) return child;
        }
        return null;
    }

    private static GameSettingSet FindPageTemplate(Transform contentsRoot, GameSettings settings) {
        if (contentsRoot != null) {
            for (int i = 0; i < contentsRoot.childCount; i++) {
                var page = contentsRoot.GetChild(i).GetComponent<GameSettingSet>();
                if (page != null) return page;
            }
        }
        return settings.game ?? settings.video ?? settings.audio ?? settings.input;
    }

    private static RectTransform FindHolder(Transform page) {
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
        for (int i = 0; i < count; i++) next[i] = old[i];
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

    private static void RemoveTab(GameTabArea tabArea, GameTabTitle title, GameObject titleGo, GameObject pageGo) {
        if (tabArea != null) {
            var old = tabArea.tabs;
            int found = -1;
            if (old != null && title != null) {
                for (int i = 0; i < old.Length; i++) {
                    var candidate = old[i];
                    if (candidate.title != null && candidate.title.gameObject == title.gameObject) {
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

                if (pageGo != null && pageGo.activeSelf && next.Length > 0) tabArea.SelectTab(0);
            }
        }

        if (titleGo != null) UnityEngine.Object.Destroy(titleGo);
        if (pageGo != null) UnityEngine.Object.Destroy(pageGo);
    }

    private void Detach() {
        var view = _view;
        _view = null;
        if (view == null) return;

        for (int i = 0; i < _rows.Count; i++) _rows[i].ResetView();
        try { view.Dispose(); }
        catch (Exception e) { UiLog.Error("settings tab detach: " + e); }
    }

    /// <summary>Scene teardown: drop references without trying to mutate the dying settings hierarchy.</summary>
    internal void DetachForSceneChange() {
        if (_view == null) return;
        for (int i = 0; i < _rows.Count; i++) _rows[i].ResetView();
        _view = null;
    }

    private void MissingTemplate() {
        UiLog.OnceError("settings:no-template:" + Id,
            "settings UI templates are unavailable; the tab will be retried on the next scene");
    }

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;
        Detach();
        _host.RemoveSettingsTab(this);
        _onDispose?.Invoke(this);
        _scope.RemoveOwned(this);
    }

    internal sealed class SettingsTabView {
        private readonly GameTabArea _tabArea;
        private readonly GameObject _titleGo;
        private readonly GameObject _pageGo;
        private readonly GameTabTitle _title;

        public SettingsTabView(GameSettings settings, GameTabArea tabArea, GameObject titleGo, GameObject pageGo,
                               RectTransform holder, GameSettingEntry entryPrefab, GameLabel headerPrefab,
                               GameTabTitle title) {
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
        public GameLabel HeaderPrefab { get; }
        public List<SettingsRowView> Views { get; } = new();
        public bool IsAlive => Settings != null && _tabArea != null && _titleGo != null && _pageGo != null;

        public void Select() {
            if (!IsAlive || _tabArea.tabs == null) return;
            for (int i = 0; i < _tabArea.tabs.Length; i++) {
                var candidate = _tabArea.tabs[i];
                if (candidate.title != null && candidate.title.gameObject == _titleGo) {
                    _tabArea.SelectTab(i);
                    return;
                }
            }
        }

        public void Dispose() {
            foreach (var view in Views) view.Destroy();
            Views.Clear();
            RemoveTab(_tabArea, _title, _titleGo, _pageGo);
        }
    }

    private static List<string> Normalize(IEnumerable<string> options) {
        var values = new List<string>();
        if (options != null) {
            foreach (var option in options) values.Add(option ?? string.Empty);
        }
        if (values.Count == 0) values.Add(string.Empty);
        return values;
    }
}
