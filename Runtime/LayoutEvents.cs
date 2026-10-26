using System;
using DiceModders.UiKit.Custom;
using UnityEngine;

namespace DiceModders.UiKit;

/// <summary>
/// The library's layout-change signal. A watcher is attached only to a root canvas; it forwards Unity's
/// <c>OnRectTransformDimensionsChange</c> through one coalesced canvas-layout callback.
/// </summary>
internal static class LayoutEvents {
    internal static event Action? CanvasChanged;
    private static bool _active;

    public static void Activate() {
        _active = true;
        CanvasChanged = null;
    }

    public static void RequestCanvasChanged() {
        if (!_active) return;
        LayoutOnce.Request(NotifyCanvasChanged);
    }

    public static void Watch(Transform? transform) {
        if (!_active || transform == null) return;

        try {
            var go = transform.gameObject;
            if (go == null || go.GetComponent<CanvasLayoutWatcher>() != null) return;
            go.AddComponent<CanvasLayoutWatcher>();
        }
        catch (Exception e) {
            UiLog.Once("layout:watch", "could not watch a canvas for size changes: " + e.Message);
        }
    }

    public static void Clear() {
        _active = false;
        CanvasChanged = null;
    }

    private static void NotifyCanvasChanged() {
        var handlers = CanvasChanged;
        if (handlers == null) return;

        foreach (var handler in handlers.GetInvocationList()) {
            try { ((Action)handler)(); }
            catch (Exception e) { UiLog.Error("canvas layout listener failed: " + e.Message); }
        }
    }
}
