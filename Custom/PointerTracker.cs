using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DiceModders.UiKit.Custom;

/// <summary>
/// Delivers mouse movement and the wheel to widgets that need them. The Input System actions exist only while at
/// least one widget is registered and invoke callbacks on input changes, so there is no per-frame polling.
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
    private static InputAction? _moveAction;
    private static InputAction? _scrollAction;
    private static Il2CppSystem.Action<InputAction.CallbackContext>? _moveHandler;
    private static Il2CppSystem.Action<InputAction.CallbackContext>? _scrollHandler;
    private static bool _inputErrorLogged;
    private static int _dispatchDepth;

    public static Entry Add(UnityEngine.Object? owner, Func<Vector2, bool>? hover, Func<float, bool>? wheel,
                            Action<Vector2>? move, int wheelPriority = 0) {
        var entry = new Entry {
            Owner = owner, Hover = hover, Wheel = wheel, Move = move, WheelPriority = wheelPriority,
        };
        Entries.Add(entry);
        EnsureActions();
        return entry;
    }

    public static void Remove(Entry? entry) {
        if (entry == null) return;
        Entries.Remove(entry);
        DisposeIfIdle();
    }

    public static void Clear() {
        Entries.Clear();
        DisposeActions();
    }

    private static void DisposeIfIdle() {
        if (Entries.Count != 0) return;
        if (_dispatchDepth > 0) {
            // Never tear down an Input System action from inside its own callback; do it after input dispatch.
            LayoutOnce.Request(DisposeIfIdle);
            return;
        }
        DisposeActions();
    }

    private static void EndDispatch() {
        _dispatchDepth--;
        if (_dispatchDepth == 0 && Entries.Count == 0) LayoutOnce.Request(DisposeIfIdle);
    }

    private static void EnsureActions() {
        if (_moveAction != null && _scrollAction != null) return;

        try {
            if (_moveAction == null) {
                _moveAction = new InputAction("UiKitPointerMove", InputActionType.PassThrough, "<Mouse>/position");
                _moveHandler = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<InputAction.CallbackContext>>(OnMovePerformed);
                if (_moveHandler == null) throw new InvalidOperationException("could not create the pointer move callback");
                _moveAction.add_performed(_moveHandler);
                _moveAction.Enable();
            }

            if (_scrollAction == null) {
                _scrollAction = new InputAction("UiKitPointerScroll", InputActionType.PassThrough, "<Mouse>/scroll");
                _scrollHandler = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<InputAction.CallbackContext>>(OnScrollPerformed);
                if (_scrollHandler == null) throw new InvalidOperationException("could not create the pointer scroll callback");
                _scrollAction.add_performed(_scrollHandler);
                _scrollAction.Enable();
            }
        }
        catch (Exception e) {
            DisposeActions();
            if (!_inputErrorLogged) {
                _inputErrorLogged = true;
                UiLog.Warn("pointer input could not be initialized; image Move and wheel callbacks are disabled: " + e.Message);
            }
        }
    }

    private static void DisposeActions() {
        var move = _moveAction;
        var scroll = _scrollAction;
        var moveHandler = _moveHandler;
        var scrollHandler = _scrollHandler;
        _moveAction = null;
        _scrollAction = null;
        _moveHandler = null;
        _scrollHandler = null;

        if (move != null) {
            try {
                if (moveHandler != null) move.remove_performed(moveHandler);
                move.Disable();
                move.Dispose();
            }
            catch (Exception e) { UiLog.Once("pointer:move-dispose", "could not dispose pointer move action: " + e.Message); }
        }

        if (scroll != null) {
            try {
                if (scrollHandler != null) scroll.remove_performed(scrollHandler);
                scroll.Disable();
                scroll.Dispose();
            }
            catch (Exception e) { UiLog.Once("pointer:scroll-dispose", "could not dispose pointer scroll action: " + e.Message); }
        }
    }

    private static void OnMovePerformed(InputAction.CallbackContext context) {
        try {
            Vector2 position = context.ReadValue<Vector2>();
            MoveAt(position);
        }
        catch (Exception e) {
            UiLog.Once("pointer:move", "pointer move input failed: " + e.Message);
        }
    }

    private static void OnScrollPerformed(InputAction.CallbackContext context) {
        try {
            var mouse = Mouse.current;
            if (mouse == null) return;

            float rawWheel = context.ReadValue<Vector2>().y;
            float notches = rawWheel == 0f ? 0f : (Mathf.Abs(rawWheel) >= 20f ? rawWheel / 120f : rawWheel);
            if (notches == 0f) return;

            WheelAt(mouse.position.ReadValue(), notches);
        }
        catch (Exception e) {
            UiLog.Once("pointer:wheel", "pointer wheel input failed: " + e.Message);
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
