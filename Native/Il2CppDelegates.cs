using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine.Events;

namespace DiceModders.UiKit.Native;

/// <summary>
/// Converts managed delegates to the game's IL2CPP delegate types and keeps them alive exactly as long as the Unity object
/// they belong to. Plain ConvertDelegate results can be collected while the game still holds them.
/// </summary>
internal static class Il2CppDelegates {
    private static readonly List<(UnityEngine.Object Owner, object Keep)> Alive = new();

    public static void Hold(UnityEngine.Object? owner, object? keep) {
        if (owner == null || keep == null) return;
        Alive.Add((owner, keep));
    }

    /// <summary>Drops references whose Unity owner was destroyed. Called on scene changes and runtime disposal.</summary>
    public static void Sweep() {
        if (Alive.Count > 0) Alive.RemoveAll(entry => entry.Owner == null);
    }

    public static void Clear() => Alive.Clear();

    public static Il2CppSystem.Action? ToIl2CppAction(Action? action)
        => action == null ? null : DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(action);

    public static Il2CppSystem.Action<bool>? ToIl2CppActionBool(Action<bool>? action)
        => action == null ? null : DelegateSupport.ConvertDelegate<Il2CppSystem.Action<bool>>(action);

    public static UnityAction? ToUnityAction(Action? action)
        => action == null ? null : DelegateSupport.ConvertDelegate<UnityAction>(action);

    public static UnityAction<int>? ToUnityActionInt(Action<int>? action)
        => action == null ? null : DelegateSupport.ConvertDelegate<UnityAction<int>>(action);

    public static UnityAction<float>? ToUnityActionFloat(Action<float>? action)
        => action == null ? null : DelegateSupport.ConvertDelegate<UnityAction<float>>(action);
}
