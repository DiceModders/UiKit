using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UIImage = UnityEngine.UI.Image;

namespace DiceModders.UiKit.Custom;

/// <summary>
/// A draggable window with a body, an optional title strip and an optional frame. It is both the builder and the handle.
/// </summary>
public sealed class Window : Element {
    public const float DefaultWidth = 400f;
    public const float DefaultHeight = 260f;
    public const float Padding = 16f;
    public const float MinWidth = 160f;
    public const float MinHeight = 100f;
    private const float TitleTop = 10f;
    private const float TitleHeight = 32f;
    private const float TitleGap = 6f;

    private readonly UiScope _scope;
    private readonly Container _body;
    private readonly UIImage _background;
    private readonly UIImage _ornament;

    private Container _titleButtons;
    private RectTransform _titleStrip;
    private TextMeshProUGUI _titleText;
    private Button _closeButton;
    private WindowDragHandler _drag;
    private Action _onClose;

    private Layer _layer;
    private OrnamentStyle _ornamentStyle = OrnamentStyle.Border;
    private float _width = DefaultWidth;
    private float _height = DefaultHeight;
    private Vector2 _position;
    private bool _positioned;
    private bool _draggable;
    private bool _fullscreenable;
    private bool _fullscreen;
    private bool _closed;
    private int? _sortingOrder;

    private Vector2 _normalAnchorMin;
    private Vector2 _normalAnchorMax;
    private Vector2 _normalPivot;
    private Vector2 _normalPosition;
    private Vector2 _normalSize;
    private bool _hasNormal;

    internal Window(UiScope scope) : base(CreateRoot(scope)) {
        _scope = scope;
        _layer = scope.Runtime.Options.DefaultLayer;

        var theme = scope.Runtime.Theme;
        _background = Root.gameObject.AddComponent<UIImage>();
        _background.sprite = theme.Panel;
        _background.type = _background.sprite != null ? UIImage.Type.Sliced : UIImage.Type.Simple;
        _background.color = _background.sprite != null ? Color.white : new Color(0.12f, 0.10f, 0.08f, 0.98f);
        _background.raycastTarget = true;

        _body = BuildBody(theme);
        _ornament = BuildOrnament(theme);

        int index = scope.Runtime.NextWindowIndex();
        _position = new Vector2(60f + index * 28f, 60f + index * 28f);
        _positioned = true;
        ApplyNormalLayout();
    }

    private static RectTransform CreateRoot(UiScope scope) {
        var layer = scope.Runtime.Options.DefaultLayer;
        var root = UiFactory.NewRect("DM Window", scope.Runtime.Layers.Get(layer).transform);
        root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        return root;
    }

    private Container BuildBody(UiTheme theme) {
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

        return new Container(rect, layout, theme, _scope.Runtime);
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

    public Container Body => _body;
    public Layer CurrentLayer => _layer;
    public bool IsVisible => IsAlive && Root.gameObject.activeSelf;
    public bool IsFullscreen => _fullscreen;
    public bool CanFullscreen => _fullscreenable;

    public Container TitleBar {
        get {
            EnsureTitle();
            return _titleButtons;
        }
    }

    public Window Title(string text) {
        EnsureTitle();
        if (_titleText != null) _titleText.text = text ?? string.Empty;
        return this;
    }

    public Window Size(float width, float height) {
        _width = Mathf.Max(MinWidth, width);
        _height = Mathf.Max(MinHeight, height);
        if (!_fullscreen) ApplyNormalLayout();
        return this;
    }

    public Window Position(float x, float y) {
        _position = new Vector2(x, y);
        _positioned = true;
        if (!_fullscreen) ApplyNormalLayout();
        return this;
    }

    public Window Center() {
        var parent = Root != null && Root.parent != null ? Root.parent.TryCast<RectTransform>() : null;
        if (parent == null) return this;

        _position = new Vector2(
            Mathf.Max(0f, (parent.rect.width - _width) * 0.5f),
            Mathf.Max(0f, (parent.rect.height - _height) * 0.5f));
        _positioned = true;
        if (!_fullscreen) ApplyNormalLayout();
        return this;
    }

    public Window Layer(Layer layer) {
        if (_layer == layer) return this;

        var position = Root.anchoredPosition;
        var size = Root.sizeDelta;
        var anchorMin = Root.anchorMin;
        var anchorMax = Root.anchorMax;
        var pivot = Root.pivot;

        Root.SetParent(_scope.Runtime.Layers.Get(layer).transform, false);
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
        return this;
    }

    public Window SortingOrder(int order) {
        _sortingOrder = order;
        ApplySorting();
        return this;
    }

    public Window Draggable(bool on = true) {
        _draggable = on;
        ApplyDraggable();
        return this;
    }

    public Window Fullscreenable(bool on = true) {
        _fullscreenable = on;
        return this;
    }

    public Window Closable(bool on = true) {
        EnsureTitle();
        if (_titleButtons == null) return this;

        if (!on) {
            _closeButton?.Dispose();
            _closeButton = null;
            SetTitleInsets(false);
            return this;
        }

        if (_closeButton != null) return this;

        _closeButton = _scope.Runtime.Theme.Icon("Misc-Cross") != null
            ? _titleButtons.IconButton("Misc-Cross", Close, "Close")
            : _titleButtons.Button("×", Close)?.Width(32f);

        if (_closeButton == null) {
            SetTitleInsets(false);
            return this;
        }

        EnsureCloseLast();
        SetTitleInsets(true);
        return this;
    }

    public Window Ornament(OrnamentStyle style) {
        _ornamentStyle = style;
        if (_ornament == null) return this;

        var sprite = _scope.Runtime.Theme.Ornament(style);
        _ornament.sprite = sprite;
        _ornament.enabled = sprite != null;
        return this;
    }

    public Window OnClose(Action action) {
        _onClose = action;
        return this;
    }

    public void Show() {
        if (IsAlive) Root.gameObject.SetActive(true);
    }

    public void Hide() {
        if (IsAlive) Root.gameObject.SetActive(false);
    }

    public void Toggle() {
        if (!IsAlive) return;
        Root.gameObject.SetActive(!Root.gameObject.activeSelf);
    }

    public void BringToFront() {
        if (!IsAlive) return;
        Root.SetAsLastSibling();
    }

    public void Fullscreen() {
        if (!_fullscreenable || _fullscreen || !IsAlive) return;
        SaveNormal();
        _fullscreen = true;
        ApplyFullscreenLayout();
        _drag?.SetCanDrag(false);
        BringToFront();
    }

    public void Restore() {
        if (!_fullscreen || !IsAlive) return;
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
    }

    public void Close() {
        if (_closed) return;
        _closed = true;

        try { _onClose?.Invoke(); }
        catch (Exception e) { UiLog.Error("window close handler: " + e); }

        Dispose();
    }

    public override void Dispose() {
        if (_closed && !IsAlive) return;
        _closed = true;

        if (_drag != null) {
            UiFactory.Destroy(_drag);
            _drag = null;
        }

        _closeButton = null;
        _titleButtons = null;
        _titleStrip = null;
        _titleText = null;
        _onClose = null;
        _drag = null;

        _scope.RemoveOwned(this);
        base.Dispose();
    }

    internal void FinalizeBuild() {
        if (!_positioned) {
            int index = _scope.Runtime.NextWindowIndex();
            _position = new Vector2(60f + index * 28f, 60f + index * 28f);
        }
        ApplyNormalLayout();
        ApplyDraggable();
        ApplySorting();
    }

    internal void HandleDoubleClick() {
        if (!_fullscreenable) return;
        if (_fullscreen) Restore();
        else Fullscreen();
    }

    internal bool CanDragNow => _draggable && !_fullscreen;

    private void EnsureTitle() {
        if (_titleStrip != null || !IsAlive) return;

        _titleStrip = UiFactory.NewRect("DM Title Bar", Root);
        _titleStrip.anchorMin = new Vector2(0f, 1f);
        _titleStrip.anchorMax = new Vector2(1f, 1f);
        _titleStrip.pivot = new Vector2(0.5f, 1f);
        SetTitleInsets(false);

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
        titleLayout.minWidth = 0f;
        titleLayout.preferredWidth = 0f;
        titleLayout.flexibleWidth = 1f;

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
        buttonsElement.flexibleWidth = 0f;

        _titleButtons = new Container(buttons, buttonLayout, _scope.Runtime.Theme, _scope.Runtime, textSizedButtons: true);
        _titleButtons.ChildAdded = EnsureCloseLast;
        SetBodyTop();
    }

    private void EnsureCloseLast() {
        if (_closeButton != null && _closeButton.IsAlive)
            _closeButton.Root.SetAsLastSibling();
    }

    private void SetTitleInsets(bool closable) {
        if (_titleStrip == null) return;

        // The layout already reserves the close button's width inside the strip. Reserving it again here
        // pushed the button a whole button-width away from the window's right padding.
        _titleStrip.offsetMin = new Vector2(Padding, -(TitleTop + TitleHeight));
        _titleStrip.offsetMax = new Vector2(-Padding, -TitleTop);
        SetBodyTop();
    }

    private void SetBodyTop() {
        if (_body == null) return;
        float top = _titleStrip != null ? Padding + TitleTop + TitleHeight + TitleGap : Padding;
        _body.Root.offsetMax = new Vector2(-Padding, -top);
    }

    private void ApplyNormalLayout() {
        if (!IsAlive || _fullscreen) return;

        Root.anchorMin = Root.anchorMax = new Vector2(0f, 1f);
        Root.pivot = new Vector2(0f, 1f);
        Root.anchoredPosition = new Vector2(_position.x, -_position.y);
        Root.sizeDelta = new Vector2(_width, _height);
    }

    private void ApplyFullscreenLayout() {
        if (!IsAlive) return;

        Root.anchorMin = Vector2.zero;
        Root.anchorMax = Vector2.one;
        Root.pivot = new Vector2(0.5f, 0.5f);
        Root.offsetMin = Vector2.zero;
        Root.offsetMax = Vector2.zero;
    }

    private void SaveNormal() {
        _normalAnchorMin = Root.anchorMin;
        _normalAnchorMax = Root.anchorMax;
        _normalPivot = Root.pivot;
        _normalPosition = Root.anchoredPosition;
        _normalSize = Root.sizeDelta;
        _hasNormal = true;
    }

    private void ApplyDraggable() {
        if (!IsAlive) return;

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
    }

    private void ApplySorting() {
        if (!IsAlive) return;

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
}
