using System;
using UnityEngine;

namespace DiceModders.UiKit.Native;

/// <summary>The native objects that currently represent one <see cref="SettingsRow"/> in a settings page.</summary>
internal sealed class SettingsRowView {
    public GameObject? Go;
    public NativeToggle? Toggle;
    public NativeSlider? Slider;
    public NativeDropdown? Dropdown;
    public NativeButton? Button;

    public bool IsAlive => Go != null;

    public void SetEnabled(bool enabled) {
        Toggle?.SetInteractable(enabled);
        Dropdown?.SetInteractable(enabled);
        Button?.SetInteractable(enabled);
        if (Slider?.Slider != null) Slider.Slider.interactable = enabled;
    }

    public void SetBool(bool value) => Toggle?.SetSilently(value);
    public void SetFloat(float value) => Slider?.SetValueSilently(value);
    public void SetIndex(int index) => Dropdown?.SetSilently(index);

    public void Destroy() {
        Escape();
        if (Go == null) return;
        try { UnityEngine.Object.Destroy(Go); }
        catch (Exception e) { UiLog.Error("settings row cleanup failed: " + e.Message); }
        Go = null;
    }

    private void Escape() {
        Toggle = null;
        Slider = null;
        Dropdown = null;
        Button = null;
    }
}
