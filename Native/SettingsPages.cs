using UnityEngine;
using GameSettingSet = DiceKingdoms.Meta.SettingSetInterface;
using GameSettings = DiceKingdoms.Meta.SettingSetsInterface;
using GameStart = DiceKingdoms.Interface.StartGame;

namespace DiceModders.UiKit.Native;

/// <summary>Maps the game's built-in settings objects to the public <see cref="SettingsPage"/> names.</summary>
internal static class SettingsPages {
    public static GameSettingSet? Find(SettingsPage page) {
        if (page == SettingsPage.Lobby) return FindLobby();

        var settings = GameUi.FindSettings();
        if (settings == null) return null;

        return page switch {
            SettingsPage.Video => settings.video,
            SettingsPage.Audio => settings.audio,
            SettingsPage.Input => settings.input,
            SettingsPage.Game => settings.game,
            _ => null,
        };
    }

    public static SettingsPage? PageOf(GameSettingSet? set) {
        if (set == null) return null;
        if (IsLobby(set)) return SettingsPage.Lobby;

        var settings = set.GetComponentInParent<GameSettings>(true);
        if (settings == null) return null;

        if (Same(settings.video, set)) return SettingsPage.Video;
        if (Same(settings.audio, set)) return SettingsPage.Audio;
        if (Same(settings.input, set)) return SettingsPage.Input;
        if (Same(settings.game, set)) return SettingsPage.Game;
        return null;
    }

    public static bool IsLobby(GameSettingSet? set) {
        if (set == null) return false;

        var parent = set.GetComponentInParent<GameStart>(true);
        if (parent != null && Same(parent.lobbySettingsInterface, set)) return true;

        foreach (var start in Object.FindObjectsOfType<GameStart>(true)) {
            if (start == null || start.lobbySettingsInterface == null) continue;
            if (Same(start.lobbySettingsInterface, set)) return true;
        }
        return false;
    }

    private static GameSettingSet? FindLobby() {
        GameSettingSet? fallback = null;
        foreach (var start in Object.FindObjectsOfType<GameStart>(true)) {
            if (start == null || start.lobbySettingsInterface == null) continue;
            var lobby = start.lobbySettingsInterface;
            if (lobby.gameObject.activeInHierarchy) return lobby;
            fallback ??= lobby;
        }
        return fallback;
    }

    private static bool Same(GameSettingSet? left, GameSettingSet? right)
        => left != null && right != null && left.Pointer == right.Pointer;
}
