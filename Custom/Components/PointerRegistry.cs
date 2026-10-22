using System;
using System.Runtime.CompilerServices;

namespace DiceModders.UiKit.Custom;

/// <summary>
/// Managed-side pointer callbacks for <see cref="PointerReceiver"/>. Keeping them out of the injected component
/// prevents IL2CPP from trying to expose managed delegates and the custom <see cref="PointerEvent"/> struct.
/// </summary>
internal static class PointerRegistry {
    private sealed class Handlers {
        public Action<PointerEvent> Pointer;
        public Action<float> Scroll;
    }

    private static readonly ConditionalWeakTable<PointerReceiver, Handlers> Table = new();

    public static void SetPointer(PointerReceiver owner, Action<PointerEvent> handler) {
        if (owner == null) return;

        if (!Table.TryGetValue(owner, out var handlers)) {
            handlers = new Handlers();
            Table.Add(owner, handlers);
        }

        handlers.Pointer = handler;
        if (handlers.Pointer == null && handlers.Scroll == null) Table.Remove(owner);
    }

    public static void SetScroll(PointerReceiver owner, Action<float> handler) {
        if (owner == null) return;

        if (!Table.TryGetValue(owner, out var handlers)) {
            handlers = new Handlers();
            Table.Add(owner, handlers);
        }

        handlers.Scroll = handler;
        if (handlers.Pointer == null && handlers.Scroll == null) Table.Remove(owner);
    }

    public static Action<PointerEvent> Pointer(PointerReceiver owner)
        => owner != null && Table.TryGetValue(owner, out var handlers) ? handlers.Pointer : null;

    public static Action<float> Scroll(PointerReceiver owner)
        => owner != null && Table.TryGetValue(owner, out var handlers) ? handlers.Scroll : null;

    public static void Clear(PointerReceiver owner) {
        if (owner != null) Table.Remove(owner);
    }
}
