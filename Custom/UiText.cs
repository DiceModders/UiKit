using System;
using TMPro;
using UnityEngine;
using UILayout = UnityEngine.UI.LayoutElement;

namespace DiceModders.UiKit.Custom;

/// <summary>
/// A text line. A plain <see cref="UiText"/> is one line cut with "..." when it is too long; call <see cref="Wrap"/> for a paragraph whose
/// height follows the text.
/// </summary>
public sealed class UiText : UiElement {
    private readonly TextMeshProUGUI _text;
    private readonly AutoHeightText _auto;
    private bool _wrap;

    internal UiText(RectTransform root, TextMeshProUGUI text, AutoHeightText auto, bool wrap) : base(root) {
        _text = text;
        _auto = auto;
        _wrap = wrap;
    }

    internal static UiText? Create(RectTransform? parent, string? value, bool wrap, UiTheme? theme) {
        var rect = UiFactory.NewRect(wrap ? "DM Paragraph" : "DM Label", parent);

        try {
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            UiFactory.ApplyFont(text, theme);

            text.text = value ?? string.Empty;
            text.fontSize = 16f;
            text.alignment = wrap ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left;
            text.color = theme != null ? theme.TextColor : AssetCatalog.DefaultTextColor;
            text.raycastTarget = false;
            text.enableWordWrapping = wrap;
            text.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;

            var layout = UiFactory.Layout(rect);
            if (layout == null) { UiFactory.Destroy(rect.gameObject); return null; }
            layout.flexibleWidth = 1f;
            layout.minWidth = 0f;
            layout.preferredWidth = 0f;

            float lineHeight = MeasureLineHeight(text);
            layout.minHeight = layout.preferredHeight = lineHeight;
            layout.flexibleHeight = 0f;

            var watcher = rect.gameObject.AddComponent<WidthWatcher>();
            watcher.Bind(text);

            var auto = rect.gameObject.AddComponent<AutoHeightText>();
            auto.Bind(text, layout);
            auto.SetEnabled(wrap);

            return new UiText(rect, text, auto, wrap);
        }
        catch (Exception e) {
            UiGuard.Report("text.create", e);
            UiFactory.Destroy(rect.gameObject);
            return null;
        }
    }

    internal static float MeasureLineHeight(TMP_Text? text) {
        if (text == null) return 24f;

        try {
            if (TmpTextSafety.TryPreferredValues(text, "Ag", 100000f, 0f, out var preferred)) {
                return Mathf.Ceil(Mathf.Max(preferred.y, text.fontSize * 1.5f)) + 1f;
            }
            return Mathf.Ceil(Mathf.Max(24f, text.fontSize * 1.5f)) + 1f;
        }
        catch {
            return 24f;
        }
    }

    public string Value {
        get => _text != null ? _text.text : string.Empty;
        set {
            if (_text == null) return;
            UiGuard.Run("text.value", () => {
                _text.text = value ?? string.Empty;
                if (_wrap) _auto?.MeasureNow();
                else {
                    _text.SetAllDirty();
                    _text.ForceMeshUpdate();
                }
            });
        }
    }

    public UiText Wrap(bool on = true) {
        if (_text == null || _wrap == on) return this;
        _wrap = on;

        UiGuard.Run("text.wrap", () => {
            _text.enableWordWrapping = on;
            _text.overflowMode = on ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            _auto?.SetEnabled(on);

            var layout = Layout;
            if (layout == null) return;
            layout.flexibleWidth = 1f;
            layout.minWidth = 0f;
            layout.preferredWidth = 0f;
            layout.flexibleHeight = 0f;

            if (on) {
                layout.minHeight = 0f;
                layout.preferredHeight = 0f;
                _auto?.MeasureNow();
            }
            else {
                float height = MeasureLineHeight(_text);
                layout.minHeight = layout.preferredHeight = height;
            }
        });

        return this;
    }

    public UiText Align(TextAlignmentOptions alignment) {
        if (_text == null) return this;
        UiGuard.Run("text.align", () => _text.alignment = alignment);
        return this;
    }

    public UiText Color(Color color) {
        if (_text == null) return this;
        UiGuard.Run("text.color", () => _text.color = color);
        return this;
    }

    public UiText Size(float fontSize) {
        if (_text == null) return this;
        UiGuard.Run("text.size", () => {
            _text.fontSize = UiGuard.Finite(fontSize, 16f);
            if (_wrap) _auto?.MeasureNow();
            else {
                var layout = Layout;
                if (layout != null) {
                    float height = MeasureLineHeight(_text);
                    layout.minHeight = layout.preferredHeight = height;
                }
            }
        });
        return this;
    }
}
