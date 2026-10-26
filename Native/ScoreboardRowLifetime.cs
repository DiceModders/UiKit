using System;
using UnityEngine;

namespace DiceModders.UiKit.Native;

/// <summary>
/// Reports a scoreboard row's destruction to the native host. Unity has no event for this, and the game's row type
/// does not expose OnDestroy, so the watcher lives on the row object and is cleaned up with it.
/// </summary>
internal sealed class ScoreboardRowLifetime : MonoBehaviour {
    public ScoreboardRowLifetime(IntPtr ptr) : base(ptr) { }

    private void OnDestroy() => ScoreboardRowLifetimeRegistry.Detach(this);
}
