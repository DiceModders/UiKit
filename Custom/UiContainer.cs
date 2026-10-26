using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DiceModders.UiKit.Custom;

/// <summary>
/// A layout container. Add widgets with the methods below; they are placed by the container's layout group.
/// Factory methods return <c>null</c> when the container is no longer alive or a widget cannot be created.
/// </summary>
public sealed class UiContainer : UiElement {
    private readonly LayoutGroup _layout;

    internal UiContainer(RectTransform root, LayoutGroup layout, UiTheme theme, UiKitRuntime runtime,
                       bool textSizedButtons = false)
        : base(root) {
        _layout = layout;
        Theme = theme;
        Runtime = runtime;
        TextSizedButtons = textSizedButtons;
    }

    internal UiTheme Theme { get; }
    internal UiKitRuntime Runtime { get; }
    internal bool TextSizedButtons { get; }
    internal Action? ChildAdded { get; set; }

    public UiText? Text(string? text) {
        if (!EnsureAlive()) return null;
        return UiGuard.Func("container.text", () => Custom.UiText.Create(Root, text, false, Theme));
    }

    public UiText? Paragraph(string? text) {
        if (!EnsureAlive()) return null;
        return UiGuard.Func("container.paragraph", () => Custom.UiText.Create(Root, text, true, Theme));
    }

    public UiButton? Button(string? text, Action? onClick = null) {
        if (!EnsureAlive()) return null;
        var button = UiGuard.Func("container.button", () => Custom.UiButton.Create(this, text, onClick, null, false));
        NotifyChildAdded(button != null);
        return button;
    }

    public UiButton? IconButton(string? sprite, Action? onClick = null, string? tooltip = null) {
        if (!EnsureAlive()) return null;
        var button = UiGuard.Func("container.icon-button",
            () => Custom.UiButton.Create(this, string.Empty, onClick, sprite, true, tooltip));
        if (button != null) {
            UiGuard.Run("container.icon-button.tooltip", () => button.Tooltip(tooltip));
            NotifyChildAdded(true);
        }
        return button;
    }

    /// <summary>Adds an icon button from a caller-supplied sprite (for example a texture created at runtime).</summary>
    public UiButton? IconButton(Sprite? icon, Action? onClick = null, string? tooltip = null) {
        if (!EnsureAlive()) return null;
        var button = UiGuard.Func("container.icon-button",
            () => Custom.UiButton.Create(this, string.Empty, onClick, null, true, tooltip, icon));
        if (button != null) {
            UiGuard.Run("container.icon-button.tooltip", () => button.Tooltip(tooltip));
            NotifyChildAdded(true);
        }
        return button;
    }

    public UiToggle? Toggle(string? label, bool value, Action<bool>? onChanged = null) {
        if (!EnsureAlive()) return null;
        return UiGuard.Func("container.toggle", () => Custom.UiToggle.Create(this, label, value, onChanged));
    }

    public UiSlider? Slider(float min, float max, float value, Action<float>? onChanged = null) {
        if (!EnsureAlive()) return null;
        return UiGuard.Func("container.slider", () => Custom.UiSlider.Create(this, min, max, value, onChanged));
    }

    public UiDropdown? Dropdown(IEnumerable<string>? options, int index = 0, Action<int>? onChanged = null) {
        if (!EnsureAlive()) return null;
        return UiGuard.Func("container.dropdown", () => Custom.UiDropdown.Create(this, options, index, onChanged));
    }

    public UiImage? Image(Texture? texture = null) {
        if (!EnsureAlive()) return null;
        return UiGuard.Func("container.image", () => Custom.UiImage.Create(this, texture));
    }

    public UiScroll? Scroll(Action<UiContainer>? build) {
        if (!EnsureAlive()) return null;
        return UiGuard.Func("container.scroll", () => Custom.UiScroll.Create(this, build));
    }

    public UiContainer? Row(Action<UiContainer>? build) {
        return BuildGroup("DM Row", rect => {
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            Configure(layout, horizontal: true);
            return layout;
        }, build);
    }

    public UiContainer? Column(Action<UiContainer>? build) {
        return BuildGroup("DM Column", rect => {
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            Configure(layout, horizontal: false);
            return layout;
        }, build);
    }

    public UiContainer? Grid(Action<UiContainer>? build = null) {
        if (!EnsureAlive()) return null;
        return BuildGroup("DM Grid", rect => {
            var layout = rect.gameObject.AddComponent<GridLayoutGroup>();
            layout.spacing = new Vector2(6f, 6f);
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.constraint = GridLayoutGroup.Constraint.Flexible;
            layout.cellSize = new Vector2(100f, 100f);

            var element = UiFactory.Layout(rect);
            if (element != null) element.flexibleWidth = 1f;
            return layout;
        }, build);
    }

    public UiContainer? Grid(int columns, Action<UiContainer>? build)
        => Grid(columns, 100f, 100f, build);

    public UiContainer? Grid(int columns, float cellWidth, float cellHeight, Action<UiContainer>? build) {
        int columnCount = Mathf.Clamp(columns, 1, 100);
        float width = Mathf.Max(0f, UiGuard.Finite(cellWidth, 100f));
        float height = Mathf.Max(0f, UiGuard.Finite(cellHeight, 100f));

        return BuildGroup("DM Grid", rect => {
            var layout = rect.gameObject.AddComponent<GridLayoutGroup>();
            layout.spacing = new Vector2(6f, 6f);
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columnCount;
            layout.cellSize = new Vector2(width, height);

            var element = UiFactory.Layout(rect);
            if (element != null) element.flexibleWidth = 1f;
            return layout;
        }, build);
    }

    public UiContainer Spacer(float height = 8f) {
        if (!EnsureAlive()) return this;

        UiGuard.Run("container.spacer", () => {
            var rect = UiFactory.NewRect("DM Spacer", Root);
            var element = UiFactory.Layout(rect);
            if (element == null) return;
            element.minHeight = element.preferredHeight = Mathf.Max(0f, UiGuard.Finite(height, 8f));
            element.flexibleWidth = 0f;
            element.flexibleHeight = 0f;
        });
        return this;
    }

    public UiContainer Separator(float height = 1f) {
        if (!EnsureAlive()) return this;

        UiGuard.Run("container.separator", () => {
            var image = UiFactory.AddImage(Root, "DM Separator", null, new Color(1f, 1f, 1f, 0.1f), false);
            if (image == null) return;
            var element = UiFactory.Layout(image.rectTransform);
            if (element == null) return;
            float value = Mathf.Max(0f, UiGuard.Finite(height, 1f));
            element.minHeight = element.preferredHeight = value;
            element.flexibleWidth = 1f;
            element.flexibleHeight = 0f;
        });
        return this;
    }

    public UiContainer Spacing(float spacing) => Spacing(spacing, spacing);

    /// <summary>Sets horizontal and vertical spacing independently. Grid and column/row layouts use the relevant axis.</summary>
    public UiContainer Spacing(float horizontal, float vertical) {
        if (!EnsureAlive()) return this;

        UiGuard.Run("container.spacing", () => {
            float x = Mathf.Max(0f, UiGuard.Finite(horizontal, 0f));
            float y = Mathf.Max(0f, UiGuard.Finite(vertical, 0f));
            if (_layout is HorizontalLayoutGroup h) h.spacing = x;
            else if (_layout is VerticalLayoutGroup v) v.spacing = y;
            else if (_layout is GridLayoutGroup grid) grid.spacing = new Vector2(x, y);
        });
        return this;
    }

    public UiContainer Padding(float all) => Padding(all, all);

    public UiContainer Padding(float horizontal, float vertical) {
        if (!EnsureAlive()) return this;

        UiGuard.Run("container.padding", () => {
            int x = Mathf.Clamp(Mathf.RoundToInt(UiGuard.Finite(horizontal, 0f)), 0, 10000);
            int y = Mathf.Clamp(Mathf.RoundToInt(UiGuard.Finite(vertical, 0f)), 0, 10000);
            var padding = new RectOffset(x, x, y, y);

            if (_layout is HorizontalLayoutGroup h) h.padding = padding;
            else if (_layout is VerticalLayoutGroup v) v.padding = padding;
            else if (_layout is GridLayoutGroup g) g.padding = padding;
        });
        return this;
    }

    public UiContainer Columns(int columns) {
        if (!EnsureAlive()) return this;

        UiGuard.Run("container.columns", () => {
            if (_layout is GridLayoutGroup grid) {
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = Mathf.Clamp(columns, 1, 100);
            }
        });
        return this;
    }

    public UiContainer CellSize(float width, float height) {
        if (!EnsureAlive()) return this;

        UiGuard.Run("container.cell-size", () => {
            if (_layout is GridLayoutGroup grid)
                grid.cellSize = new Vector2(
                    Mathf.Max(0f, UiGuard.Finite(width, 0f)),
                    Mathf.Max(0f, UiGuard.Finite(height, 0f)));
        });
        return this;
    }

    /// <summary>Removes all child objects added through this container.</summary>
    public UiContainer Clear() {
        if (!EnsureAlive()) return this;

        UiGuard.Run("container.clear", () => {
            var root = Root;
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--) {
                var child = root.GetChild(i);
                if (child == null) continue;
                child.gameObject.SetActive(false);
                UiFactory.Destroy(child.gameObject);
            }
        });
        return this;
    }

    /// <summary>Removes one element if it currently belongs to this container.</summary>
    public UiContainer Remove(UiElement? element) {
        if (element == null || !IsAlive) return this;

        UiGuard.Run("container.remove", () => {
            var root = element.Root;
            var self = Root;
            if (root != null && self != null && root.IsChildOf(self)) element.Dispose();
        });
        return this;
    }

    private UiContainer? BuildGroup(string name, Func<RectTransform, LayoutGroup> createLayout, Action<UiContainer>? build) {
        if (!EnsureAlive()) return null;

        RectTransform? rect = null;
        try {
            rect = UiFactory.NewRect(name, Root);
            var layout = createLayout(rect);
            var container = new UiContainer(rect, layout, Theme, Runtime);
            build?.Invoke(container);
            return container.IsAlive ? container : null;
        }
        catch (Exception e) {
            UiGuard.Report("container." + name, e);
            if (rect != null) UiGuard.Run("container.cleanup", () => UiFactory.Destroy(rect.gameObject));
            return null;
        }
    }

    private void NotifyChildAdded(bool added) {
        if (!added) return;
        UiGuard.Run("container.child-added", () => ChildAdded?.Invoke());
    }

    private bool EnsureAlive() {
        if (IsAlive) return true;
        UiLog.Once("container:disposed", "cannot add widgets to a disposed container");
        return false;
    }

    private static void Configure(HorizontalOrVerticalLayoutGroup layout, bool horizontal) {
        layout.spacing = 6f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = !horizontal;
        layout.childForceExpandHeight = horizontal;
    }
}
