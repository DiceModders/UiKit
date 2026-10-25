using System;
using System.Runtime.CompilerServices;
using UnityEngine.Events;

namespace DiceModders.UiKit.Custom;

/// <summary>UiWindow drag callbacks kept outside the injected component.</summary>
internal static class WindowDragRegistry {
    private sealed class Binding {
        public UnityAction? BringToFront;
        public UnityAction? DoubleClick;
    }

    private static readonly ConditionalWeakTable<WindowDragHandler, Binding> Table = new();

    public static void Bind(WindowDragHandler handler, Action? bringToFront, Action? doubleClick) {
        if (handler == null) return;

        Table.Remove(handler);
        Table.Add(handler, new Binding {
            BringToFront = EventRelay.ToUnity(bringToFront),
            DoubleClick = EventRelay.ToUnity(doubleClick),
        });
    }

    public static void BringToFront(WindowDragHandler handler) {
        if (handler != null && Table.TryGetValue(handler, out var binding)) binding.BringToFront?.Invoke();
    }

    public static void DoubleClick(WindowDragHandler handler) {
        if (handler != null && Table.TryGetValue(handler, out var binding)) binding.DoubleClick?.Invoke();
    }

    public static void Clear(WindowDragHandler handler) {
        if (handler != null) Table.Remove(handler);
    }
}
