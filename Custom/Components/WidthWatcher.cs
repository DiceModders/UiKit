using System;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;

namespace DiceModders.UiKit.Custom;

/// <summary>
/// Rebuilds a text that is cut with "..." when its width changes. TMP sometimes keeps the old cut for a text created before its first layout pass.
/// This is the event-driven replacement for a per-frame width watcher.
/// </summary>
internal sealed class WidthWatcher : MonoBehaviour {
    public WidthWatcher(IntPtr ptr) : base(ptr) { }

    private TMP_Text? _text;
    private float _lastWidth = -1f;

    public void Bind(TMP_Text? text) => _text = text;

    private void OnEnable() {
        _lastWidth = -1f;
    }

    private void OnRectTransformDimensionsChange() {
        var text = _text;
        if (text == null) return;

        try {
            var rect = text.rectTransform;
            if (rect == null) return;

            float width = rect.rect.width;
            if (Mathf.Abs(width - _lastWidth) < 0.5f) return;
            _lastWidth = width;

            if (!TmpTextSafety.EnsureMeasurable(text)) return;
            text.SetAllDirty();
            text.ForceMeshUpdate();
        }
        catch {
            // TMP can be mid-rebuild here; the next width change or layout event will retry.
        }
    }

    private void OnDestroy() => _text = null;
}
