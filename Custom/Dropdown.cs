using System;
using System.Collections.Generic;
using DiceModders.UiKit.Native;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UButton = UnityEngine.UI.Button;
using UIImage = UnityEngine.UI.Image;

namespace DiceModders.UiKit.Custom;

/// <summary>
/// A dropdown in the game's own style when its template is available, otherwise a managed compact cycling button.
/// The native skin opens the game's real popup; the managed fallback advances to the next option on click.
/// </summary>
public sealed class Dropdown : Element {
    private readonly List<string> _options;
    private readonly TMP_Text _label;
    private readonly UButton _button;
    private readonly Action<int> _onChanged;
    private readonly NativeDropdown _native;
    private int _index;

    internal Dropdown(RectTransform root, List<string> options, int index, TMP_Text label,
                      UButton button, Action<int> onChanged, NativeDropdown native) : base(root) {
        _options = options;
        _index = ClampIndex(index);
        _label = label;
        _button = button;
        _onChanged = onChanged;
        _native = native;
        if (_native == null) UpdateLabel();
    }

    internal static Dropdown? Create(Container parent, IEnumerable<string>? options, int index, Action<int>? onChanged) {
        var runtime = parent.Runtime;
        var values = Normalize(options);

        if (SkinPolicy.AllowNative(runtime)) {
            NativeDropdown? native = null;
            try {
                native = SkinPolicy.TryNative("dropdown", () =>
                    NativeDropdown.Create(runtime.Templates, parent.Root, values, index));
                if (native != null) {
                    var layout = UiFactory.Layout(native.Rt);
                    if (layout != null) {
                        layout.minHeight = layout.preferredHeight = native.Height;
                        layout.flexibleWidth = 1f;
                        layout.flexibleHeight = 0f;
                    }

                    var result = new Dropdown(native.Rt, values, native.Index, null, null, onChanged, native);
                    native.SetChanged(result.HandleNativeChange);
                    return result;
                }
            }
            catch (Exception e) {
                UiGuard.Report("dropdown.native", e);
                if (native?.Rt != null) UiGuard.Run("dropdown.cleanup", () => UiFactory.Destroy(native.Rt.gameObject));
            }

            if (!SkinPolicy.MissingNative(runtime, "dropdown"))
                return null;
        }

        return CreateManaged(parent, values, index, onChanged);
    }

    private static Dropdown? CreateManaged(Container parent, List<string> values, int index, Action<int>? onChanged) {
        var theme = parent.Theme;
        var root = UiFactory.NewRect("DM Dropdown", parent.Root);
        try {

            var background = root.gameObject.AddComponent<UIImage>();
            var normal = theme.Button(AssetCatalog.ButtonVariant.Regular, AssetCatalog.ButtonState.Ready);
            if (normal != null) {
                background.sprite = normal;
                background.type = UIImage.Type.Sliced;
                background.color = Color.white;
            }
            else {
                background.color = new Color(0.20f, 0.17f, 0.13f, 0.95f);
            }

            var button = root.gameObject.AddComponent<UButton>();
            button.targetGraphic = background;

            var hover = theme.Button(AssetCatalog.ButtonVariant.Regular, AssetCatalog.ButtonState.Hovered);
            var pressed = theme.Button(AssetCatalog.ButtonVariant.Regular, AssetCatalog.ButtonState.Pressed);
            if (normal != null && hover != null && pressed != null) {
                button.transition = Selectable.Transition.SpriteSwap;
                var state = button.spriteState;
                state.highlightedSprite = hover;
                state.pressedSprite = pressed;
                state.selectedSprite = normal;
                button.spriteState = state;
            }

            var label = UiFactory.AddText(root, "DM Label", string.Empty, 16f,
                                          TextAlignmentOptions.MidlineLeft, theme);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(10f, 0f);
            label.rectTransform.offsetMax = new Vector2(-26f, 0f);

            var arrow = UiFactory.AddText(root, "DM Arrow", "▼", 12f,
                                          TextAlignmentOptions.MidlineRight, theme);
            arrow.rectTransform.anchorMin = new Vector2(1f, 0f);
            arrow.rectTransform.anchorMax = new Vector2(1f, 1f);
            arrow.rectTransform.pivot = new Vector2(1f, 0.5f);
            arrow.rectTransform.sizeDelta = new Vector2(20f, 0f);
            arrow.rectTransform.anchoredPosition = new Vector2(-8f, 0f);

            var layout = UiFactory.Layout(root);
            layout.minHeight = layout.preferredHeight = 32f;
            layout.flexibleWidth = 1f;
            layout.flexibleHeight = 0f;

            var result = new Dropdown(root, values, index, label, button, onChanged, null);
            var relay = root.gameObject.AddComponent<ClickRelay>();
            ClickRelayRegistry.Bind(relay, button, result.Cycle);
            return result;
        }
        catch (Exception e) {
            UiGuard.Report("dropdown.managed", e);
            UiFactory.Destroy(root.gameObject);
            return null;
        }
    }

    public int Index => _index;
    public string Selected => _index >= 0 && _index < _options.Count ? _options[_index] : string.Empty;
    public int Count => _options.Count;

    public Dropdown Set(int index) {
        if (!IsAlive) return this;
        _index = ClampIndex(index);
        if (_native != null) UiGuard.Run("dropdown.set", () => _native.SetSilently(_index));
        else UiGuard.Run("dropdown.set", UpdateLabel);
        return this;
    }

    public Dropdown Options(IEnumerable<string>? options) {
        if (!IsAlive) return this;

        List<string> values;
        try { values = Normalize(options); }
        catch (Exception e) {
            UiGuard.Report("dropdown.options", e);
            return this;
        }

        _options.Clear();
        _options.AddRange(values);
        _index = ClampIndex(_index);

        if (_native != null) UiGuard.Run("dropdown.options", () => _native.SetOptions(_options, _index));
        else UiGuard.Run("dropdown.options", UpdateLabel);
        return this;
    }

    public Dropdown Interactable(bool on = true) {
        if (!IsAlive) return this;
        if (_native != null) UiGuard.Run("dropdown.interactable", () => _native.SetInteractable(on));
        else if (_button != null) UiGuard.Run("dropdown.interactable", () => _button.interactable = on);
        return this;
    }

    private int ClampIndex(int index) {
        if (_options.Count == 0) return 0;
        if (index < 0) return 0;
        return index >= _options.Count ? _options.Count - 1 : index;
    }

    private void Cycle() {
        if (_options.Count == 0) return;
        _index = (_index + 1) % _options.Count;
        UpdateLabel();
        try { _onChanged?.Invoke(_index); }
        catch (Exception e) { UiLog.Error("dropdown change handler: " + e); }
    }

    private void HandleNativeChange(int index) {
        _index = ClampIndex(index);
        try { _onChanged?.Invoke(_index); }
        catch (Exception e) { UiLog.Error("dropdown change handler: " + e); }
    }

    private void UpdateLabel() {
        if (_label != null) _label.text = Selected;
    }

    private static List<string> Normalize(IEnumerable<string>? options) {
        var values = new List<string>();
        if (options != null) {
            foreach (var option in options) values.Add(option ?? string.Empty);
        }
        if (values.Count == 0) values.Add(string.Empty);
        return values;
    }
}
