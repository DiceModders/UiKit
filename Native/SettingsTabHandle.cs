using System;
using System.Collections.Generic;
using System.Globalization;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;
using GameLabel = DiceKingdoms.Interface.Label;
using GameSettingEntry = DiceKingdoms.Meta.SettingEntryInterface;
using GameSettingSet = DiceKingdoms.Meta.SettingSetInterface;
using GameSettings = DiceKingdoms.Meta.SettingSetsInterface;
using GameTabArea = DiceKingdoms.Interface.TabArea;
using GameTabTitle = DiceKingdoms.Interface.TabAreaTabTitle;

namespace DiceModders.UiKit.Native;

/// <summary>
/// A tab added to the game's Settings screen. It is owned by the creating scope but lives in the game's shared settings
/// hierarchy, so every mod and the game itself see it as a normal tab.
/// </summary>
public sealed class SettingsTabHandle : NativeHandle {
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
                                  string? format = "0.##", bool wholeNumbers = false) {
        var row = NewRow(SettingsRowKind.Slider, label);
        if (row == null) return null;
        row.Min = UiGuard.Finite(min, 0f);
        row.Max = UiGuard.Finite(max, row.Min + 1f);
        if (row.Max <= row.Min) row.Max = row.Min + 1f;
        row.FloatValue = Mathf.Clamp(UiGuard.Finite(value, row.Min), row.Min, row.Max);
        row.WholeNumbers = wholeNumbers;
        if (wholeNumbers) row.FloatValue = Mathf.Round(row.FloatValue);
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
        row.ButtonText = string.IsNullOrWhiteSpace(buttonText) ? "Open" : buttonText!;
        row.Clicked = clicked;
        if (HasView) BuildRow(row);
        return row;
    }

    /// <summary>A row with a label and several buttons. Each button is sized to fit the row's available width.</summary>
    public SettingsRowHandle? AddButtons(string? label, params (string text, Action? clicked)[] buttons) {
        var row = NewRow(SettingsRowKind.Buttons, label);
        if (row == null) return null;

        if (buttons != null) {
            foreach (var button in buttons) row.Buttons.Add(new SettingsButtonDefinition(button.text, button.clicked));
        }
        if (row.Buttons.Count == 0) row.Buttons.Add(new SettingsButtonDefinition("OK", null));
        if (HasView) BuildRow(row);
        return row;
    }

    /// <summary>
    /// A "-" / value / "+" row. The value text provider is read when the row is attached and after each step; it is not
    /// polled every frame.
    /// </summary>
    public SettingsRowHandle? AddStepper(string? label, Func<string>? valueText, Action<int>? step) {
        var row = NewRow(SettingsRowKind.Stepper, label);
        if (row == null) return null;
        row.TextProvider = valueText;
        row.Text = valueText == null ? string.Empty : SafeRead(valueText, _scope.Id);
        row.IntChanged = step;
        if (HasView) BuildRow(row);
        return row;
    }

    /// <summary>A wrapped paragraph. Use <see cref="SettingsRowHandle.SetText"/> to change it later.</summary>
    public SettingsRowHandle? AddInfo(string? text) {
        var row = NewRow(SettingsRowKind.Info, null);
        if (row == null) return null;
        row.Text = text ?? string.Empty;
        if (HasView) BuildRow(row);
        return row;
    }

    private SettingsRowHandle? NewRow(SettingsRowKind kind, string? label) {
        if (IsDisposed) {
            UiLog.OnceError("settings:disposed", "cannot add a settings row to a disposed tab");
            return null;
        }
        var row = new SettingsRowHandle(_scope.Id, kind, label);
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

            var rows = new SettingsRowHost(_scope, holder, entryPrefab, headerPrefab);
            var view = new SettingsTabView(settings, tabArea, titleGo, pageGo, pageRt, rows, tabTitle);
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
        try { view = tabView.Rows.Build(row); }
        catch (Exception e) {
            UiGuard.Report("settings.row", e, _scope.Id);
            return;
        }
        if (view == null) return;

        try {
            row.Attach(view);
            tabView.Rows.Add(view);
        }
        catch (Exception e) {
            UiGuard.Report("settings.row.attach", e, _scope.Id);
            row.ResetView();
            view.Destroy();
        }
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
                               RectTransform pageRect, SettingsRowHost rows, GameTabTitle? title) {
            Settings = settings;
            _tabArea = tabArea;
            _titleGo = titleGo;
            _pageGo = pageGo;
            _title = title;
            PageRect = pageRect;
            Rows = rows;
        }

        public GameSettings Settings { get; }
        public RectTransform PageRect { get; }
        public SettingsRowHost Rows { get; }
        public bool IsAlive => Settings != null && _tabArea != null && _titleGo != null && _pageGo != null &&
                               Rows.IsAlive;

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
            Rows.Dispose();

            try { RemoveTab(_tabArea, _title, _titleGo, _pageGo); }
            catch (Exception e) { UiLog.Error("settings tab view cleanup failed: " + e.Message); }
        }
    }

    private static string SafeFormat(string? format) {
        if (string.IsNullOrWhiteSpace(format)) return "0.##";

        try {
            _ = 0f.ToString(format, CultureInfo.InvariantCulture);
            return format!;
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

    private static string SafeRead(Func<string>? value, string ownerId) {
        if (value == null) return string.Empty;
        try { return value() ?? string.Empty; }
        catch (Exception e) {
            UiLog.Once("settings:text:" + ownerId, "settings value text: " + e.Message);
            return string.Empty;
        }
    }
}
