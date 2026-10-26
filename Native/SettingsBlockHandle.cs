using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using GameLabel = DiceKingdoms.Interface.Label;
using GameSettingSet = DiceKingdoms.Meta.SettingSetInterface;

namespace DiceModders.UiKit.Native;

/// <summary>
/// A stable block of rows appended to one of the game's built-in Settings pages. The block lives across Settings rebuilds
/// and scene changes; only its native rows are recreated. Values and callbacks stay in the handle.
/// </summary>
public sealed class SettingsBlockHandle : NativeHandle {
    private readonly UiScope _scope;
    private readonly NativeUiHost _host;
    private readonly Action<SettingsBlockHandle> _onDispose;
    private readonly List<SettingsRowHandle> _rows = new();

    private GameSettingSet? _pageSet;
    private SettingsRowHost? _rowHost;

    internal SettingsBlockHandle(UiScope scope, NativeUiHost host, string id, SettingsPage page, string? title,
                                 int order, int sequence, Action<SettingsBlockHandle> onDispose)
        : base(scope, host, id) {
        _scope = scope;
        _host = host;
        _onDispose = onDispose;
        Page = page;
        Title = string.IsNullOrWhiteSpace(title) ? null : title;
        Order = order;
        Sequence = sequence;

        // Match the game's own block layout: a non-empty title is a group header above the first row.
        if (Title != null) _rows.Add(new SettingsRowHandle(_scope.Id, SettingsRowKind.Header, Title));
    }

    public SettingsPage Page { get; }
    public string? Title { get; }
    public int Order { get; }
    internal int Sequence { get; }

    /// <summary>True when at least one row exists in the block.</summary>
    public bool HasRows => _rows.Count > 0;

    internal bool HasAttachedRows {
        get {
            for (int i = 0; i < _rows.Count; i++) {
                if (_rows[i].IsAttached) return true;
            }
            return false;
        }
    }

    internal bool IsPageAlive => _pageSet != null && _pageSet.holder != null && _rowHost != null && _rowHost.IsAlive;

    internal bool IsOnPage(GameSettingSet? set)
        => set != null && _pageSet != null && _pageSet.Pointer == set.Pointer;

    internal IReadOnlyList<SettingsRowHandle> Rows => _rows;

    public SettingsRowHandle? AddHeader(string? text) => AddRow(SettingsRowKind.Header, text, row => { });

    public SettingsRowHandle? AddToggle(string? label, bool value, Action<bool>? changed)
        => AddRow(SettingsRowKind.Toggle, label, row => {
            row.BoolValue = value;
            row.BoolChanged = changed;
        });

    public SettingsRowHandle? AddSlider(string? label, float min, float max, float value, Action<float>? changed,
                                        string? format = "0.##", bool wholeNumbers = false)
        => AddRow(SettingsRowKind.Slider, label, row => {
            row.Min = UiGuard.Finite(min, 0f);
            row.Max = UiGuard.Finite(max, row.Min + 1f);
            if (row.Max <= row.Min) row.Max = row.Min + 1f;
            row.FloatValue = Mathf.Clamp(UiGuard.Finite(value, row.Min), row.Min, row.Max);
            row.WholeNumbers = wholeNumbers;
            if (wholeNumbers) row.FloatValue = Mathf.Round(row.FloatValue);
            row.Format = SafeFormat(format);
            row.FloatChanged = changed;
        });

    public SettingsRowHandle? AddDropdown(string? label, IEnumerable<string>? options, int index, Action<int>? changed)
        => AddRow(SettingsRowKind.Dropdown, label, row => {
            try { row.Options = Normalize(options); }
            catch (Exception e) {
                UiGuard.Report("settings.dropdown-options", e, _scope.Id);
                row.Options = new List<string> { string.Empty };
            }
            row.Index = Mathf.Clamp(index, 0, Mathf.Max(0, row.Options.Count - 1));
            row.IntChanged = changed;
        });

    public SettingsRowHandle? AddButton(string? label, string? buttonText, Action? clicked)
        => AddRow(SettingsRowKind.Button, label, row => {
            row.ButtonText = string.IsNullOrWhiteSpace(buttonText) ? "Open" : buttonText!;
            row.Clicked = clicked;
        });

    /// <summary>A row with a label and several buttons. Each button is sized to fit the row's available width.</summary>
    public SettingsRowHandle? AddButtons(string? label, params (string text, Action? clicked)[] buttons)
        => AddRow(SettingsRowKind.Buttons, label, row => {
            if (buttons != null) {
                foreach (var button in buttons) row.Buttons.Add(new SettingsButtonDefinition(button.text, button.clicked));
            }
            if (row.Buttons.Count == 0) row.Buttons.Add(new SettingsButtonDefinition("OK", null));
        });

    /// <summary>
    /// A "-" / value / "+" row. The value text provider is read when the row is attached and after each step; it is not
    /// polled every frame.
    /// </summary>
    public SettingsRowHandle? AddStepper(string? label, Func<string>? valueText, Action<int>? step)
        => AddRow(SettingsRowKind.Stepper, label, row => {
            row.TextProvider = valueText;
            row.Text = valueText == null ? string.Empty : SafeRead(valueText, _scope.Id);
            row.IntChanged = step;
        });

    /// <summary>A wrapped paragraph. Use <see cref="SettingsRowHandle.SetText"/> to change it later.</summary>
    public SettingsRowHandle? AddInfo(string? text)
        => AddRow(SettingsRowKind.Info, null, row => row.Text = text ?? string.Empty);

    private SettingsRowHandle? AddRow(SettingsRowKind kind, string? label, Action<SettingsRowHandle> configure) {
        if (IsDisposed) {
            UiLog.OnceError("settings:block-disposed", "cannot add a settings row to a disposed block");
            return null;
        }

        var row = new SettingsRowHandle(_scope.Id, kind, label);
        configure(row);
        _rows.Add(row);
        if (_rowHost != null && _rowHost.IsAlive) {
            BuildRow(row);
            UiGuard.Run("settings.block.reorder", () => _host.ReorderSettingsBlocks(_pageSet), _scope.Id);
            UpdateChrome(_pageSet);
        }
        return row;
    }

    internal override void Refresh(NativeScene scene) {
        if (IsDisposed || State == NativeState.Unsupported) return;

        GameSettingSet? set;
        try { set = SettingsPages.Find(Page); }
        catch (Exception e) {
            UiGuard.Report("settings.block.find", e, _scope.Id);
            MarkFailed("could not find the requested Settings page");
            return;
        }

        if (set == null) {
            UiLog.Once("settings:block-missing:" + Id, $"settings block {Id} is waiting for the {Page} page");
            DetachRows();
            MarkWaiting("waiting for the " + Page + " Settings page");
            return;
        }

        if (CanKeep(set)) {
            MarkAttached("settings");
            return;
        }

        try { AttachToPage(set, false); }
        catch (Exception e) {
            UiGuard.Report("settings.block.attach", e, _scope.Id);
            DetachRows();
            MarkFailed("settings block could not be attached");
            UpdateChrome(set);
            return;
        }

        if (_rowHost != null) MarkAttached("settings");
        else {
            MarkFailed("settings page templates are unavailable");
            UpdateChrome(set);
        }
    }

    internal void OnPageGenerated(GameSettingSet set) {
        if (IsDisposed || State == NativeState.Unsupported || set == null) return;

        try { AttachToPage(set, true); }
        catch (Exception e) {
            UiGuard.Report("settings.block.rebuild", e, _scope.Id);
            DetachRows();
            MarkFailed("settings block could not be rebuilt");
            UpdateChrome(set);
            return;
        }

        if (_rowHost != null) MarkAttached("settings");
        else {
            MarkFailed("settings page templates are unavailable");
            UpdateChrome(set);
        }
    }

    private bool CanKeep(GameSettingSet set) {
        if (_pageSet == null || _rowHost == null || !_rowHost.IsAlive) return false;
        if (_pageSet.Pointer != set.Pointer) return false;
        for (int i = 0; i < _rows.Count; i++) {
            if (!_rows[i].IsAttached) return false;
        }
        return true;
    }

    private void AttachToPage(GameSettingSet set, bool force) {
        if (set == null || set.holder == null) throw new InvalidOperationException("settings page has no holder");
        if (!force && CanKeep(set)) return;

        // Drop rows from a previous page visit before validating the new page. If the new page is incomplete, the old
        // views are stale and must not keep the page's chrome alive.
        DetachRows();

        var entryPrefab = set.settingEntryInterfacePrefab;
        var headerPrefab = set.headerPrefab;
        if (entryPrefab == null) {
            UiLog.OnceError("settings:block-entry", "settings page has no row prefab");
            return;
        }

        float contentLeft = SettingsPageChrome.MeasureContentLeft(set);
        var host = new SettingsRowHost(_scope, set.holder, entryPrefab, headerPrefab, contentLeft);
        _pageSet = set;
        _rowHost = host;

        foreach (var row in _rows) BuildRow(row);
        _host.ReorderSettingsBlocks(set);
        UpdateChrome(set);
        LayoutRebuilder.MarkLayoutForRebuild(set.holder);
    }

    private void UpdateChrome(GameSettingSet? set) {
        if (set == null) return;
        bool hasRows = HasAttachedRows || _host.HasAttachedSettingsRows(Page, this);
        UiGuard.Run("settings.block.chrome", () => SettingsPageChrome.Apply(set, hasRows), _scope.Id);
    }

    private void BuildRow(SettingsRowHandle row) {
        var host = _rowHost;
        if (host == null || row == null) return;

        SettingsRowView? view;
        try { view = host.Build(row); }
        catch (Exception e) {
            UiGuard.Report("settings.block.row", e, _scope.Id);
            return;
        }
        if (view == null) return;

        try {
            row.Attach(view);
            host.Add(view);
        }
        catch (Exception e) {
            UiGuard.Report("settings.block.row.attach", e, _scope.Id);
            row.ResetView();
            view.Destroy();
        }
    }

    private void DetachRows() {
        var host = _rowHost;
        _rowHost = null;
        _pageSet = null;
        for (int i = 0; i < _rows.Count; i++) _rows[i].ResetView();
        if (host != null) UiGuard.Run("settings.block.detach", host.Dispose, _scope.Id);
    }

    protected override void DetachCore() {
        DetachRows();
    }

    protected override void DisposeCore() {
        UiGuard.Run("settings.block.on-dispose", () => _onDispose?.Invoke(this), _scope.Id);
        try { _scope.Runtime.NativeUi.OnSettingsBlockDisposed(this); }
        catch (Exception e) { UiLog.OnceError("settings:block-dispose", "settings block disposal failed: " + e); }
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
