using System;
using HarmonyLib;
using GameCivilization = DiceKingdoms.Interface.Civilization;
using GamePlayerSkins = DiceKingdoms.Game.PlayerSkins;
using GameRules = DiceKingdoms.Meta.Rules;
using GameScoreboardRow = DiceKingdoms.Interface.ScoreboardRow;
using GameTechnologies = DiceKingdoms.Game.Technologies;
using GameTechTree = DiceKingdoms.Game.TechTree;

namespace DiceModders.UiKit.Native;

/// <summary>
/// The few game methods the native layer must observe or answer. Every hook contains its own error boundary: a failure
/// here must never change the game's own behaviour.
/// </summary>
internal static class NativeHooks {
    private static Harmony? _harmony;
    private static UiKitRuntime? _runtime;
    private static bool _busySkins;

    public static void Install(UiKitRuntime runtime) {
        if (_harmony != null) return;
        _runtime = runtime;

        Harmony? harmony = null;
        try {
            harmony = new Harmony("DiceModders.UiKit.native");
            harmony.PatchAll(typeof(ScoreboardRowStartPatch));
            harmony.PatchAll(typeof(RulesAwakePatch));
            harmony.PatchAll(typeof(RulesEnablePatch));
            harmony.PatchAll(typeof(TechTreeLookupPatch));
            harmony.PatchAll(typeof(PlayerSkinsCivilizationPatch));
            _harmony = harmony;
            UiLog.Info("native game hooks installed");
        }
        catch (Exception e) {
            _runtime = null;
            if (harmony != null) {
                try { harmony.UnpatchSelf(); }
                catch (Exception cleanup) { UiLog.Warn("partial native hooks could not be removed: " + cleanup.Message); }
            }
            UiLog.Error("native game hooks could not be installed: " + e);
        }
    }

    public static void Uninstall() {
        var harmony = _harmony;
        _harmony = null;
        _runtime = null;
        _busySkins = false;
        if (harmony == null) return;

        try { harmony.UnpatchSelf(); }
        catch (Exception e) { UiLog.Warn("native game hooks could not be removed: " + e.Message); }
    }

    internal static void RowStarted(GameScoreboardRow? row) {
        try { _runtime?.NativeUi.OnScoreboardRowReady(row); }
        catch (Exception e) { UiLog.OnceError("scoreboard:row", "scoreboard row hook failed: " + e); }
    }

    internal static void RulesOpened(GameRules? rules) {
        try { _runtime?.NativeUi.OnRulesScreen(rules); }
        catch (Exception e) { UiLog.OnceError("rules:screen", "Rules screen hook failed: " + e); }
    }

    internal static bool TechTreePrefix(GameCivilization civilization, ref GameTechTree? result) {
        try {
            var runtime = _runtime;
            if (runtime != null && runtime.NativeUi.TryGetRulesTree((int)civilization, out var tree) && tree != null) {
                result = tree;
                return false;
            }
        }
        catch (Exception e) {
            UiLog.OnceError("rules:tree", "tech tree hook failed: " + e);
        }
        return true;
    }

    internal static bool SkinsPrefix(GamePlayerSkins instance, GameCivilization value) {
        if (_busySkins) return true;

        int id = (int)value;
        if (id is >= 0 and <= 4) return true;

        try {
            var runtime = _runtime;
            if (runtime == null || !runtime.NativeUi.TryGetSkinsLike(id, out int like)) return true;

            _busySkins = true;
            try {
                instance.Civilization = (GameCivilization)like;
                instance.civilization = value;
            }
            finally {
                _busySkins = false;
            }
            return false;
        }
        catch (Exception e) {
            _busySkins = false;
            UiLog.OnceError("rules:skins", "civilization skin hook failed: " + e);
        }
        return true;
    }
}

[HarmonyPatch(typeof(GameScoreboardRow), "Start")]
internal static class ScoreboardRowStartPatch {
    [HarmonyPostfix]
    public static void Postfix(GameScoreboardRow __instance) => NativeHooks.RowStarted(__instance);
}

[HarmonyPatch(typeof(GameRules), "Awake")]
internal static class RulesAwakePatch {
    [HarmonyPostfix]
    public static void Postfix(GameRules __instance) => NativeHooks.RulesOpened(__instance);
}

[HarmonyPatch(typeof(GameRules), "OnEnable")]
internal static class RulesEnablePatch {
    [HarmonyPostfix]
    public static void Postfix(GameRules __instance) => NativeHooks.RulesOpened(__instance);
}

[HarmonyPatch(typeof(GameTechnologies), "TechTree")]
internal static class TechTreeLookupPatch {
    [HarmonyPrefix]
    public static bool Prefix(GameCivilization civilization, ref GameTechTree? __result)
        => NativeHooks.TechTreePrefix(civilization, ref __result);
}

[HarmonyPatch(typeof(GamePlayerSkins), "Civilization", MethodType.Setter)]
internal static class PlayerSkinsCivilizationPatch {
    [HarmonyPrefix]
    public static bool Prefix(GamePlayerSkins __instance, GameCivilization value)
        => NativeHooks.SkinsPrefix(__instance, value);
}
