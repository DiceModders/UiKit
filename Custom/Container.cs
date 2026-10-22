using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DiceModders.UiKit.Custom;

/// <summary>A layout container. Add widgets with the methods below; they are placed by the container's layout group.</summary>
public sealed class Container : Element {
    private readonly LayoutGroup _layout;

    internal Container(RectTransform root, LayoutGroup layout, UiTheme theme, UiKitRuntime runtime,
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
    internal Action ChildAdded { get; set; }

    public Text Text(string text) => Custom.Text.Create(Root, text, false, Theme);
    public Text Paragraph(string text) => Custom.Text.Create(Root, text, true, Theme);

    public Button Button(string text, Action onClick = null) {
        var button = Custom.Button.Create(this, text, onClick, null, false);
        if (button != null) ChildAdded?.Invoke();
        return button;
    }

    public Button IconButton(string sprite, Action onClick = null, string tooltip = null) {
        var button = Custom.Button.Create(this, string.Empty, onClick, sprite, true, tooltip);
        if (button != null) {
            button.Tooltip(tooltip);
            ChildAdded?.Invoke();
        }
        return button;
    }

    public Toggle Toggle(string label, bool value, Action<bool> onChanged = null)
        => Custom.Toggle.Create(this, label, value, onChanged);

    public Slider Slider(float min, float max, float value, Action<float> onChanged = null)
        => Custom.Slider.Create(this, min, max, value, onChanged);

    public Dropdown Dropdown(IEnumerable<string> options, int index = 0, Action<int> onChanged = null)
        => Custom.Dropdown.Create(this, options, index, onChanged);

    public Image Image(Texture texture = null) => Custom.Image.Create(this, texture);

    public Scroll Scroll(Action<Container> build) => Custom.Scroll.Create(this, build);

    public Container Row(Action<Container> build) {
        var rect = UiFactory.NewRect("DM Row", Root);
        var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        Configure(layout, horizontal: true);
        var container = new Container(rect, layout, Theme, Runtime);
        build?.Invoke(container);
        return container;
    }

    public Container Column(Action<Container> build) {
        var rect = UiFactory.NewRect("DM Column", Root);
        var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        Configure(layout, horizontal: false);
        var container = new Container(rect, layout, Theme, Runtime);
        build?.Invoke(container);
        return container;
    }

    public Container Grid(int columns, Action<Container> build)
        => Grid(columns, 100f, 100f, build);

    public Container Grid(int columns, float cellWidth, float cellHeight, Action<Container> build) {
        var rect = UiFactory.NewRect("DM Grid", Root);
        var layout = rect.gameObject.AddComponent<GridLayoutGroup>();
        layout.spacing = new Vector2(6f, 6f);
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = Mathf.Max(1, columns);
        layout.cellSize = new Vector2(cellWidth, cellHeight);

        var element = UiFactory.Layout(rect);
        element.flexibleWidth = 1f;

        var container = new Container(rect, layout, Theme, Runtime);
        build?.Invoke(container);
        return container;
    }

    public Container Spacer(float height = 8f) {
        var rect = UiFactory.NewRect("DM Spacer", Root);
        var element = UiFactory.Layout(rect);
        element.minHeight = element.preferredHeight = Mathf.Max(0f, height);
        element.flexibleWidth = 0f;
        element.flexibleHeight = 0f;
        return this;
    }

    public Container Separator() {
        var image = UiFactory.AddImage(Root, "DM Separator", null, new Color(1f, 1f, 1f, 0.1f), false);
        var element = UiFactory.Layout(image.rectTransform);
        element.minHeight = element.preferredHeight = 1f;
        element.flexibleWidth = 1f;
        element.flexibleHeight = 0f;
        return this;
    }

    public Container Spacing(float spacing) {
        if (_layout is HorizontalLayoutGroup horizontal) horizontal.spacing = spacing;
        else if (_layout is VerticalLayoutGroup vertical) vertical.spacing = spacing;
        else if (_layout is GridLayoutGroup grid) grid.spacing = new Vector2(spacing, spacing);
        return this;
    }

    public Container Padding(float all) => Padding(all, all);

    public Container Padding(float horizontal, float vertical) {
        var padding = new RectOffset(
            Mathf.RoundToInt(horizontal), Mathf.RoundToInt(horizontal),
            Mathf.RoundToInt(vertical), Mathf.RoundToInt(vertical));

        if (_layout is HorizontalLayoutGroup h) h.padding = padding;
        else if (_layout is VerticalLayoutGroup v) v.padding = padding;
        else if (_layout is GridLayoutGroup g) g.padding = padding;
        return this;
    }

    public Container Columns(int columns) {
        if (_layout is GridLayoutGroup grid) {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, columns);
        }
        return this;
    }

    public Container CellSize(float width, float height) {
        if (_layout is GridLayoutGroup grid) grid.cellSize = new Vector2(width, height);
        return this;
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
