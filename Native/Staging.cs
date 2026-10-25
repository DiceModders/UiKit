using UnityEngine;

namespace DiceModders.UiKit.Native;

/// <summary>
/// An inactive, persistent parent for freshly cloned game objects. A clone parked here does not run
/// Awake/OnEnable/Start, so it can be stripped and reconfigured before it enters the real UI.
/// </summary>
internal static class Staging {
    private static GameObject? _root;

    public static Transform Root {
        get {
            if (_root == null) {
                _root = new GameObject("DM Staging");
                _root.SetActive(false);
                Object.DontDestroyOnLoad(_root);
            }
            return _root.transform;
        }
    }

    /// <summary>True for objects created by this library, so template searches never pick up our own clones.</summary>
    public static bool IsOurs(Transform? transform) {
        for (; transform != null; transform = transform.parent) {
            string name = transform.name;
            if (name.StartsWith("DM ") || name.StartsWith("DiceModders")) return true;
        }
        return false;
    }

    public static void Reset() {
        if (_root == null) return;
        Object.Destroy(_root);
        _root = null;
    }
}
