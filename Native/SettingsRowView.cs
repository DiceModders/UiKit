using UnityEngine;

namespace DiceModders.UiKit.Native;

/// <summary>The native objects that currently represent one <see cref="SettingsRow"/> in a settings page.</summary>
internal sealed class SettingsRowView {
    public GameObject Go;
    public NativeToggle Toggle;
    public NativeSlider Slider;
    public NativeDropdown Dropdown;
    public NativeButton Button;

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
        if (Go != null) Object.Destroy(Go);
        Go = null;
    }
}
