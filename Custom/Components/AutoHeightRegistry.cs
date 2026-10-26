using System;
using System.Runtime.CompilerServices;

namespace DiceModders.UiKit.Custom;

/// <summary>
/// Keeps the managed after-measure callback outside the injected <see cref="AutoHeightText"/> component. Il2CppInterop
/// cannot expose a method with a managed delegate parameter on a MonoBehaviour.
/// </summary>
internal static class AutoHeightRegistry {
    private sealed class Binding {
        public Action? Callback;
    }

    private static readonly ConditionalWeakTable<AutoHeightText, Binding> Table = new();

    public static void SetAfterMeasure(AutoHeightText? owner, Action? callback) {
        if (owner == null) return;

        if (callback == null) {
            Table.Remove(owner);
            return;
        }

        if (Table.TryGetValue(owner, out var binding)) binding.Callback = callback;
        else Table.Add(owner, new Binding { Callback = callback });
    }

    public static void Invoke(AutoHeightText? owner) {
        if (owner == null || !Table.TryGetValue(owner, out var binding)) return;
        UiGuard.Callback("auto-height", binding.Callback);
    }

    public static void Clear(AutoHeightText? owner) {
        if (owner != null) Table.Remove(owner);
    }
}
