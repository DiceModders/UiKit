using System;
using DiceModders.UiKit.Custom;
using Il2CppInterop.Runtime;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DiceModders.UiKit;

/// <summary>
/// Escape closes the front-most closable mod window (<see cref="UiWindow.CloseOnEscape"/>). One input action on the game's own input system;
/// Unity reports it through <c>InputSystem.onActionChange</c> (its <c>performed</c> callback carries a struct Il2CppInterop cannot wrap), so
/// nothing checks the keyboard every frame. Ignored while a text field has the focus.
/// </summary>
internal static class EscapeKey {
    private static InputAction? _action;
    private static Il2CppSystem.Action<Il2CppSystem.Object, InputActionChange>? _listener;

    public static void Ensure() {
        if (_action != null) return;
        try {
            _action = new InputAction("uikit.escape", InputActionType.Button, "<Keyboard>/escape");
            _listener = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Il2CppSystem.Object, InputActionChange>>(
                new Action<Il2CppSystem.Object, InputActionChange>(OnChange));
            InputSystem.add_onActionChange(_listener);
            _action.Enable();
        }
        catch (Exception e) { UiLog.Once("escape:init", "escape key unavailable: " + e.Message); }
    }

    private static void OnChange(Il2CppSystem.Object obj, InputActionChange change) {
        if (change != InputActionChange.ActionPerformed || obj == null || _action == null || obj.Pointer != _action.Pointer) return;
        if (Typing()) return;
        UiGuard.Run("escape.close", () => UiWindow.CloseFront(), null);
    }

    private static bool Typing() {
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == null) return false;
        var tmp = selected.GetComponent<TMPro.TMP_InputField>();
        return tmp != null && tmp.isFocused;
    }
}
