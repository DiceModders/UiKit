using System;
using UnityEngine;
using GameConfirmModal = DiceKingdoms.Interface.ConfirmModal;

namespace DiceModders.UiKit.Native;

/// <summary>
/// The game's own modal dialog (<c>ConfirmModal</c>): the yes/no question the game asks before going back to the menu, and its information box.
/// Text may contain the game's icon tokens (<c>{r.gold}</c>, <c>{o.question-mark}</c> ...). The dialog exists on the match screen; elsewhere the calls
/// return false and nothing opens.
/// </summary>
public sealed class DialogsApi {
    private readonly UiScope _scope;

    /// <summary>Pass as <c>icon</c> for a sharp warning sign (drawn by UiKit; the game has no warning icon of its own).</summary>
    public const string WarningIcon = "uikit:warning";

    internal DialogsApi(UiScope scope) => _scope = scope;

    /// <summary>True while the game's dialog is open (ours or the game's own).</summary>
    public bool IsOpen => UiGuard.Value("dialogs.open", () => Find() is { } modal && modal.Opened, false, _scope.Id);

    /// <summary>
    /// Asks a yes/no question with the game's dialog. <paramref name="onConfirm"/> runs on the confirm button, <paramref name="onCancel"/> on cancel.
    /// <paramref name="dontShowAgainId"/> adds the game's "don't show again" box: once ticked, later calls with the same id confirm at once without asking
    /// (for this game session). Returns false when the dialog is not available or the scope is disposed.
    /// </summary>
    public bool Confirm(string? text, Action? onConfirm, Action? onCancel = null, string? icon = null, string? dontShowAgainId = null) {
        if (!_scope.IsAlive) return false;
        return UiGuard.Value("dialogs.confirm", () => {
            var modal = Find();
            if (modal == null) return false;
            modal.OpenConfirm(Icon(icon), GameTextFactory.Plain(text),
                Wrap(modal, onCancel, "dialogs.cancel"), Wrap(modal, onConfirm, "dialogs.confirm-click"), dontShowAgainId);
            if (modal.Opened) ShowPicture(modal, icon);   // "don't show again" ticked: the game confirmed without opening
            return true;
        }, false, _scope.Id);
    }

    /// <summary>Shows an information box with one button. <paramref name="onClose"/> runs when it is closed. Returns false when not available.</summary>
    public bool Info(string? text, Action? onClose = null, string? icon = null, string? dontShowAgainId = null) {
        if (!_scope.IsAlive) return false;
        return UiGuard.Value("dialogs.info", () => {
            var modal = Find();
            if (modal == null) return false;
            modal.OpenInformation(Icon(icon), GameTextFactory.Plain(text),
                Wrap(modal, onClose, "dialogs.close"), dontShowAgainId);
            if (modal.Opened) ShowPicture(modal, icon);
            return true;
        }, false, _scope.Id);
    }

    /// <summary>
    /// The game's callbacks hold our delegate; it is kept alive with the dialog object and does nothing once the scope is disposed.
    /// The game calls a button's callback BEFORE it closes the dialog, so the mod's callback runs a moment later (after this frame's layout): a dialog
    /// opened from it would otherwise be closed at once by the game, and the first dialog's cancel would run.
    /// </summary>
    private Il2CppSystem.Action? Wrap(GameConfirmModal modal, Action? callback, string operation) {
        var scope = _scope;
        Action safe = () => {
            DialogIcons.Show(modal.iconLabel, null);     // the game reuses the dialog: our picture must not stay for its own questions
            if (!scope.IsAlive || callback == null) return;
            if (!LayoutOnce.Request(() => { if (scope.IsAlive) UiGuard.Callback(operation, callback); }))
                UiGuard.Callback(operation, callback);   // UiKit shutting down: run it now
        };
        var native = Il2CppDelegates.ToIl2CppAction(safe);
        Il2CppDelegates.Hold(modal, new object?[] { safe, native });
        return native;
    }

    /// <summary>No icon given: the game's own empty icon, like its Back to menu dialog (a text icon is drawn as a scaled-up glyph and looks blurry).</summary>
    private static DiceKingdoms.Interface.StructuredText Icon(string? icon) =>
        string.IsNullOrEmpty(icon) || icon == WarningIcon ? DiceKingdoms.Interface.StructuredText.EMPTY : GameTextFactory.Plain(icon);

    private static void ShowPicture(GameConfirmModal modal, string? icon) =>
        UiGuard.Run("dialogs.icon", () => DialogIcons.Show(modal.iconLabel, icon == WarningIcon ? DialogIcons.Warning : null));

    private static GameConfirmModal? _found;

    /// <summary>The dialog of the loaded scene (it may be inactive until opened). Remembered after the first search; searched again only when that dialog is gone (scene change).</summary>
    private static GameConfirmModal? Find() {
        try { if (_found != null && _found.gameObject.scene.IsValid()) return _found; } catch { }
        _found = null;
        foreach (var o in Resources.FindObjectsOfTypeAll<GameConfirmModal>())
            if (o != null && o.gameObject.scene.IsValid()) return _found = o;
        return null;
    }
}
