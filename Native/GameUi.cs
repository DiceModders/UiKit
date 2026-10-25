using System.Collections.Generic;
using UnityEngine;
using GameRules = DiceKingdoms.Meta.Rules;
using GameScoreboardRow = DiceKingdoms.Interface.ScoreboardRow;
using GameSettings = DiceKingdoms.Meta.SettingSetsInterface;

namespace DiceModders.UiKit.Native;

/// <summary>Where the game keeps the UI objects the library reuses (verified against the game's hierarchy).</summary>
internal static class GameUi {
    // Top-right button bar: "Meta/Buttons" in a match, "Top Right/Top Right Buttons" in the main menu.
    public const string BarMatch = "Meta/Buttons";
    public const string BarMenu = "Top Right/Top Right Buttons";
    public const string RulesButton = "Rules";
    public const string SettingsCloseButton = "Settings/Close Button";

    public static Transform? FindBar(Transform? canvasRoot) {
        if (canvasRoot == null) return null;
        return canvasRoot.Find(BarMatch) ?? canvasRoot.Find(BarMenu);
    }

    /// <summary>
    /// The first settings screen in the scene. It may be inactive while the player has the settings window closed; native
    /// additions can still be prepared there and appear as soon as the screen opens.
    /// </summary>
    public static GameSettings? FindSettings() {
        GameSettings? fallback = null;
        foreach (var settings in Object.FindObjectsOfType<GameSettings>(true)) {
            if (settings == null || Staging.IsOurs(settings.transform)) continue;
            if (settings.gameObject.activeInHierarchy) return settings;
            fallback ??= settings;
        }
        return fallback;
    }

    /// <summary>The first active top-right bar of any root canvas, or null while no scene with a bar is loaded.</summary>
    public static Transform? FindActiveBar() {
        foreach (var canvas in Object.FindObjectsOfType<Canvas>(true)) {
            if (canvas == null || !canvas.isRootCanvas) continue;
            var bar = FindBar(canvas.transform);
            if (bar != null && bar.gameObject.activeInHierarchy) return bar;
        }
        return null;
    }

    /// <summary>The Rules screen, active if possible. It can be inactive while the player has it closed.</summary>
    public static GameRules? FindRules() {
        GameRules? fallback = null;
        foreach (var rules in Object.FindObjectsOfType<GameRules>(true)) {
            if (rules == null || Staging.IsOurs(rules.transform)) continue;
            if (rules.gameObject.activeInHierarchy) return rules;
            fallback ??= rules;
        }
        return fallback;
    }

    /// <summary>Every scoreboard row currently alive, including rows in an inactive scoreboard panel.</summary>
    public static List<GameScoreboardRow> FindScoreboardRows() {
        var rows = new List<GameScoreboardRow>();
        foreach (var row in Object.FindObjectsOfType<GameScoreboardRow>(true)) {
            if (row == null || Staging.IsOurs(row.transform)) continue;
            rows.Add(row);
        }
        return rows;
    }
}
