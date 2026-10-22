using System;
using System.Runtime.CompilerServices;
using UnityEngine.Events;
using UButton = UnityEngine.UI.Button;

namespace DiceModders.UiKit.Custom;

/// <summary>
/// Button listeners kept outside the injected component. This keeps managed <see cref="Action"/> out of the component's
/// method signatures, which IL2CPP class injection cannot proxy.
/// </summary>
internal static class ClickRelayRegistry {
    private sealed class Binding {
        public UButton Button;
        public UnityAction Callback;
    }

    private static readonly ConditionalWeakTable<ClickRelay, Binding> Table = new();

    public static void Bind(ClickRelay relay, UButton button, Action action) {
        if (relay == null) return;

        Clear(relay);
        if (button == null || action == null) return;

        var callback = EventRelay.ToUnity(action);
        button.onClick.AddListener(callback);
        Table.Add(relay, new Binding { Button = button, Callback = callback });
    }

    public static void Clear(ClickRelay relay) {
        if (relay == null) return;
        if (!Table.TryGetValue(relay, out var binding)) return;

        Table.Remove(relay);
        if (binding.Button != null) binding.Button.onClick.RemoveListener(binding.Callback);
    }
}
