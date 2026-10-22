using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiceModders.UiKit.Native;

internal enum SettingsRowKind {
    Header,
    Toggle,
    Slider,
    Dropdown,
    Button,
}

/// <summary>
/// A row in a mod-created settings tab. The row keeps its value and callback while the settings screen is rebuilt on a scene
/// change; only the native visual is recreated.
/// </summary>
public sealed class SettingsRow {
    private readonly SettingsTabHandle _tab;

    internal SettingsRow(SettingsTabHandle tab, SettingsRowKind kind, string label) {
        _tab = tab;
        Kind = kind;
        Label = label ?? string.Empty;
    }

    internal SettingsRowKind Kind { get; }
    internal string Label { get; }

    internal bool BoolValue;
    internal float FloatValue;
    internal float Min;
    internal float Max = 1f;
    internal string Format = "0.##";
    internal List<string> Options = new();
    internal int Index;
    internal string ButtonText = "Open";
    internal bool Enabled = true;
    internal bool Visible = true;

    internal Action<bool> BoolChanged;
    internal Action<float> FloatChanged;
    internal Action<int> IntChanged;
    internal Action Clicked;

    internal SettingsRowView View;

    internal bool IsAttached => View != null && View.IsAlive;

    internal void Attach(SettingsRowView view) {
        View = view;
        if (view == null) return;
        ApplyEnabled();
        if (view.Go != null) view.Go.SetActive(Visible);
    }

    internal void ResetView() => View = null;

    public SettingsRow SetValue(bool value) {
        if (Kind != SettingsRowKind.Toggle) return this;
        BoolValue = value;
        View?.SetBool(value);
        return this;
    }

    public SettingsRow SetValue(float value) {
        if (Kind != SettingsRowKind.Slider) return this;
        FloatValue = Mathf.Clamp(value, Min, Max);
        View?.SetFloat(FloatValue);
        return this;
    }

    public SettingsRow SetValue(int index) {
        if (Kind != SettingsRowKind.Dropdown) return this;
        Index = Mathf.Clamp(index, 0, Mathf.Max(0, Options.Count - 1));
        View?.SetIndex(Index);
        return this;
    }

    public SettingsRow SetEnabled(bool enabled = true) {
        Enabled = enabled;
        ApplyEnabled();
        return this;
    }

    public SettingsRow SetVisible(bool visible = true) {
        Visible = visible;
        if (View?.Go != null) View.Go.SetActive(visible);
        return this;
    }

    private void ApplyEnabled() => View?.SetEnabled(Enabled);
}
