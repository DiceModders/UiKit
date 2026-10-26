using UnityEngine;
using UnityEngine.UI;

namespace DiceModders.UiKit.Custom;

/// <summary>
/// Fluent layout helpers for fixed sizes and weighted shares. Weighted sizing applies inside a Row, Column or
/// Window.Body; top-level windows keep their explicit Size and grid cells keep their CellSize.
/// </summary>
public static class UiLayout {
    public static T? Fixed<T>(this T? element, float width, float height) where T : UiElement {
        var layout = element?.Layout;
        if (layout == null) return element;

        width = UiGuard.Finite(width, 0f);
        height = UiGuard.Finite(height, 0f);
        layout.minWidth = layout.preferredWidth = width;
        layout.minHeight = layout.preferredHeight = height;
        layout.flexibleWidth = layout.flexibleHeight = 0f;
        return element;
    }

    /// <summary>Fixes both dimensions to the same size.</summary>
    public static T? Fixed<T>(this T? element, float size) where T : UiElement => element.Fixed(size, size);

    /// <summary>Fixes the width. Alias for <see cref="Width{T}"/>.</summary>
    public static T? FixedW<T>(this T? element, float width) where T : UiElement => element.Width(width);

    /// <summary>Fixes the height. Alias for <see cref="Height{T}"/>.</summary>
    public static T? FixedH<T>(this T? element, float height) where T : UiElement => element.Height(height);

    public static T? Width<T>(this T? element, float width) where T : UiElement {
        var layout = element?.Layout;
        if (layout == null) return element;

        layout.minWidth = layout.preferredWidth = UiGuard.Finite(width, 0f);
        layout.flexibleWidth = 0f;
        return element;
    }

    public static T? Height<T>(this T? element, float height) where T : UiElement {
        var layout = element?.Layout;
        if (layout == null) return element;

        layout.minHeight = layout.preferredHeight = UiGuard.Finite(height, 0f);
        layout.flexibleHeight = 0f;
        return element;
    }

    /// <summary>Shares the free width and height proportionally to the weight.</summary>
    public static T? Weight<T>(this T? element, float weight) where T : UiElement {
        var layout = element?.Layout;
        if (layout == null) return element;

        weight = Mathf.Max(0f, UiGuard.Finite(weight, 0f));
        layout.minWidth = layout.preferredWidth = 0f;
        layout.minHeight = layout.preferredHeight = 0f;
        layout.flexibleWidth = layout.flexibleHeight = weight;
        return element;
    }

    /// <summary>Shares the free width proportionally to the weight.</summary>
    public static T? WeightW<T>(this T? element, float weight) where T : UiElement {
        var layout = element?.Layout;
        if (layout == null) return element;

        layout.minWidth = layout.preferredWidth = 0f;
        layout.flexibleWidth = Mathf.Max(0f, UiGuard.Finite(weight, 0f));
        return element;
    }

    /// <summary>Shares the free height proportionally to the weight.</summary>
    public static T? WeightH<T>(this T? element, float weight) where T : UiElement {
        var layout = element?.Layout;
        if (layout == null) return element;

        layout.minHeight = layout.preferredHeight = 0f;
        layout.flexibleHeight = Mathf.Max(0f, UiGuard.Finite(weight, 0f));
        return element;
    }

    public static T? Fill<T>(this T? element) where T : UiElement => element.Weight(1f);

    public static T? FillWidth<T>(this T? element) where T : UiElement => element.WeightW(1f);

    public static T? FillHeight<T>(this T? element) where T : UiElement => element.WeightH(1f);
}
