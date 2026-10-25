using System;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameDropdown = DiceKingdoms.Interface.Dropdown;
using SettingInterfaceBool = DiceKingdoms.Meta.SettingInterfaceBool;
using SettingInterfaceFloat = DiceKingdoms.Meta.SettingInterfaceFloat;
using SettingSetsInterface = DiceKingdoms.Meta.SettingSetsInterface;
using GameButtonIcon = DiceKingdoms.Interface.ButtonIcon;
using GameButtonLabel = DiceKingdoms.Interface.ButtonLabel;

namespace DiceModders.UiKit.Native;

/// <summary>
/// The process-wide cache of live game objects that native skins clone. Entries are revalidated with Unity's null test, so a
/// template destroyed by a scene change is found again in the next scene. Searches are throttled per template; a missing
/// template costs at most one hierarchy scan per second and never causes a per-frame scan.
/// </summary>
internal sealed class TemplateCatalog {
    private const float SearchInterval = 1f;

    private GameObject? _iconButton;
    private GameObject? _labelButton;
    private SettingInterfaceBool? _boolSetting;
    private SettingInterfaceFloat? _floatSetting;
    private GameDropdown? _dropdown;
    private Scrollbar? _scrollbar;

    private float _nextIcon;
    private float _nextLabel;
    private float _nextBool;
    private float _nextFloat;
    private float _nextDropdown;
    private float _nextScrollbar;

    /// <summary>Drops every reference. Called on scene changes so the next use searches the new scene.</summary>
    public void Invalidate() {
        _iconButton = null;
        _labelButton = null;
        _boolSetting = null;
        _floatSetting = null;
        _dropdown = null;
        _scrollbar = null;

        _nextIcon = 0f;
        _nextLabel = 0f;
        _nextBool = 0f;
        _nextFloat = 0f;
        _nextDropdown = 0f;
        _nextScrollbar = 0f;
    }

    /// <summary>An icon button of the top-right bar (the Rules "?" button).</summary>
    public GameObject? IconButton() {
        if (_iconButton != null) return _iconButton;
        if (!CanSearch(ref _nextIcon)) return null;

        try {
            foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true)) {
                if (canvas == null || !canvas.isRootCanvas) continue;
                var bar = GameUi.FindBar(canvas.transform);
                var rules = bar != null ? bar.Find(GameUi.RulesButton) : null;
                if (rules != null && rules.GetComponent<GameButtonIcon>() != null)
                    return _iconButton = rules.gameObject;
            }

            foreach (var icon in UnityEngine.Object.FindObjectsOfType<GameButtonIcon>(true)) {
                if (icon != null && !Staging.IsOurs(icon.transform))
                    return _iconButton = icon.gameObject;
            }
        }
        catch (Exception e) {
            UiLog.Once("templates:icon", "native icon-button template search failed: " + e.Message);
        }
        return null;
    }

    /// <summary>A text button (the Settings Close button, which uses the same Button-1 art as the settings controls).</summary>
    public GameObject? LabelButton() {
        if (_labelButton != null) return _labelButton;
        if (!CanSearch(ref _nextLabel)) return null;

        try {
            foreach (var sets in UnityEngine.Object.FindObjectsOfType<SettingSetsInterface>(true)) {
                var close = sets != null ? sets.closeButton : null;
                if (close != null && close.GetComponent<GameButtonLabel>() != null)
                    return _labelButton = close.gameObject;
            }

            foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>(true)) {
                if (canvas == null || !canvas.isRootCanvas) continue;
                var close = canvas.transform.Find(GameUi.SettingsCloseButton);
                if (close != null && close.GetComponent<GameButtonLabel>() != null)
                    return _labelButton = close.gameObject;
            }

            foreach (var label in UnityEngine.Object.FindObjectsOfType<GameButtonLabel>(true)) {
                if (label != null && !Staging.IsOurs(label.transform))
                    return _labelButton = label.gameObject;
            }
        }
        catch (Exception e) {
            UiLog.Once("templates:label", "native text-button template search failed: " + e.Message);
        }
        return null;
    }

    /// <summary>The game's boolean setting control (checkbox art, checkmark, click sound).</summary>
    public SettingInterfaceBool? BoolSetting() {
        if (_boolSetting != null) return _boolSetting;
        if (!CanSearch(ref _nextBool)) return null;

        try {
            // FindObjectsOfTypeAll also returns prefab assets referenced by the settings screens.
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<SettingInterfaceBool>())) {
                var control = o?.TryCast<SettingInterfaceBool>();
                if (control != null && !Staging.IsOurs(control.transform))
                    return _boolSetting = control;
            }
        }
        catch (Exception e) {
            UiLog.Once("templates:bool", "native checkbox template search failed: " + e.Message);
        }
        return null;
    }

    /// <summary>The game's float setting control (a slider with its value label).</summary>
    public SettingInterfaceFloat? FloatSetting() {
        if (_floatSetting != null) return _floatSetting;
        if (!CanSearch(ref _nextFloat)) return null;

        try {
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<SettingInterfaceFloat>())) {
                var control = o?.TryCast<SettingInterfaceFloat>();
                if (control != null && !Staging.IsOurs(control.transform) && control.GetComponentInChildren<Slider>(true) != null)
                    return _floatSetting = control;
            }
        }
        catch (Exception e) {
            UiLog.Once("templates:float", "native slider template search failed: " + e.Message);
        }
        return null;
    }

    /// <summary>The game's dropdown wrapper around a TMP_Dropdown (the Language setting).</summary>
    public GameDropdown? Dropdown() {
        if (_dropdown != null) return _dropdown;
        if (!CanSearch(ref _nextDropdown)) return null;

        try {
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<GameDropdown>())) {
                var control = o?.TryCast<GameDropdown>();
                if (control != null && !Staging.IsOurs(control.transform) && control.GetComponent<TMP_Dropdown>() != null)
                    return _dropdown = control;
            }
        }
        catch (Exception e) {
            UiLog.Once("templates:dropdown", "native dropdown template search failed: " + e.Message);
        }
        return null;
    }

    /// <summary>A vertical scrollbar of the game's art (not one of the dropdown popup templates).</summary>
    public Scrollbar? VerticalScrollbar() {
        if (_scrollbar != null) return _scrollbar;
        if (!CanSearch(ref _nextScrollbar)) return null;

        try {
            Scrollbar? best = null;
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Scrollbar>())) {
                var bar = o?.TryCast<Scrollbar>();
                if (bar == null || Staging.IsOurs(bar.transform) || bar.handleRect == null) continue;
                if (bar.direction != Scrollbar.Direction.BottomToTop &&
                    bar.direction != Scrollbar.Direction.TopToBottom) continue;

                bool inDropdownTemplate = false;
                for (var t = bar.transform.parent; t != null; t = t.parent) {
                    if (t.name == "Template") { inDropdownTemplate = true; break; }
                }
                if (inDropdownTemplate) continue;

                if (best == null || bar.gameObject.scene.IsValid()) best = bar;
                if (bar.gameObject.scene.IsValid()) break;
            }

            if (best != null) return _scrollbar = best;
        }
        catch (Exception e) {
            UiLog.Once("templates:scrollbar", "native scrollbar template search failed: " + e.Message);
        }
        return null;
    }

    private static bool CanSearch(ref float next) {
        float now = Time.realtimeSinceStartup;
        if (now < next) return false;
        next = now + SearchInterval;
        return true;
    }
}
