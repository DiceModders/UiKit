using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace DiceModders.UiKit.Custom;

/// <summary>
/// Delivers pointer events through Unity's EventTrigger. Il2CppInterop exposes the event-system handler "interfaces" as classes,
/// so implementing them directly is not possible; EventTrigger is the supported path and still avoids any per-frame polling.
/// </summary>
internal sealed class PointerReceiver : MonoBehaviour {
    public PointerReceiver(IntPtr ptr) : base(ptr) { }

    public RectTransform? Area;

    private EventTrigger? _trigger;
    private readonly List<UnityAction<BaseEventData>> _keep = new();

    public void Bind(RectTransform? area) {
        Area = area;
        if (_trigger != null) return;

        var trigger = gameObject.GetComponent<EventTrigger>() ?? gameObject.AddComponent<EventTrigger>();
        _trigger = trigger;
        trigger.triggers.Clear();

        EventTriggerBinder.Add(trigger, _keep, EventTriggerType.PointerEnter, EventRelay.ToEvent(OnEnter));
        EventTriggerBinder.Add(trigger, _keep, EventTriggerType.PointerExit, EventRelay.ToEvent(OnExit));
        EventTriggerBinder.Add(trigger, _keep, EventTriggerType.PointerDown, EventRelay.ToEvent(OnDown));
        EventTriggerBinder.Add(trigger, _keep, EventTriggerType.PointerUp, EventRelay.ToEvent(OnUp));
        EventTriggerBinder.Add(trigger, _keep, EventTriggerType.PointerClick, EventRelay.ToEvent(OnClick));
        EventTriggerBinder.Add(trigger, _keep, EventTriggerType.BeginDrag, EventRelay.ToEvent(OnBeginDrag));
        EventTriggerBinder.Add(trigger, _keep, EventTriggerType.Drag, EventRelay.ToEvent(OnDrag));
        EventTriggerBinder.Add(trigger, _keep, EventTriggerType.EndDrag, EventRelay.ToEvent(OnEndDrag));
        EventTriggerBinder.Add(trigger, _keep, EventTriggerType.Scroll, EventRelay.ToEvent(OnScrollEvent));
    }

    private static Camera? CameraFor(RectTransform? rect) {
        if (rect == null) return null;
        var canvas = rect.GetComponentInParent<Canvas>();
        canvas = canvas != null ? canvas.rootCanvas : null;
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    private static float ScaleFor(RectTransform? rect) {
        if (rect == null) return 1f;
        var canvas = rect.GetComponentInParent<Canvas>();
        canvas = canvas != null ? canvas.rootCanvas : null;
        return canvas != null && canvas.scaleFactor > 0.0001f ? canvas.scaleFactor : 1f;
    }

    private void Raise(int kind, PointerEventData? data, int button) {
        var handler = PointerRegistry.Pointer(this);
        if (handler == null || Area == null || data == null) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Area, data.position, CameraFor(Area), out var local))
            return;

        var rect = Area.rect;
        var position = local - rect.min;
        var uv = new Vector2(
            rect.width > 0.001f ? position.x / rect.width : 0f,
            rect.height > 0.001f ? position.y / rect.height : 0f);
        var delta = data.delta / ScaleFor(Area);

        try { handler(new UiPointerEvent((UiPointerKind)kind, button, uv, position, delta)); }
        catch (Exception e) { UiLog.Error("image pointer handler: " + e); }
    }

    private void OnEnter(BaseEventData? data) => Raise((int)UiPointerKind.Enter, data?.TryCast<PointerEventData>(), -1);
    private void OnExit(BaseEventData? data) => Raise((int)UiPointerKind.Exit, data?.TryCast<PointerEventData>(), -1);

    private void OnDown(BaseEventData? data) {
        var pointer = data?.TryCast<PointerEventData>();
        Raise((int)UiPointerKind.Down, pointer, pointer != null ? (int)pointer.button : -1);
    }

    private void OnUp(BaseEventData? data) {
        var pointer = data?.TryCast<PointerEventData>();
        Raise((int)UiPointerKind.Up, pointer, pointer != null ? (int)pointer.button : -1);
    }

    private void OnClick(BaseEventData? data) {
        var pointer = data?.TryCast<PointerEventData>();
        Raise((int)UiPointerKind.Click, pointer, pointer != null ? (int)pointer.button : -1);
    }

    private void OnBeginDrag(BaseEventData? data) {
        var pointer = data?.TryCast<PointerEventData>();
        Raise((int)UiPointerKind.Drag, pointer, pointer != null ? (int)pointer.button : -1);
    }

    private void OnDrag(BaseEventData? data) {
        var pointer = data?.TryCast<PointerEventData>();
        Raise((int)UiPointerKind.Drag, pointer, pointer != null ? (int)pointer.button : -1);
    }

    private void OnEndDrag(BaseEventData? data) {
        var pointer = data?.TryCast<PointerEventData>();
        Raise((int)UiPointerKind.Up, pointer, pointer != null ? (int)pointer.button : -1);
    }

    private void OnScrollEvent(BaseEventData? data) {
        var handler = PointerRegistry.Scroll(this);
        if (handler == null) return;
        var pointer = data?.TryCast<PointerEventData>();
        if (pointer == null) return;
        try { handler(pointer.scrollDelta.y); }
        catch (Exception e) { UiLog.Error("image scroll handler: " + e); }
    }

    private void OnDestroy() {
        PointerRegistry.Clear(this);
        _keep.Clear();
    }
}
