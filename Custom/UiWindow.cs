using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UIImage = UnityEngine.UI.Image;

namespace DiceModders.UiKit.Custom;

/// <summary>
/// A draggable window with a body, an optional title strip and an optional frame. It is both the builder and the handle.
/// </summary>
public sealed class UiWindow : UiElement {
    public const float DefaultWidth = 400f;
    public const float DefaultHeight = 260f;
    public const float Padding = 16f;
    public const float MinWidth = 160f;
    public const float MinHeight = 100f;
    private const float TitleTop = 10f;
    private const float TitleHeight = 32f;
    private const float TitleGap = 6f;

    private readonly UiScope _scope;
    private readonly UiContainer _body = null!;
    private readonly UIImage _background = null!;
    private readonly UIImage _ornament = null!;

    private UiContainer? _titleButtons;
    private RectTransform? _titleStrip;
    private TextMeshProUGUI? _titleText;
    private UiButton? _closeButton;
    private WindowDragHandler? _drag;
    private Action? _onClose;

    private Layer _layer;
    private OrnamentStyle _ornamentStyle = OrnamentStyle.Border;
    private float _width = DefaultWidth;
    private float _height = DefaultHeight;
    private Vector2 _position;
    private bool _draggable;
    private bool _fullscreenable;
    private bool _fullscreen;
    private bool _closed;
    private int? _sortingOrder;
    private bool _ordersListening;

    private Vector2 _normalAnchorMin;
    private Vector2 _normalAnchorMax;
    private Vector2 _normalPivot;
    private Vector2 _normalPosition;
    private Vector2 _normalSize;
    private bool _hasNormal;

    internal UiWindow(UiScope scope) : base(CreateRoot(scope)) {
        _scope = scope;
        _layer = scope.Runtime.Options.DefaultLayer;

        try {
            var theme = scope.Runtime.Theme;
            var root = Root;
            if (root == null) throw new InvalidOperationException("window root is unavailable");
            _background = root.gameObject.AddComponent<UIImage>();
            _background.sprite = theme.Panel;
            _background.type = _background.sprite != null ? UIImage.Type.Sliced : UIImage.Type.Simple;
            _background.color = _background.sprite != null ? Color.white : new Color(0.12f, 0.10f, 0.08f, 0.98f);
            _background.raycastTarget = true;

            _body = BuildBody(theme);
            _ornament = BuildOrnament(theme);

            int index = scope.Runtime.NextWindowIndex();
            _position = new Vector2(60f + index * 28f, 60f + index * 28f);
            ApplyNormalLayout();
        }
        catch (Exception e) {
            UiGuard.Report("window.construct", e, scope.Id);
            base.Dispose();
            throw;
        }
    }

    private static RectTransform CreateRoot(UiScope scope) {
        RectTransform? root = null;
        try {
            var layer = scope.Runtime.Options.DefaultLayer;
            var canvas = scope.Runtime.Layers.Get(layer);
            if (canvas == null) throw new InvalidOperationException("window layer canvas is unavailable");
            root = UiFactory.NewRect("DM Window", canvas.transform);
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            return root;
        }
        catch {
            if (root != null) UiFactory.Destroy(root.gameObject);
            throw;
        }
    }

    private UiContainer BuildBody(UiTheme theme) {
        var rect = UiFactory.NewRect("DM Body", Root);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(Padding, Padding);
        rect.offsetMax = new Vector2(-Padding, -Padding);
        rect.gameObject.AddComponent<RectMask2D>();

        var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        return new UiContainer(rect, layout, theme, _scope.Runtime);
    }

    private UIImage BuildOrnament(UiTheme theme) {
        var rect = UiFactory.NewRect("DM Ornament", Root);
        UiFactory.Stretch(rect);

        var image = rect.gameObject.AddComponent<UIImage>();
        image.type = UIImage.Type.Sliced;
        image.raycastTarget = false;
        image.sprite = theme.Ornament(_ornamentStyle);
        image.enabled = image.sprite != null;
        return image;
    }

    public UiContainer Body => _body;
    public Layer CurrentLayer => _layer;
    public bool IsVisible => IsAlive && Root != null && Root.gameObject.activeSelf;
    public bool IsFullscreen => _fullscreen;
    public bool CanFullscreen => _fullscreenable;

    public UiContainer? TitleBar {
        get {
            if (!IsAlive) return null;
            EnsureTitle();
            return _titleButtons;
        }
    }

    public UiWindow Title(string? text) {
        if (!IsAlive) return this;
        EnsureTitle();
        if (_titleText != null) _titleText.text = text ?? string.Empty;
        return this;
    }

    public UiWindow Size(float width, float height) {
        if (!IsAlive) return this;

        _width = Mathf.Max(MinWidth, UiGuard.Finite(width, DefaultWidth));
        _height = Mathf.Max(MinHeight, UiGuard.Finite(height, DefaultHeight));
        if (!_fullscreen) ApplyNormalLayout();
        return this;
    }

    public UiWindow Position(float x, float y) {
        if (!IsAlive) return this;

        _position = new Vector2(UiGuard.Finite(x, 0f), UiGuard.Finite(y, 0f));
        if (!_fullscreen) ApplyNormalLayout();
        return this;
    }

    public UiWindow Center() {
        if (!IsAlive || Root == null) return this;

        UiGuard.Run("window.center", () => {
            var parent = Root.parent != null ? Root.parent.TryCast<RectTransform>() : null;
            if (parent == null) return;

            _position = new Vector2(
                Mathf.Max(0f, (parent.rect.width - _width) * 0.5f),
                Mathf.Max(0f, (parent.rect.height - _height) * 0.5f));
            if (!_fullscreen) ApplyNormalLayout();
        }, _scope.Id);
        return this;
    }

    public UiWindow Layer(Layer layer) {
        if (!IsAlive || Root == null || _layer == layer) return this;

        var position = Root.anchoredPosition;
        var size = Root.sizeDelta;
        var anchorMin = Root.anchorMin;
        var anchorMax = Root.anchorMax;
        var pivot = Root.pivot;

        var parent = UiGuard.Func("window.layer", () =>
            _scope.Runtime.Layers.Get(layer)?.transform, _scope.Id);
        if (parent == null) return this;

        try {
            Root.SetParent(parent, false);
            _layer = layer;

            if (_fullscreen) {
                ApplyFullscreenLayout();
            }
            else {
                Root.anchorMin = anchorMin;
                Root.anchorMax = anchorMax;
                Root.pivot = pivot;
                Root.anchoredPosition = position;
                Root.sizeDelta = size;
            }

            ApplySorting();
        }
        catch (Exception e) {
            UiGuard.Report("window.layer", e, _scope.Id);
        }
        return this;
    }

    public UiWindow SortingOrder(int order) {
        if (!IsAlive) return this;

        _sortingOrder = order;
        ListenForLayerChanges();
        ApplySorting();
        return this;
    }

    public UiWindow Draggable(bool on = true) {
        if (!IsAlive) return this;
        _draggable = on;
        ApplyDraggable();
        return this;
    }

    public UiWindow Fullscreenable(bool on = true) {
        if (!IsAlive) return this;
        _fullscreenable = on;
        return this;
    }

    public UiWindow Closable(bool on = true) {
        if (!IsAlive) return this;
        EnsureTitle();
        if (_titleButtons == null) return this;

        if (!on) {
            _closeButton?.Dispose();
            _closeButton = null;
            SetTitleInsets();
            return this;
        }

        if (_closeButton != null) return this;

        _closeButton = _scope.Runtime.Theme.Icon("Misc-Cross") != null
            ? _titleButtons.IconButton("Misc-Cross", Close, "Close")
            : _titleButtons.Button("×", Close)?.Width(32f);

        if (_closeButton == null) {
            SetTitleInsets();
            return this;
        }

        EnsureCloseLast();
        SetTitleInsets();
        return this;
    }

    public UiWindow Ornament(OrnamentStyle style) {
        if (!IsAlive) return this;
        _ornamentStyle = style;
        if (_ornament == null) return this;

        var sprite = UiGuard.Func("window.ornament", () => _scope.Runtime.Theme.Ornament(style), _scope.Id);
        _ornament.sprite = sprite;
        _ornament.enabled = sprite != null;
        return this;
    }

    public UiWindow OnClose(Action? action) {
        _onClose = action;
        return this;
    }

    public void Show() {
        if (!IsAlive || Root == null) return;
        UiGuard.Run("window.show", () => Root.gameObject.SetActive(true), _scope.Id);
    }

    public void Hide() {
        if (!IsAlive || Root == null) return;
        UiGuard.Run("window.hide", () => Root.gameObject.SetActive(false), _scope.Id);
    }

    public void Toggle() {
        if (!IsAlive || Root == null) return;
        UiGuard.Run("window.toggle", () => Root.gameObject.SetActive(!Root.gameObject.activeSelf), _scope.Id);
    }

    public void BringToFront() {
        if (!IsAlive || Root == null) return;
        UiGuard.Run("window.bring-to-front", () => Root.SetAsLastSibling(), _scope.Id);
    }

    public void Fullscreen() {
        if (!_fullscreenable || _fullscreen || !IsAlive) return;

        UiGuard.Run("window.fullscreen", () => {
            SaveNormal();
            _fullscreen = true;
            ApplyFullscreenLayout();
            _drag?.SetCanDrag(false);
            BringToFront();
        }, _scope.Id);
    }

    public void Restore() {
        if (!_fullscreen || !IsAlive || Root == null) return;

        UiGuard.Run("window.restore", () => {
            _fullscreen = false;

            if (_hasNormal) {
                Root.anchorMin = _normalAnchorMin;
                Root.anchorMax = _normalAnchorMax;
                Root.pivot = _normalPivot;
                Root.anchoredPosition = _normalPosition;
                Root.sizeDelta = _normalSize;
            }
            else {
                ApplyNormalLayout();
            }

            _drag?.SetCanDrag(_draggable);
        }, _scope.Id);
    }

    public void Close() {
        if (_closed) return;
        _closed = true;

        UiGuard.Callback("window.close", _onClose);
        Dispose();
    }

    public override void Dispose() {
        _closed = true;
        UnlistenFromLayerChanges();

        if (_drag != null) {
            UiGuard.Run("window.drag-cleanup", () => UiFactory.Destroy(_drag), _scope.Id);
            _drag = null;
        }

        _closeButton = null;
        _titleButtons = null;
        _titleStrip = null;
        _titleText = null;
        _onClose = null;

        UiGuard.Run("window.untrack", () => _scope.RemoveOwned(this), _scope.Id);
        base.Dispose();
    }

    internal void FinalizeBuild() {
        if (!IsAlive) return;

        UiGuard.Run("window.finalize", () => {
            ApplyNormalLayout();
            ApplyDraggable();
            ApplySorting();
        }, _scope.Id);
    }

    internal void HandleDoubleClick() {
        if (!_fullscreenable) return;
        if (_fullscreen) Restore();
        else Fullscreen();
    }

    internal bool CanDragNow => _draggable && !_fullscreen;

    private void EnsureTitle() {
        if (_titleStrip != null || !IsAlive) return;

        try {
            CreateTitle();
        }
        catch (Exception e) {
            UiGuard.Report("window.title", e, _scope.Id);
            if (_titleStrip != null) UiGuard.Run("window.title-cleanup", () => UiFactory.Destroy(_titleStrip.gameObject), _scope.Id);
            _titleStrip = null;
            _titleText = null;
            _titleButtons = null;
            _closeButton = null;
            SetBodyTop();
        }
    }

    private void CreateTitle() {
        if (Root == null) return;

        _titleStrip = UiFactory.NewRect("DM Title Bar", Root);
        _titleStrip.anchorMin = new Vector2(0f, 1f);
        _titleStrip.anchorMax = new Vector2(1f, 1f);
        _titleStrip.pivot = new Vector2(0.5f, 1f);
        SetTitleInsets();

        var layout = _titleStrip.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        _titleText = UiFactory.AddText(_titleStrip, "DM Title", string.Empty, 20f,
                                       TextAlignmentOptions.MidlineLeft, _scope.Runtime.Theme);
        _titleText.enableWordWrapping = false;
        _titleText.overflowMode = TextOverflowModes.Ellipsis;

        var titleLayout = UiFactory.Layout(_titleText.rectTransform);
        if (titleLayout != null) {
            titleLayout.minWidth = 0f;
            titleLayout.preferredWidth = 0f;
            titleLayout.flexibleWidth = 1f;
        }

        var watcher = _titleText.gameObject.AddComponent<WidthWatcher>();
        watcher.Bind(_titleText);

        var buttons = UiFactory.NewRect("DM Title Buttons", _titleStrip);
        var buttonLayout = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 6f;
        buttonLayout.childAlignment = TextAnchor.MiddleRight;
        buttonLayout.childControlWidth = true;
        buttonLayout.childControlHeight = true;
        buttonLayout.childForceExpandWidth = false;
        buttonLayout.childForceExpandHeight = true;

        var buttonsElement = UiFactory.Layout(buttons);
        if (buttonsElement != null) buttonsElement.flexibleWidth = 0f;

        _titleButtons = new UiContainer(buttons, buttonLayout, _scope.Runtime.Theme, _scope.Runtime, textSizedButtons: true);
        _titleButtons.ChildAdded = EnsureCloseLast;
        SetBodyTop();
    }

    private void EnsureCloseLast() {
        if (_closeButton != null && _closeButton.IsAlive && _closeButton.Root != null)
            UiGuard.Run("window.close-order", () => _closeButton.Root.SetAsLastSibling(), _scope.Id);
    }

    private void SetTitleInsets() {
        if (_titleStrip == null) return;

        // The layout already reserves the close button's width inside the strip. Reserving it again here
        // pushed the button a whole button-width away from the window's right padding.
        _titleStrip.offsetMin = new Vector2(Padding, -(TitleTop + TitleHeight));
        _titleStrip.offsetMax = new Vector2(-Padding, -TitleTop);
        SetBodyTop();
    }

    private void SetBodyTop() {
        if (_body == null || !_body.IsAlive || _body.Root == null) return;
        float top = _titleStrip != null ? Padding + TitleTop + TitleHeight + TitleGap : Padding;
        UiGuard.Run("window.body-top", () => _body.Root.offsetMax = new Vector2(-Padding, -top), _scope.Id);
    }

    private void ApplyNormalLayout() {
        if (!IsAlive || _fullscreen || Root == null) return;

        Root.anchorMin = Root.anchorMax = new Vector2(0f, 1f);
        Root.pivot = new Vector2(0f, 1f);
        Root.anchoredPosition = new Vector2(_position.x, -_position.y);
        Root.sizeDelta = new Vector2(_width, _height);
    }

    private void ApplyFullscreenLayout() {
        if (!IsAlive || Root == null) return;

        Root.anchorMin = Vector2.zero;
        Root.anchorMax = Vector2.one;
        Root.pivot = new Vector2(0.5f, 0.5f);
        Root.offsetMin = Vector2.zero;
        Root.offsetMax = Vector2.zero;
    }

    private void SaveNormal() {
        if (Root == null) return;

        _normalAnchorMin = Root.anchorMin;
        _normalAnchorMax = Root.anchorMax;
        _normalPivot = Root.pivot;
        _normalPosition = Root.anchoredPosition;
        _normalSize = Root.sizeDelta;
        _hasNormal = true;
    }

    private void ApplyDraggable() {
        if (!IsAlive || Root == null) return;

        UiGuard.Run("window.drag", () => {
            if (_draggable) {
                if (_drag == null) {
                    _drag = Root.gameObject.AddComponent<WindowDragHandler>();
                    WindowDragRegistry.Bind(_drag, BringToFront, HandleDoubleClick);
                    _drag.Bind(Root, Padding);
                    _drag.SetCanDrag(!_fullscreen);
                }
                else {
                    _drag.enabled = true;
                    _drag.SetCanDrag(!_fullscreen);
                }
            }
            else if (_drag != null) {
                // Keep the component and its EventTrigger alive: removing a component does not remove
                // the trigger entries it registered. Disabling it is enough and can be reversed later.
                _drag.SetCanDrag(false);
                _drag.enabled = false;
            }
        }, _scope.Id);
    }

    private void ListenForLayerChanges() {
        if (_ordersListening || _sortingOrder == null) return;

        _ordersListening = true;
        try { _scope.Runtime.Layers.OrdersChanged += ApplySorting; }
        catch (Exception e) {
            _ordersListening = false;
            UiGuard.Report("window.sorting.listen", e, _scope.Id);
        }
    }

    private void UnlistenFromLayerChanges() {
        if (!_ordersListening) return;

        _ordersListening = false;
        try { _scope.Runtime.Layers.OrdersChanged -= ApplySorting; }
        catch (Exception e) {
            UiGuard.Report("window.sorting.unlisten", e, _scope.Id);
        }
    }

    private void ApplySorting() {
        if (!IsAlive || Root == null) return;

        try {
            var canvas = Root.gameObject.GetComponent<Canvas>();
            var raycaster = Root.gameObject.GetComponent<GraphicRaycaster>();

            if (_sortingOrder == null) {
                if (canvas != null) UiFactory.Destroy(canvas);
                if (raycaster != null) UiFactory.Destroy(raycaster);
                return;
            }

            if (canvas == null) canvas = Root.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = _scope.Runtime.Layers.OrderFor(_layer) + _sortingOrder.Value;

            if (raycaster == null) Root.gameObject.AddComponent<GraphicRaycaster>();
        }
        catch (Exception e) {
            UiGuard.Report("window.sorting", e, _scope.Id);
        }
    }
}
