using System;
using System.Globalization;
using DiceModders.UiKit.Native;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UIImage = UnityEngine.UI.Image;
using USlider = UnityEngine.UI.Slider;

namespace DiceModders.UiKit.Custom;

/// <summary>
/// A slider with a value label. Uses a clone of the game's native float setting control when available; otherwise a managed
/// slider is built from the game's sprites.
/// </summary>
public sealed class Slider : Element {
    private readonly USlider _slider;
    private readonly TMP_Text _label;
    private readonly Action<float> _onChanged;
    private string _format = "0.##";

    internal Slider(RectTransform root, USlider slider, TMP_Text label, Action<float> onChanged) : base(root) {
        _slider = slider;
        _label = label;
        _onChanged = onChanged;
    }

    internal static Slider Create(Container parent, float min, float max, float value, Action<float> onChanged) {
        var runtime = parent.Runtime;

        if (SkinPolicy.AllowNative(runtime)) {
            var native = SkinPolicy.TryNative("slider", () =>
                NativeSlider.Create(runtime.Templates, parent.Root, min, max, value));
            if (native != null) {
                if (native.ValueText == null) {
                    var fallback = UiFactory.AddText(native.Rt, "DM Value", string.Empty, 14f,
                                                     TextAlignmentOptions.MidlineRight, parent.Theme);
                    var fallbackLayout = UiFactory.Layout(fallback.rectTransform);
                    fallbackLayout.minWidth = fallbackLayout.preferredWidth = 44f;
                    fallbackLayout.minHeight = fallbackLayout.preferredHeight = 24f;
                    fallbackLayout.flexibleWidth = 0f;
                    fallbackLayout.flexibleHeight = 0f;
                    native.ValueText = fallback;
                }

                var layout = UiFactory.Layout(native.Rt);
                if (layout != null) {
                    layout.minWidth = 160f;
                    layout.preferredHeight = layout.minHeight = native.Height;
                    layout.flexibleWidth = 1f;
                    layout.flexibleHeight = 0f;
                }

                var result = new Slider(native.Rt, native.Slider, native.ValueText, onChanged);
                result.UpdateLabel();

                var relay = native.Rt.gameObject.AddComponent<SliderRelay>();
                SliderRelayRegistry.Bind(relay, native.Slider, result.HandleValue);
                return result;
            }

            if (!SkinPolicy.MissingNative(runtime, "slider"))
                return null;
        }

        return CreateManaged(parent, min, max, value, onChanged);
    }

    private static Slider CreateManaged(Container parent, float min, float max, float value, Action<float> onChanged) {
        var theme = parent.Theme;

        var root = UiFactory.NewRect("DM Slider Row", parent.Root);
        var row = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 8f;
        row.padding = new RectOffset(0, 0, 0, 0);
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = true;

        var rootLayout = UiFactory.Layout(root);
        rootLayout.minHeight = rootLayout.preferredHeight = 32f;
        rootLayout.flexibleWidth = 1f;
        rootLayout.flexibleHeight = 0f;

        var area = UiFactory.NewRect("DM Slider", root);
        var areaLayout = UiFactory.Layout(area);
        areaLayout.minHeight = areaLayout.preferredHeight = 32f;
        areaLayout.flexibleWidth = 1f;
        areaLayout.flexibleHeight = 0f;

        var background = area.gameObject.AddComponent<UIImage>();
        background.color = new Color(0.16f, 0.14f, 0.12f, 0.95f);
        background.raycastTarget = true;

        var fillArea = UiFactory.NewRect("DM Fill Area", area);
        fillArea.anchorMin = new Vector2(0f, 0f);
        fillArea.anchorMax = new Vector2(1f, 1f);
        fillArea.offsetMin = new Vector2(6f, 6f);
        fillArea.offsetMax = new Vector2(-6f, -6f);

        var fill = UiFactory.NewRect("DM Fill", fillArea);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        var fillImage = fill.gameObject.AddComponent<UIImage>();
        fillImage.color = new Color(0.78f, 0.61f, 0.34f, 1f);
        fillImage.raycastTarget = false;

        var handleArea = UiFactory.NewRect("DM Handle Area", area);
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(8f, 0f);
        handleArea.offsetMax = new Vector2(-8f, 0f);

        var handle = UiFactory.NewRect("DM Handle", handleArea);
        handle.sizeDelta = new Vector2(14f, 24f);
        var handleImage = handle.gameObject.AddComponent<UIImage>();
        handleImage.color = new Color(0.93f, 0.89f, 0.80f, 1f);
        handleImage.raycastTarget = false;

        var slider = area.gameObject.AddComponent<USlider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = background;
        slider.direction = USlider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max > min ? max : min + 1f;
        slider.wholeNumbers = false;
        slider.value = Mathf.Clamp(value, slider.minValue, slider.maxValue);

        var label = UiFactory.AddText(root, "DM Value", string.Empty, 14f,
                                      TextAlignmentOptions.MidlineRight, theme);
        var labelLayout = UiFactory.Layout(label.rectTransform);
        labelLayout.minWidth = labelLayout.preferredWidth = 44f;
        labelLayout.minHeight = labelLayout.preferredHeight = 24f;
        labelLayout.flexibleWidth = 0f;
        labelLayout.flexibleHeight = 0f;

        var result = new Slider(root, slider, label, onChanged);
        result.UpdateLabel();

        var relay = area.gameObject.AddComponent<SliderRelay>();
        SliderRelayRegistry.Bind(relay, slider, result.HandleValue);

        return result;
    }

    public float Value => _slider != null ? _slider.value : 0f;

    public Slider Set(float value) {
        if (_slider == null) return this;
        _slider.SetValueWithoutNotify(Mathf.Clamp(value, _slider.minValue, _slider.maxValue));
        UpdateLabel();
        return this;
    }

    public Slider Range(float min, float max) {
        if (_slider == null) return this;

        _slider.minValue = min;
        _slider.maxValue = max > min ? max : min + 1f;
        _slider.SetValueWithoutNotify(Mathf.Clamp(_slider.value, _slider.minValue, _slider.maxValue));
        UpdateLabel();
        return this;
    }

    public Slider Format(string format) {
        _format = string.IsNullOrEmpty(format) ? "0.##" : format;
        UpdateLabel();
        return this;
    }

    public Slider WholeNumbers(bool on = true) {
        if (_slider != null) _slider.wholeNumbers = on;
        UpdateLabel();
        return this;
    }

    public Slider Interactable(bool on = true) {
        if (_slider != null) _slider.interactable = on;
        return this;
    }

    private void HandleValue(float value) {
        UpdateLabel();
        try { _onChanged?.Invoke(value); }
        catch (Exception e) { UiLog.Error("slider change handler: " + e); }
    }

    private void UpdateLabel() {
        if (_label == null || _slider == null) return;
        _label.text = _slider.value.ToString(_format, CultureInfo.InvariantCulture);
    }
}
