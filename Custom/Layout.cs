using UnityEngine.UI;

namespace DiceModders.UiKit.Custom;

/// <summary>Fluent layout helpers. They return the same widget type, or null when the widget was not created.</summary>
public static class Layout {
    public static T? Fixed<T>(this T? element, float width, float height) where T : Element {
        var layout = element?.Layout;
        if (layout == null) return element;

        width = UiGuard.Finite(width, 0f);
        height = UiGuard.Finite(height, 0f);
        layout.minWidth = layout.preferredWidth = width;
        layout.minHeight = layout.preferredHeight = height;
        layout.flexibleWidth = layout.flexibleHeight = 0f;
        return element;
    }

    public static T? Width<T>(this T? element, float width) where T : Element {
        var layout = element?.Layout;
        if (layout == null) return element;

        layout.minWidth = layout.preferredWidth = UiGuard.Finite(width, 0f);
        layout.flexibleWidth = 0f;
        return element;
    }

    public static T? Height<T>(this T? element, float height) where T : Element {
        var layout = element?.Layout;
        if (layout == null) return element;

        layout.minHeight = layout.preferredHeight = UiGuard.Finite(height, 0f);
        layout.flexibleHeight = 0f;
        return element;
    }

    public static T? Fill<T>(this T? element) where T : Element => element.FillWidth()?.FillHeight();

    public static T? FillWidth<T>(this T? element) where T : Element {
        var layout = element?.Layout;
        if (layout == null) return element;

        layout.minWidth = layout.preferredWidth = 0f;
        layout.flexibleWidth = 1f;
        return element;
    }

    public static T? FillHeight<T>(this T? element) where T : Element {
        var layout = element?.Layout;
        if (layout == null) return element;

        layout.minHeight = layout.preferredHeight = 0f;
        layout.flexibleHeight = 1f;
        return element;
    }
}
