using System;
using UnityEngine;

namespace DiceModders.UiKit.Custom;

/// <summary>
/// Forwards a root canvas resize to the library's layout event. Unity calls this message only when the watched
/// RectTransform actually changes, so there is no per-frame polling.
/// </summary>
internal sealed class CanvasLayoutWatcher : MonoBehaviour {
    public CanvasLayoutWatcher(IntPtr ptr) : base(ptr) { }

    private void OnEnable() => LayoutEvents.RequestCanvasChanged();

    private void OnRectTransformDimensionsChange() => LayoutEvents.RequestCanvasChanged();
}
