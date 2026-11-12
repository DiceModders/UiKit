using System;
using UnityEngine;

namespace DiceModders.UiKit.Custom;

/// <summary>
/// Reports a new size of its RectTransform once, after the canvas layout pass that produced it. Unity calls
/// <c>OnRectTransformDimensionsChange</c> only on a real change, so nothing runs while the size stays the same.
/// </summary>
internal sealed class SizeWatcher : MonoBehaviour {
    public SizeWatcher(IntPtr ptr) : base(ptr) { }

    private Action<Vector2>? _changed;
    private Vector2 _last = new(-1f, -1f);
    private bool _queued;

    public void Bind(Action<Vector2>? changed) {
        _changed = changed;
        _last = new Vector2(-1f, -1f);
        Queue();
    }

    private void OnEnable() => Queue();

    private void OnRectTransformDimensionsChange() => Queue();

    private void Queue() {
        if (_queued || _changed == null) return;
        _queued = true;
        LayoutOnce.Request(Report);
    }

    private void Report() {
        _queued = false;
        if (this == null || _changed == null) return;
        var rect = GetComponent<RectTransform>();
        if (rect == null || !gameObject.activeInHierarchy) return;
        var size = rect.rect.size;
        if (Mathf.Abs(size.x - _last.x) < 0.5f && Mathf.Abs(size.y - _last.y) < 0.5f) return;
        _last = size;
        try { _changed(size); }
        catch (Exception e) { UiLog.Error("image resize handler: " + e); }
    }

    private void OnDestroy() => _changed = null;
}
