using System;
using DiceModders.UiKit.Native;
using UnityEngine;
using UnityEngine.UI;
using UIImage = UnityEngine.UI.Image;

namespace DiceModders.UiKit.Custom;

/// <summary>A vertical scroll view. Add rows to <see cref="Content"/>; the view stays at the top while the content grows, until the user scrolls.</summary>
public sealed class UiScroll : UiElement {
    private readonly ScrollRect _scroll;
    private readonly UiContainer _content;

    internal UiScroll(RectTransform root, ScrollRect scroll, UiContainer content) : base(root) {
        _scroll = scroll;
        _content = content;
    }

    internal static UiScroll? Create(UiContainer parent, Action<UiContainer>? build) {
        var theme = parent.Theme;

        var root = UiFactory.NewRect("DM ScrollView", parent.Root);
        try {
            var rootLayout = UiFactory.Layout(root);
            if (rootLayout == null) { UiFactory.Destroy(root.gameObject); return null; }
            rootLayout.minHeight = 60f;
            rootLayout.flexibleWidth = 1f;
            rootLayout.flexibleHeight = 1f;

            var rootImage = root.gameObject.AddComponent<UIImage>();
            rootImage.color = new Color(0f, 0f, 0f, 0f);
            rootImage.raycastTarget = true;

            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.scrollSensitivity = 30f;

            var viewport = UiFactory.NewRect("DM Viewport", root);
            UiFactory.Stretch(viewport);
            viewport.offsetMax = new Vector2(-14f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportImage = viewport.gameObject.AddComponent<UIImage>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewportImage.raycastTarget = true;

            var content = UiFactory.NewRect("DM Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;

            var scrollbar = BuildScrollbar(root, parent.Runtime);
            if (scrollbar != null) {
                scroll.verticalScrollbar = scrollbar;
                scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                scroll.verticalScrollbarSpacing = 4f;

                var barRect = scrollbar.GetComponent<RectTransform>();
                float barWidth = barRect != null ? Mathf.Max(barRect.rect.width, barRect.sizeDelta.x) : 14f;
                viewport.offsetMax = new Vector2(-Mathf.Max(14f, barWidth + 4f), 0f);
            }

            var pin = content.gameObject.AddComponent<ScrollPin>();
            pin.Bind(scroll);

            var container = new UiContainer(content, layout, theme, parent.Runtime);
            build?.Invoke(container);
            if (!container.IsAlive) {
                UiFactory.Destroy(root.gameObject);
                return null;
            }

            return new UiScroll(root, scroll, container);
        }
        catch (Exception e) {
            UiGuard.Report("scroll.create", e);
            UiFactory.Destroy(root.gameObject);
            return null;
        }
    }

    private static Scrollbar? BuildScrollbar(RectTransform root, UiKitRuntime runtime) {
        if (SkinPolicy.AllowNative(runtime)) {
            var native = SkinPolicy.TryNative("scrollbar", () =>
                NativeScrollbar.Create(runtime.Templates, root));
            if (native != null) return native;
            if (!SkinPolicy.MissingNative(runtime, "scrollbar")) return null;
        }

        var barRoot = UiFactory.NewRect("DM Scrollbar", root);
        barRoot.anchorMin = new Vector2(1f, 0f);
        barRoot.anchorMax = new Vector2(1f, 1f);
        barRoot.pivot = new Vector2(1f, 0.5f);
        barRoot.sizeDelta = new Vector2(10f, 0f);
        barRoot.anchoredPosition = new Vector2(-2f, 0f);

        var background = barRoot.gameObject.AddComponent<UIImage>();
        background.color = new Color(0.12f, 0.10f, 0.08f, 0.8f);
        background.raycastTarget = true;

        var slide = UiFactory.NewRect("DM Slide Area", barRoot);
        UiFactory.Stretch(slide);
        slide.offsetMin = new Vector2(1f, 1f);
        slide.offsetMax = new Vector2(-1f, -1f);

        var handle = UiFactory.NewRect("DM Handle", slide);
        UiFactory.Stretch(handle);
        var handleImage = handle.gameObject.AddComponent<UIImage>();
        handleImage.color = new Color(0.65f, 0.58f, 0.45f, 1f);
        handleImage.raycastTarget = false;

        var scrollbar = barRoot.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = background;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.size = 0.2f;
        return scrollbar;
    }

    public UiContainer Content => _content;

    /// <summary>0 = top, 1 = bottom.</summary>
    public float Position {
        get => _scroll != null ? 1f - _scroll.verticalNormalizedPosition : 0f;
        set {
            if (_scroll == null) return;
            UiGuard.Run("scroll.position", () => _scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(value));
        }
    }

    public UiScroll ToTop() {
        Position = 0f;
        return this;
    }

    public UiScroll ToBottom() {
        UiGuard.Run("scroll.to-bottom", () => {
            Canvas.ForceUpdateCanvases();
            Position = 1f;
        });
        return this;
    }
}
