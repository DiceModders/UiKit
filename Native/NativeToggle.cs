using System;
using UnityEngine;
using GameToggle = DiceKingdoms.Interface.Toggle;
using SettingInterfaceBool = DiceKingdoms.Meta.SettingInterfaceBool;

namespace DiceModders.UiKit.Native;

/// <summary>
/// A checkbox cloned from the game's boolean setting control. The setting binding is removed before the clone wakes up, so
/// nothing is written to the game's settings; the game's own Toggle provides the art, checkmark, click sound and change event.
/// </summary>
internal sealed class NativeToggle {
    public GameObject Go { get; private set; }
    public GameToggle Game { get; private set; }
    public bool IsAlive => Go != null && Game != null;

    private bool _silent;
    private Action<bool> _changed;

    private NativeToggle() { }

    public bool Value => Game != null && Game.Value;

    public static NativeToggle Create(TemplateCatalog templates, Transform parent, bool initial, Action<bool> changed) {
        var prefab = templates?.BoolSetting();
        if (prefab == null) return null;

        var go = UnityEngine.Object.Instantiate(prefab.gameObject, Staging.Root);
        go.name = "DM Toggle";
        NativeButton.StripForeign(go);

        var binding = go.GetComponent<SettingInterfaceBool>();
        if (binding != null) UnityEngine.Object.DestroyImmediate(binding);
        NativeButton.RepairBaseButtons(go);

        var game = go.GetComponentInChildren<GameToggle>(true);
        if (game == null) {
            UnityEngine.Object.Destroy(go);
            return null;
        }

        go.transform.SetParent(parent, false);
        go.SetActive(true);

        var toggle = new NativeToggle {
            Go = go,
            Game = game,
            _changed = changed,
        };

        Action<bool> handler = toggle.OnChange;
        var native = Il2CppDelegates.ToIl2CppActionBool(handler);
        game.onChange = native;
        Il2CppDelegates.Hold(go, new object[] { handler, native });
        toggle.SetSilently(initial);
        return toggle;
    }

    public void SetChanged(Action<bool> changed) => _changed = changed;

    private void OnChange(bool value) {
        if (_silent) return;
        try { _changed?.Invoke(value); }
        catch (Exception e) { UiLog.Error("native toggle: " + e); }
        NativeButton.Deselect();
    }

    /// <summary>Changes the checkbox without calling the change callback.</summary>
    public void SetSilently(bool value) {
        if (Game == null) return;
        _silent = true;
        try { Game.Value = value; }
        finally { _silent = false; }
    }

    public void SetInteractable(bool on) {
        if (Game != null) Game.Interactable = on;
    }

    public void Destroy() {
        if (Go != null) UnityEngine.Object.Destroy(Go);
        Go = null;
    }
}
