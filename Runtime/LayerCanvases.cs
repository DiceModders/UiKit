using System;
using DiceModders.UiKit.Native;
using UnityEngine;
using UnityEngine.UI;

namespace DiceModders.UiKit;

/// <summary>
/// Three shared canvases for the whole process: behind the game, at the game's order, and above it.
/// Windows never create canvases; they only move between these hosts.
/// </summary>
internal sealed class LayerCanvases : IDisposable {
    private readonly UiKitRuntime _runtime;

    private Canvas _behind;
    private Canvas _game;
    private Canvas _above;
    private int _gameOrder;
    private int _behindOrder;
    private int _aboveOrder;
    private bool _initialized;

    public LayerCanvases(UiKitRuntime runtime) => _runtime = runtime;

    public Canvas Get(Layer layer) {
        Ensure();
        return layer switch {
            Layer.BehindGame => _behind,
            Layer.Game => _game,
            _ => _above,
        };
    }

    public int OrderFor(Layer layer) {
        Ensure();
        return layer switch {
            Layer.BehindGame => _behindOrder,
            Layer.Game => _gameOrder,
            _ => _aboveOrder,
        };
    }

    /// <summary>Re-reads the game's sorting order after a scene change and updates the shared canvases.</summary>
    public void Refresh() {
        if (!_initialized) return;

        var hud = FindHudCanvas();
        ComputeOrders(hud);

        ApplyOrder(_behind, _behindOrder);
        ApplyOrder(_game, _gameOrder);
        ApplyOrder(_above, _aboveOrder);

        ApplyRenderMode(_behind, Layer.BehindGame, hud);
        ApplyRenderMode(_game, Layer.Game, hud);
        ApplyRenderMode(_above, Layer.AboveGame, hud);

        if (hud != null) {
            ApplySortingLayer(_behind, hud);
            ApplySortingLayer(_game, hud);
            ApplySortingLayer(_above, hud);

            CopyScaler(hud, _behind != null ? _behind.GetComponent<CanvasScaler>() : null);
            CopyScaler(hud, _game != null ? _game.GetComponent<CanvasScaler>() : null);
            CopyScaler(hud, _above != null ? _above.GetComponent<CanvasScaler>() : null);
        }
    }

    public void Invalidate() => Refresh();

    private void Ensure() {
        if (_initialized) return;
        _initialized = true;

        var hud = FindHudCanvas();
        ComputeOrders(hud);

        _behind = Create("DiceModders UiKit [Behind]", Layer.BehindGame, _behindOrder, hud);
        _game = Create("DiceModders UiKit [Game]", Layer.Game, _gameOrder, hud);
        _above = Create("DiceModders UiKit [Above]", Layer.AboveGame, _aboveOrder, hud);

        string mode = hud != null ? hud.renderMode.ToString() : "no HUD";
        string hudName = hud != null ? $"'{hud.name}'" : "none";
        UiLog.Info($"UI layers ready (HUD {hudName}, {mode}, order {_gameOrder - 1}; game {_gameOrder}, behind {_behindOrder}, above {_aboveOrder})");
    }

    private static Canvas Create(string name, Layer layer, int order, Canvas hud) {
        var rect = UiFactory.NewRect(name, null);
        var go = rect.gameObject;
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.layer = hud != null ? hud.gameObject.layer : 5;
        rect.sizeDelta = new Vector2(1920f, 1080f);

        var canvas = go.AddComponent<Canvas>();
        ApplyRenderMode(canvas, layer, hud);
        canvas.sortingOrder = order;
        if (hud != null) canvas.sortingLayerID = hud.sortingLayerID;

        var scaler = go.AddComponent<CanvasScaler>();
        CopyScaler(hud, scaler);

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    /// <summary>
    /// Matches the HUD's render mode. An overlay canvas always draws over a Screen Space - Camera HUD, even with a negative
    /// sorting order, so the BehindGame layer must use the same camera space as the HUD to actually sit behind it.
    /// </summary>
    private static void ApplyRenderMode(Canvas canvas, Layer layer, Canvas hud) {
        if (canvas == null) return;

        if (hud == null || hud.renderMode == RenderMode.ScreenSpaceOverlay || hud.worldCamera == null) {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            return;
        }

        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = hud.worldCamera;

        float baseDistance = hud.planeDistance > 0.01f ? hud.planeDistance : 100f;
        float near = hud.worldCamera != null ? hud.worldCamera.nearClipPlane + 0.01f : 0.01f;
        canvas.planeDistance = layer switch {
            Layer.BehindGame => baseDistance + 10f,
            Layer.Game => baseDistance,
            _ => Mathf.Max(near, baseDistance - 10f),
        };
    }

    private static void ApplyOrder(Canvas canvas, int order) {
        if (canvas != null) canvas.sortingOrder = order;
    }

    private static void ApplySortingLayer(Canvas canvas, Canvas hud) {
        if (canvas != null && hud != null) canvas.sortingLayerID = hud.sortingLayerID;
    }

    private static void CopyScaler(Canvas source, CanvasScaler target) {
        if (target == null) return;

        var from = source != null ? source.GetComponent<CanvasScaler>() : null;
        if (from == null) {
            target.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            target.referenceResolution = new Vector2(1920f, 1080f);
            target.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            target.matchWidthOrHeight = 0.5f;
            target.referencePixelsPerUnit = 100f;
            return;
        }

        target.uiScaleMode = from.uiScaleMode;
        target.referenceResolution = from.referenceResolution;
        target.screenMatchMode = from.screenMatchMode;
        target.matchWidthOrHeight = from.matchWidthOrHeight;
        target.referencePixelsPerUnit = from.referencePixelsPerUnit;
        target.scaleFactor = from.scaleFactor;
        target.physicalUnit = from.physicalUnit;
        target.fallbackScreenDPI = from.fallbackScreenDPI;
        target.defaultSpriteDPI = from.defaultSpriteDPI;
    }

    private void ComputeOrders(Canvas hud) {
        int hudOrder = hud != null ? hud.sortingOrder : 0;

        // Same-order overlay canvases sort by creation order, so the game layer gets the next free
        // band above the HUD. It still sits below the game's own popups (which use higher orders).
        _gameOrder = hudOrder + 1;

        int min = hudOrder;
        int max = hudOrder;

        foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>()) {
            if (canvas == null || !canvas.isRootCanvas || !canvas.gameObject.activeInHierarchy) continue;
            if (canvas.renderMode == RenderMode.WorldSpace) continue;
            if (canvas.gameObject.name.StartsWith("DiceModders UiKit")) continue;
            if (canvas.sortingOrder > 1000) continue;   // debug overlays such as the game's performance graph

            min = Mathf.Min(min, canvas.sortingOrder);
            max = Mathf.Max(max, canvas.sortingOrder);
        }

        _behindOrder = min - 100;
        _aboveOrder = max + 100;
    }

    private static Canvas FindHudCanvas() {
        try {
            Canvas best = null;
            int bestScore = int.MinValue;

            foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>()) {
                if (canvas == null || !canvas.isRootCanvas || !canvas.gameObject.activeInHierarchy) continue;
                if (canvas.renderMode == RenderMode.WorldSpace) continue;
                if (canvas.gameObject.name.StartsWith("DiceModders UiKit")) continue;

                int score = 0;
                if (canvas.sortingOrder <= 100) score += 10;
                if (canvas.sortingOrder == 0) score += 2;

                if (GameUi.FindBar(canvas.transform) != null)
                    score += 1000;

                if (score > bestScore) {
                    bestScore = score;
                    best = canvas;
                }
            }

            return best;
        }
        catch {
            return null;
        }
    }

    public void Dispose() {
        Destroy(_behind);
        Destroy(_game);
        Destroy(_above);
        _behind = null;
        _game = null;
        _above = null;
        _initialized = false;
    }

    private static void Destroy(Canvas canvas) {
        if (canvas == null) return;
        UnityEngine.Object.Destroy(canvas.gameObject);
    }
}
