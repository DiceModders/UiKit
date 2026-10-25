using System;
using UnityEngine;
using UnityEngine.UI;

namespace DiceModders.UiKit.Custom;

/// <summary>Base of every custom widget. Owns one RectTransform and can be disposed like the window that contains it.</summary>
public abstract class UiElement : IDisposable {
    private RectTransform? _root;
    private LayoutElement? _layout;
    private bool _disposed;

    protected UiElement(RectTransform root) => _root = root;

    /// <summary>The widget root, or <c>null</c> after the widget has been disposed.</summary>
    public RectTransform? Root => _root;
    public bool IsAlive => !_disposed && _root != null && _root.gameObject != null;

    internal LayoutElement? Layout {
        get {
            if (_layout != null) return _layout;
            if (!IsAlive || _root == null) return null;
            _layout = UiGuard.Func("element.layout", () =>
                _root.gameObject.GetComponent<LayoutElement>() ?? _root.gameObject.AddComponent<LayoutElement>());
            return _layout;
        }
    }

    public virtual void Dispose() {
        if (_disposed) return;
        _disposed = true;

        if (_root != null && _root.gameObject != null) {
            try { UnityEngine.Object.Destroy(_root.gameObject); }
            catch (Exception e) { UiLog.Error("element dispose failed: " + e.Message); }
        }

        _root = null;
        _layout = null;
    }
}
