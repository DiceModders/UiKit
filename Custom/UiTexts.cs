using System;
using System.Collections.Generic;

namespace DiceModders.UiKit.Custom;

/// <summary>
/// The few words UiKit itself shows (window title-bar buttons), in the game's six languages. UiKit does not depend on ModCore, so it reads the
/// game's selected language itself; mods translate their own texts (ModCore.Texts).
/// </summary>
internal static class UiTexts {
	private static readonly Dictionary<string, Dictionary<string, string>> Words = new() {
		["fullscreen"] = new() { ["en"] = "Full screen", ["de"] = "Vollbild", ["fr"] = "Plein écran", ["ru"] = "Во весь экран", ["uk"] = "На весь екран", ["zh"] = "全屏" },
		["window"] = new() { ["en"] = "Window", ["de"] = "Fenster", ["fr"] = "Fenêtre", ["ru"] = "Окно", ["uk"] = "Вікно", ["zh"] = "窗口" },
	};

	internal static string Get(string key) {
		if (!Words.TryGetValue(key, out var byLanguage)) return key;
		string code = Language();
		int dash = code.IndexOf('-');
		if (byLanguage.TryGetValue(code, out var text) || (dash > 0 && byLanguage.TryGetValue(code.Substring(0, dash), out text))) return text;
		return byLanguage["en"];
	}

	private static string Language() {
		try {
			if (!UnityEngine.Localization.Settings.LocalizationSettings.InitializationOperation.IsDone) return "en";
			return UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale?.Identifier.Code ?? "en";
		}
		catch (Exception) { return "en"; }
	}
}
