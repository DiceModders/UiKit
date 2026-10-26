using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace DiceModders.UiKit;

/// <summary>
/// Runs work once after the next Unity canvas layout pass. Requests are coalesced and the static
/// <see cref="Canvas.willRenderCanvases"/> subscription is removed after the pass, so this is not a persistent ticker.
/// </summary>
internal static class LayoutOnce {
    private static readonly List<Action> Pending = new();
    private static Canvas.WillRenderCanvases? _handler;
    private static bool _scheduled;
    private static bool _enabled = true;

    public static void Begin() => _enabled = true;

    public static void Request(Action? action) {
        if (!_enabled || action == null) return;
        Pending.Add(action);
        if (_scheduled) return;

        try {
            Action callback = Flush;
            var handler = DelegateSupport.ConvertDelegate<Canvas.WillRenderCanvases>(callback);
            if (handler == null) {
                Flush();
                return;
            }

            _handler = handler;
            _scheduled = true;
            Canvas.willRenderCanvases += handler;
        }
        catch (Exception e) {
            _handler = null;
            _scheduled = false;
            UiLog.Once("layout:once", "could not schedule a layout callback: " + e.Message);
            Flush();
        }
    }

    public static void Clear() {
        _enabled = false;
        Unsubscribe();
        Pending.Clear();
    }

    private static void Flush() {
        Unsubscribe();

        var actions = Pending.ToArray();
        Pending.Clear();
        for (int i = 0; i < actions.Length; i++) {
            UiGuard.Callback("layout.once", actions[i]);
        }
    }

    private static void Unsubscribe() {
        var handler = _handler;
        _handler = null;
        _scheduled = false;
        if (handler == null) return;

        try { Canvas.willRenderCanvases -= handler; }
        catch (Exception e) { UiLog.Once("layout:unsubscribe", "could not remove a layout callback: " + e.Message); }
    }
}
