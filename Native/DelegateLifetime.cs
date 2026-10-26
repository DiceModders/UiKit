using System;
using UnityEngine;

namespace DiceModders.UiKit.Native;

/// <summary>
/// Releases the managed/IL2CPP delegate holders owned by a game object as soon as that object is destroyed.
/// The component is added only to owners that already receive held delegates.
/// </summary>
internal sealed class DelegateLifetime : MonoBehaviour {
    public DelegateLifetime(IntPtr ptr) : base(ptr) { }

    private void OnDestroy() {
        try { Il2CppDelegates.RemoveOwner(gameObject); }
        catch (Exception e) { UiLog.Once("delegates:lifetime", "delegate lifetime cleanup failed: " + e.Message); }
    }
}
