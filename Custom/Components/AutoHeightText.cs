using System;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceModders.UiKit.Custom;

/// <summary>
/// Gives a wrapped text the height of its content at the width the layout actually gave it.
/// Event-driven: remeasures when the width changes, when the text is enabled, and once on the next frame after creation.
/// </summary>
internal sealed class AutoHeightText : MonoBehaviour {
    public AutoHeightText(IntPtr ptr) : base(ptr) { }

    private TMP_Text? _text;
    private LayoutElement? _layout;
    private float _lastHeight = -1f;
    private bool _scheduled;
    private Canvas.WillRenderCanvases? _canvasHandler;

    public bool Enabled { get; private set; }

    /// <summary>Called after the measured height changed, so owners can re-run post-layout chrome work.</summary>
    internal Action? AfterMeasure { get; set; }

    public void Bind(TMP_Text? text, LayoutElement? layout) {
        _text = text;
        _layout = layout;
    }

    public void SetEnabled(bool enabled) {
        Enabled = enabled;
        _lastHeight = -1f;
        if (enabled) MeasureSoon();
    }

    private void OnEnable() {
        if (Enabled) MeasureSoon();
    }

    private void Start() {
        if (Enabled) MeasureSoon();
    }

    private void OnRectTransformDimensionsChange() {
        if (Enabled) Measure();
    }

    public void MeasureNow() {
        if (Enabled) Measure();
    }

    /// <summary>Remembers to measure once after the current text change has been laid out.</summary>
    public void Refresh() => MeasureSoon();

    private void MeasureSoon() {
        if (!Enabled || _scheduled || _text == null) return;

        // One-shot layout check: fires once before the next canvas render, then unsubscribes.
        Action action = OnWillRenderCanvases;
        var handler = DelegateSupport.ConvertDelegate<Canvas.WillRenderCanvases>(action);
        if (handler == null) return;
        _scheduled = true;
        _canvasHandler = handler;
        Canvas.willRenderCanvases += handler;
    }

    private void OnWillRenderCanvases() {
        if (_canvasHandler != null) {
            Canvas.willRenderCanvases -= _canvasHandler;
            _canvasHandler = null;
        }
        _scheduled = false;
        Measure();
    }

    private void Measure() {
        if (!Enabled || _text == null || _layout == null) return;

        float width = _text.rectTransform.rect.width;
        if (width < 1f) return;   // not laid out yet; the next frame or a width change will retry

        float height = Mathf.Ceil(_text.GetPreferredValues(_text.text, width, 0f).y);
        float minimum = Mathf.Ceil(_text.fontSize * 1.2f);
        if (height < minimum) height = minimum;

        if (Mathf.Abs(height - _lastHeight) < 0.5f) return;
        _lastHeight = height;

        _layout.minHeight = height;
        _layout.preferredHeight = height;
        _layout.flexibleHeight = 0f;

        var parent = _text.rectTransform.parent;
        if (parent != null) {
            var rect = parent.TryCast<RectTransform>();
            if (rect != null) LayoutRebuilder.MarkLayoutForRebuild(rect);
        }

        UiGuard.Callback("auto-height", AfterMeasure);
    }

    private void OnDisable() {
        if (_canvasHandler != null) {
            Canvas.willRenderCanvases -= _canvasHandler;
            _canvasHandler = null;
        }
        _scheduled = false;
    }

    private void OnDestroy() {
        OnDisable();
        _text = null;
        _layout = null;
    }
}
