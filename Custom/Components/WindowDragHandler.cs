using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DiceModders.UiKit.Custom;

/// <summary>
/// Drags a window by its empty background. Uses Unity's EventTrigger (the event-system handler interfaces are classes in Il2CppInterop),
/// so there is no input polling.
/// </summary>
internal sealed class WindowDragHandler : MonoBehaviour {
    public WindowDragHandler(IntPtr ptr) : base(ptr) { }

    private RectTransform _root;
    private float _minVisible = 8f;
    private bool _canDragNow = true;

    private bool _dragging;
    private bool _canDrag;
    private Canvas _canvas;
    private EventTrigger _trigger;
    private readonly List<UnityAction<BaseEventData>> _keep = new();

    public void Bind(RectTransform root, float minVisible) {
        _root = root;
        _minVisible = minVisible;
        _canDragNow = true;

        _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null) _canvas = _canvas.rootCanvas;

        if (_trigger != null) return;
        _trigger = gameObject.GetComponent<EventTrigger>() ?? gameObject.AddComponent<EventTrigger>();
        _trigger.triggers.Clear();

        EventTriggerBinder.Add(_trigger, _keep, EventTriggerType.PointerDown, EventRelay.ToEvent(OnPointerDown));
        EventTriggerBinder.Add(_trigger, _keep, EventTriggerType.BeginDrag, EventRelay.ToEvent(OnBeginDrag));
        EventTriggerBinder.Add(_trigger, _keep, EventTriggerType.Drag, EventRelay.ToEvent(OnDrag));
        EventTriggerBinder.Add(_trigger, _keep, EventTriggerType.EndDrag, EventRelay.ToEvent(OnEndDrag));
        EventTriggerBinder.Add(_trigger, _keep, EventTriggerType.PointerClick, EventRelay.ToEvent(OnPointerClick));
    }

    public void SetCanDrag(bool canDrag) {
        _canDragNow = canDrag;
        if (!canDrag) {
            _dragging = false;
            _canDrag = false;
        }
    }

    private static bool IsInteractive(GameObject hit) {
        if (hit == null) return false;
        if (hit.GetComponentInParent<Selectable>() != null) return true;
        if (hit.GetComponentInParent<ScrollRect>() != null) return true;
        return hit.GetComponentInParent<TMP_InputField>() != null;
    }

    private void OnPointerDown(BaseEventData data) {
        var pointer = data.TryCast<PointerEventData>();
        if (pointer == null || pointer.button != PointerEventData.InputButton.Left) return;

        WindowDragRegistry.BringToFront(this);
        _canDrag = !IsInteractive(pointer.pointerCurrentRaycast.gameObject);
    }

    private void OnBeginDrag(BaseEventData data) {
        var pointer = data.TryCast<PointerEventData>();
        _dragging = _canDrag && pointer != null && pointer.button == PointerEventData.InputButton.Left;
    }

    private void OnDrag(BaseEventData data) {
        var pointer = data.TryCast<PointerEventData>();
        if (!_dragging || _root == null || pointer == null) return;
        if (!_canDragNow) return;

        float scale = _canvas != null && _canvas.scaleFactor > 0.0001f ? _canvas.scaleFactor : 1f;
        var position = _root.anchoredPosition + pointer.delta / scale;
        ClampToParent(_root, ref position);
        _root.anchoredPosition = position;
    }

    private void OnEndDrag(BaseEventData data) {
        _dragging = false;
    }

    private void OnPointerClick(BaseEventData data) {
        var pointer = data.TryCast<PointerEventData>();
        if (pointer == null || pointer.button != PointerEventData.InputButton.Left) return;
        if (pointer.clickCount < 2) return;
        if (IsInteractive(pointer.pointerCurrentRaycast.gameObject)) return;
        WindowDragRegistry.DoubleClick(this);
    }

    private void OnDisable() {
        _dragging = false;
        _canDrag = false;
    }

    private void ClampToParent(RectTransform root, ref Vector2 position) {
        var parent = root.parent != null ? root.parent.TryCast<RectTransform>() : null;
        if (parent == null) return;

        var parentRect = parent.rect;
        var ownRect = root.rect;

        float minX = 0f;
        float maxX = Mathf.Max(0f, parentRect.width - ownRect.width);
        float minY = -Mathf.Max(0f, parentRect.height - _minVisible);
        float maxY = 0f;

        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.y = Mathf.Clamp(position.y, minY, maxY);
    }

    private void OnDestroy() {
        WindowDragRegistry.Clear(this);
        _keep.Clear();
        _root = null;
    }
}
