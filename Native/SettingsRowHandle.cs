using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace DiceModders.UiKit.Native;

internal enum SettingsRowKind {
    Header,
    Toggle,
    Slider,
    Dropdown,
    Button,
    Buttons,
    Stepper,
    Info,
}

internal readonly struct SettingsButtonDefinition {
    public SettingsButtonDefinition(string? text, Action? clicked) {
        Text = string.IsNullOrWhiteSpace(text) ? "OK" : text!;
        Clicked = clicked;
    }

    public string Text { get; }
    public Action? Clicked { get; }
}

/// <summary>
/// A row in a mod-created settings tab or in a block added to one of the game's built-in Settings tabs. The row keeps its
/// value and callback while the settings screen is rebuilt on a scene change; only the native visual is recreated.
/// </summary>
public sealed class SettingsRowHandle {
    private readonly string _ownerId;

    internal SettingsRowHandle(string ownerId, SettingsRowKind kind, string? label) {
        _ownerId = ownerId;
        Kind = kind;
        Label = label ?? string.Empty;
    }

    internal SettingsRowKind Kind { get; }
    internal string Label { get; }

    internal bool BoolValue;
    internal float FloatValue;
    internal float Min;
    internal float Max = 1f;
    internal bool WholeNumbers;
    internal string Format = "0.##";
    internal List<string> Options = new();
    internal int Index;
    internal string ButtonText = "Open";
    internal bool Enabled = true;
    internal bool Visible = true;
    internal string Text = string.Empty;
    internal Func<string>? TextProvider;

    internal List<SettingsButtonDefinition> Buttons = new();

    internal Action<bool>? BoolChanged;
    internal Action<float>? FloatChanged;
    internal Action<int>? IntChanged;
    internal Action? Clicked;

    internal SettingsRowView? View;

    /// <summary>True while this row has a live native control.</summary>
    public bool IsAttached => View != null && View.IsAlive;

    internal void Attach(SettingsRowView? view) {
        View = view;
        if (view == null) return;
        UiGuard.Run("settings.row.text", RefreshTextInternal, _ownerId);
        UiGuard.Run("settings.row.enabled", ApplyEnabled, _ownerId);
        UiGuard.Run("settings.row.visible", () => {
            if (view.Go != null) view.Go.SetActive(Visible);
        }, _ownerId);
    }

    internal void ResetView() => View = null;

    internal void RefreshText() => UiGuard.Run("settings.row.text", RefreshTextInternal, _ownerId);

    internal string ReadText() {
        if (TextProvider == null) return Text;
        try { return TextProvider() ?? string.Empty; }
        catch (Exception e) {
            UiLog.Once("settings:text:" + _ownerId, "settings value text: " + e.Message);
            return Text;
        }
    }

    private void RefreshTextInternal() {
        if (Kind is not (SettingsRowKind.Info or SettingsRowKind.Stepper)) return;
        string text = ReadText();
        View?.SetText(text);
    }

    public SettingsRowHandle SetValue(bool value) {
        if (Kind != SettingsRowKind.Toggle) return this;
        BoolValue = value;
        UiGuard.Run("settings.row.value", () => View?.SetBool(value), _ownerId);
        return this;
    }

    public SettingsRowHandle SetValue(float value) {
        if (Kind != SettingsRowKind.Slider) return this;
        float next = Mathf.Clamp(UiGuard.Finite(value, FloatValue), Min, Max);
        if (WholeNumbers) next = Mathf.Round(next);
        FloatValue = next;
        UiGuard.Run("settings.row.value", () => View?.SetFloat(FloatValue), _ownerId);
        return this;
    }

    public SettingsRowHandle SetValue(int index) {
        if (Kind != SettingsRowKind.Dropdown) return this;
        Index = Mathf.Clamp(index, 0, Mathf.Max(0, Options.Count - 1));
        UiGuard.Run("settings.row.value", () => View?.SetIndex(Index), _ownerId);
        return this;
    }

    /// <summary>Updates the displayed text of an Info or Stepper row. The stepper's value provider still wins on its next refresh.</summary>
    public SettingsRowHandle SetText(string? text) {
        if (Kind is not (SettingsRowKind.Info or SettingsRowKind.Stepper)) return this;
        Text = text ?? string.Empty;
        UiGuard.Run("settings.row.text", () => View?.SetText(Text), _ownerId);
        return this;
    }

    public SettingsRowHandle SetEnabled(bool enabled = true) {
        Enabled = enabled;
        UiGuard.Run("settings.row.enabled", ApplyEnabled, _ownerId);
        return this;
    }

    public SettingsRowHandle SetVisible(bool visible = true) {
        Visible = visible;
        UiGuard.Run("settings.row.visible", () => {
            if (View?.Go != null) View.Go.SetActive(visible);
        }, _ownerId);
        return this;
    }

    private void ApplyEnabled() => View?.SetEnabled(Enabled);
}
