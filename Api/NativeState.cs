namespace DiceModders.UiKit.Native;

/// <summary>
/// Lifecycle of one native registration. A registration is stable: scene changes and missing game UI only move it
/// between <see cref="Pending"/>, <see cref="Attached"/>, <see cref="Detached"/> and <see cref="Failed"/>; the handle
/// object itself is never replaced.
/// </summary>
public enum NativeState {
    /// <summary>The registration is waiting for a target (scene, game screen, asset or template).</summary>
    Pending,

    /// <summary>The registration currently owns live objects in the game's UI.</summary>
    Attached,

    /// <summary>The registration was attached earlier and is now waiting to be attached again.</summary>
    Detached,

    /// <summary>The last attempt failed. The library retries automatically; <see cref="NativeHandle.Status"/> says why.</summary>
    Failed,

    /// <summary>The active host policy or the game does not support this registration.</summary>
    Unsupported,

    /// <summary>The registration was disposed and can never attach again.</summary>
    Disposed,
}
