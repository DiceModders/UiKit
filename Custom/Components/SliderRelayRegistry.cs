using System;
using System.Runtime.CompilerServices;
using UnityEngine.Events;
using USlider = UnityEngine.UI.Slider;

namespace DiceModders.UiKit.Custom;

/// <summary>Slider listeners kept outside the injected component, for the same reason as <see cref="ClickRelayRegistry"/>.</summary>
internal static class SliderRelayRegistry {
    private sealed class Binding {
        public USlider Slider;
        public UnityAction<float> Callback;
    }

    private static readonly ConditionalWeakTable<SliderRelay, Binding> Table = new();

    public static void Bind(SliderRelay relay, USlider slider, Action<float> action) {
        if (relay == null) return;

        Clear(relay);
        if (slider == null || action == null) return;

        var callback = EventRelay.ToUnity(action);
        slider.onValueChanged.AddListener(callback);
        Table.Add(relay, new Binding { Slider = slider, Callback = callback });
    }

    public static void Clear(SliderRelay relay) {
        if (relay == null) return;
        if (!Table.TryGetValue(relay, out var binding)) return;

        Table.Remove(relay);
        if (binding.Slider != null) binding.Slider.onValueChanged.RemoveListener(binding.Callback);
    }
}
