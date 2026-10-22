using System;
using UnityEngine;

namespace DiceModders.UiKit.Custom;

/// <summary>Owner component for a button listener. The listener itself lives in <see cref="ClickRelayRegistry"/>.</summary>
internal sealed class ClickRelay : MonoBehaviour {
    public ClickRelay(IntPtr ptr) : base(ptr) { }

    private void OnDestroy() => ClickRelayRegistry.Clear(this);
}
