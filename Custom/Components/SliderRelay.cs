using System;
using UnityEngine;

namespace DiceModders.UiKit.Custom;

/// <summary>Owner component for a slider listener. The listener itself lives in <see cref="SliderRelayRegistry"/>.</summary>
internal sealed class SliderRelay : MonoBehaviour {
    public SliderRelay(IntPtr ptr) : base(ptr) { }

    private void OnDestroy() => SliderRelayRegistry.Clear(this);
}
