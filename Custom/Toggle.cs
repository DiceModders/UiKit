using System;
using DiceModders.UiKit.Native;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UButton = UnityEngine.UI.Button;
using UIImage = UnityEngine.UI.Image;

namespace DiceModders.UiKit.Custom;

/// <summary>
/// A checkbox with a label. Uses a clone of the game's native boolean setting control when available; otherwise the managed
/// checkbox is built from the game's sprites.
/// </summary>
public sealed class Toggle : Element {
    private readonly UIImage _check;
    private readonly TextMeshProUGUI _label;
    private readonly Action<bool> _onChanged;
    private readonly NativeToggle _native;
    private bool _value;

    internal Toggle(RectTransform root, UIImage check, TextMeshProUGUI label, bool value,
                    Action<bool> onChanged, NativeToggle native = null) : base(root) {
        _check = check;
        _label = label;
        _value = value;
        _onChanged = onChanged;
        _native = native;
        ApplyValue();
    }

    internal static Toggle Create(Container parent, string label, bool value, Action<bool> onChanged) {
        var runtime = parent.Runtime;

        if (SkinPolicy.AllowNative(runtime)) {
            var row = CreateRow(parent);
            Toggle result = null;

            var native = SkinPolicy.TryNative("toggle", () =>
                NativeToggle.Create(runtime.Templates, row, value, v => result?.HandleNativeChange(v)));
            if (native != null) {
                var nativeLayout = UiFactory.Layout(native.Go.GetComponent<RectTransform>());
                if (nativeLayout != null) {
                    nativeLayout.minWidth = nativeLayout.preferredWidth = 28f;
                    nativeLayout.minHeight = nativeLayout.preferredHeight = 28f;
                    nativeLayout.flexibleWidth = 0f;
                    nativeLayout.flexibleHeight = 0f;
                }

                var text = UiFactory.AddText(row, "DM Label", label ?? string.Empty, 16f,
                                             TextAlignmentOptions.MidlineLeft, parent.Theme);
                var textLayout = UiFactory.Layout(text.rectTransform);
                textLayout.minHeight = textLayout.preferredHeight = 24f;
                textLayout.flexibleWidth = 1f;
                textLayout.flexibleHeight = 0f;

                result = new Toggle(row, null, text, value, onChanged, native);
                native.SetChanged(result.HandleNativeChange);
                return result;
            }

            UiFactory.Destroy(row.gameObject);
            if (!SkinPolicy.MissingNative(runtime, "toggle"))
                return null;
        }

        return CreateManaged(parent, label, value, onChanged);
    }

    private static Toggle CreateManaged(Container parent, string label, bool value, Action<bool> onChanged) {
        var theme = parent.Theme;
        var root = CreateRow(parent);

        var box = UiFactory.NewRect("DM Checkbox", root);
        box.sizeDelta = new Vector2(24f, 24f);
        var boxLayout = UiFactory.Layout(box);
        boxLayout.minWidth = boxLayout.preferredWidth = 24f;
        boxLayout.minHeight = boxLayout.preferredHeight = 24f;
        boxLayout.flexibleWidth = 0f;
        boxLayout.flexibleHeight = 0f;

        var background = box.gameObject.AddComponent<UIImage>();
        var normal = theme.Button(AssetCatalog.ButtonVariant.Regular, AssetCatalog.ButtonState.Ready);
        if (normal != null) {
            background.sprite = normal;
            background.type = UIImage.Type.Sliced;
            background.color = Color.white;
        }
        else {
            background.color = new Color(0.20f, 0.17f, 0.13f, 0.95f);
        }

        var button = box.gameObject.AddComponent<UButton>();
        button.targetGraphic = background;

        var hoverSprite = theme.Button(AssetCatalog.ButtonVariant.Regular, AssetCatalog.ButtonState.Hovered);
        var pressedSprite = theme.Button(AssetCatalog.ButtonVariant.Regular, AssetCatalog.ButtonState.Pressed);
        if (normal != null && hoverSprite != null && pressedSprite != null) {
            button.transition = Selectable.Transition.SpriteSwap;
            var state = button.spriteState;
            state.highlightedSprite = hoverSprite;
            state.pressedSprite = pressedSprite;
            state.selectedSprite = normal;
            button.spriteState = state;
        }
        else {
            button.transition = Selectable.Transition.ColorTint;
        }

        var check = UiFactory.AddImage(box, "DM Check", theme.Icon("Misc-Check"), Color.white, false);
        check.type = UIImage.Type.Simple;
        check.rectTransform.anchorMin = Vector2.zero;
        check.rectTransform.anchorMax = Vector2.one;
        check.rectTransform.offsetMin = new Vector2(4f, 4f);
        check.rectTransform.offsetMax = new Vector2(-4f, -4f);
        check.preserveAspect = true;

        var text = UiFactory.AddText(root, "DM Label", label ?? string.Empty, 16f,
                                     TextAlignmentOptions.MidlineLeft, theme);
        var textLayout = UiFactory.Layout(text.rectTransform);
        textLayout.minHeight = textLayout.preferredHeight = 24f;
        textLayout.flexibleWidth = 1f;
        textLayout.flexibleHeight = 0f;

        var result = new Toggle(root, check, text, value, onChanged);

        var relay = box.gameObject.AddComponent<ClickRelay>();
        ClickRelayRegistry.Bind(relay, button, result.HandleClick);

        return result;
    }

    private static RectTransform CreateRow(Container parent) {
        var root = UiFactory.NewRect("DM Toggle", parent.Root);
        var row = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 8f;
        row.padding = new RectOffset(0, 0, 0, 0);
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;

        var rootLayout = UiFactory.Layout(root);
        rootLayout.minHeight = rootLayout.preferredHeight = 32f;
        rootLayout.flexibleWidth = 1f;
        rootLayout.flexibleHeight = 0f;
        return root;
    }

    public bool Value => _native != null && _native.IsAlive ? _native.Value : _value;

    public Toggle Set(bool value) {
        _value = value;
        ApplyValue();
        return this;
    }

    public Toggle Text(string value) {
        if (_label != null) _label.text = value ?? string.Empty;
        return this;
    }

    public Toggle Interactable(bool on = true) {
        if (_native != null) _native.SetInteractable(on);
        return this;
    }

    private void HandleClick() {
        Set(!_value);
        try { _onChanged?.Invoke(_value); }
        catch (Exception e) { UiLog.Error("toggle change handler: " + e); }
    }

    private void HandleNativeChange(bool value) {
        _value = value;
        try { _onChanged?.Invoke(value); }
        catch (Exception e) { UiLog.Error("toggle change handler: " + e); }
    }

    private void ApplyValue() {
        if (_native != null) _native.SetSilently(_value);
        else if (_check != null) _check.enabled = _value;
    }
}
