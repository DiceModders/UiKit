using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DiceModders.UiKit.Custom;

/// <summary>
/// Delivers mouse movement and the wheel to widgets that need them. The Input System update callback exists only while
/// at least one widget is registered and delivers position/wheel deltas without <c>InputAction.CallbackContext</c>
/// marshalling, which Il2CppInterop cannot convert.
/// </summary>
internal static class PointerTracker {
    internal sealed class Entry {
        public UnityEngine.Object? Owner;
        public Func<Vector2, bool>? Hover;
        public Func<float, bool>? Wheel;
        public Action<Vector2>? Move;
        public int WheelPriority;
    }

    private static readonly List<Entry> Entries = new();
    private static Il2CppSystem.Action? _updateHandler;
    private static Vector2 _lastPosition;
    private static bool _hasLastPosition;
    private static bool _inputErrorLogged;
    private static int _dispatchDepth;

    public static Entry Add(UnityEngine.Object? owner, Func<Vector2, bool>? hover, Func<float, bool>? wheel,
                            Action<Vector2>? move, int wheelPriority = 0) {
        var entry = new Entry {
            Owner = owner, Hover = hover, Wheel = wheel, Move = move, WheelPriority = wheelPriority,
        };
        Entries.Add(entry);
        EnsureInput();
        return entry;
    }

    public static void Remove(Entry? entry) {
        if (entry == null) return;
        Entries.Remove(entry);
        DisposeIfIdle();
    }

    public static void Clear() {
        Entries.Clear();
        DisposeInput();
    }

    private static void DisposeIfIdle() {
        if (Entries.Count != 0) return;
        if (_dispatchDepth > 0) {
            // Never unsubscribe from inside the Input System callback; do it after input dispatch.
            LayoutOnce.Request(DisposeIfIdle);
            return;
        }
        DisposeInput();
    }

    private static void EndDispatch() {
        _dispatchDepth--;
        if (_dispatchDepth == 0 && Entries.Count == 0) LayoutOnce.Request(DisposeIfIdle);
    }

    private static void EnsureInput() {
        if (_updateHandler != null || Entries.Count == 0) return;

        try {
            var handler = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(OnInputAfterUpdate);
            if (handler == null) throw new InvalidOperationException("could not create the pointer update callback");
            InputSystem.add_onAfterUpdate(handler);
            _updateHandler = handler;
        }
        catch (Exception e) {
            DisposeInput();
            if (!_inputErrorLogged) {
                _inputErrorLogged = true;
                UiLog.Warn("pointer input could not be initialized; image Move and wheel callbacks are disabled: " + e.Message);
            }
        }
    }

    private static void DisposeInput() {
        var handler = _updateHandler;
        _updateHandler = null;
        _hasLastPosition = false;
        _lastPosition = default;
        if (handler == null) return;

        try { InputSystem.remove_onAfterUpdate(handler); }
        catch (Exception e) { UiLog.Once("pointer:input-dispose", "could not remove the pointer update callback: " + e.Message); }
    }

    private static void OnInputAfterUpdate() {
        try {
            if (Entries.Count == 0) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 position = mouse.position.ReadValue();
            if (!_hasLastPosition || position != _lastPosition) {
                _hasLastPosition = true;
                _lastPosition = position;
                MoveAt(position);
            }

            float rawWheel = mouse.scroll.ReadValue().y;
            if (rawWheel == 0f) return;

            float notches = Mathf.Abs(rawWheel) >= 20f ? rawWheel / 120f : rawWheel;
            if (notches != 0f) WheelAt(position, notches);
        }
        catch (Exception e) {
            UiLog.Once("pointer:input", "pointer input failed: " + e.Message);
        }
    }

    private static void MoveAt(Vector2 position) {
        _dispatchDepth++;
        try {
            for (int i = Entries.Count - 1; i >= 0; i--) {
                var entry = Entries[i];
                if (entry.Owner == null) {
                    Entries.RemoveAt(i);
                    continue;
                }
                if (entry.Move == null) continue;

                bool hovered;
                try { hovered = entry.Hover != null && entry.Hover(position); }
                catch { hovered = false; }
                if (!hovered) continue;

                try { entry.Move(position); }
                catch (Exception e) { UiLog.Once("pointer:move", "pointer move handler: " + e); }
            }
        }
        finally { EndDispatch(); }
    }

    private static void WheelAt(Vector2 position, float notches) {
        _dispatchDepth++;
        try {
            var candidates = new List<Entry>();
            for (int i = Entries.Count - 1; i >= 0; i--) {
                var entry = Entries[i];
                if (entry.Owner == null) {
                    Entries.RemoveAt(i);
                    continue;
                }
                if (entry.Wheel == null) continue;

                bool hovered;
                try { hovered = entry.Hover != null && entry.Hover(position); }
                catch { hovered = false; }
                if (hovered) candidates.Add(entry);
            }

            // Lower priority values win; for equal priorities the most recently registered inner view wins. A handler
            // that declines the wheel (for example an image with no OnScroll handler) passes it to the next candidate.
            while (candidates.Count > 0) {
                int best = 0;
                for (int i = 1; i < candidates.Count; i++) {
                    if (candidates[i].WheelPriority < candidates[best].WheelPriority) best = i;
                }

                var entry = candidates[best];
                candidates.RemoveAt(best);
                try {
                    if (entry.Wheel != null && entry.Wheel(notches)) return;
                }
                catch (Exception e) { UiLog.Once("pointer:wheel", "wheel handler: " + e); }
            }
        }
        finally { EndDispatch(); }
    }

    public static Camera? CameraFor(RectTransform? rect) {
        if (rect == null) return null;
        var canvas = rect.GetComponentInParent<Canvas>();
        canvas = canvas != null ? canvas.rootCanvas : null;
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }
}
