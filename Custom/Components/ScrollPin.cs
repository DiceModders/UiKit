using System;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DiceModders.UiKit.Custom;

/// <summary>
/// Keeps a scroll view at the top while its content is still growing, until the user scrolls.
/// Reacts to layout changes and to the scrollbar; no per-frame update.
/// </summary>
internal sealed class ScrollPin : MonoBehaviour {
    public ScrollPin(IntPtr ptr) : base(ptr) { }

    private ScrollRect _scroll;
    private UnityAction<Vector2> _handler;
    private bool _pinned = true;
    private bool _applying;

    public void Bind(ScrollRect scroll) {
        if (scroll == null) return;
        _scroll = scroll;
        _handler = DelegateSupport.ConvertDelegate<UnityAction<Vector2>>(OnScroll);
        scroll.onValueChanged.AddListener(_handler);
    }

    private void OnScroll(Vector2 position) {
        if (_applying || _scroll == null) return;
        _pinned = _scroll.verticalNormalizedPosition > 0.999f;
    }

    private void OnRectTransformDimensionsChange() {
        if (_scroll == null || !_pinned) return;

        _applying = true;
        _scroll.verticalNormalizedPosition = 1f;
        _applying = false;
    }

    private void OnDestroy() {
        if (_scroll != null && _handler != null) _scroll.onValueChanged.RemoveListener(_handler);
        _scroll = null;
        _handler = null;
    }
}
