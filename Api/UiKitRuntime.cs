using System;
using System.Collections.Generic;
using BepInEx.Logging;
using DiceModders.UiKit.Custom;
using DiceModders.UiKit.Native;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace DiceModders.UiKit;

/// <summary>
/// One process-wide runtime shared by every mod: asset cache, theme, layer canvases and scene hooks.
/// Scopes are cheap ownership tokens; they never create canvases or scan assets.
/// </summary>
internal sealed class UiKitRuntime {
    private readonly List<UiScope> _scopes = new();
    private UnityAction<Scene, LoadSceneMode>? _sceneLoaded;
    private UnityAction<Scene>? _sceneUnloaded;
    private DemoController? _demo;
    private int _windowIndex;
    private int _scopeIndex;
    private bool _disposed;

    internal UiKitRuntime(ManualLogSource? log, UiOptions? options) {
        Options = options ?? new UiOptions();
        UiLog.SetLogger(log);

        Assets = new AssetCatalog();
        Templates = new TemplateCatalog();
        Theme = new UiTheme(Assets);
        Layers = new LayerCanvases(this);
        NativeUi = new NativeUiHost();

        RegisterComponents();

        try {
            Action<Scene, LoadSceneMode> loaded = OnSceneLoaded;
            Action<Scene> unloaded = OnSceneUnloaded;
            _sceneLoaded = DelegateSupport.ConvertDelegate<UnityAction<Scene, LoadSceneMode>>(loaded);
            _sceneUnloaded = DelegateSupport.ConvertDelegate<UnityAction<Scene>>(unloaded);
            SceneManager.sceneLoaded += _sceneLoaded;
            SceneManager.sceneUnloaded += _sceneUnloaded;
        }
        catch (Exception e) {
            UiLog.Warn("could not subscribe to scene changes: " + e.Message);
        }

        if (Options.EnableDemo) {
            UiGuard.Run("demo.create", () => {
                _demo = new DemoController(this);
                _demo.Request();
                _demo.OnSceneLoaded();
            }, "runtime");

            if (_demo == null) UiLog.Error("the built-in demo could not be created");
        }

        UiLog.Info("UI kit runtime created");
    }

    internal AssetCatalog Assets { get; }
    internal TemplateCatalog Templates { get; }
    internal UiTheme Theme { get; }
    internal LayerCanvases Layers { get; }
    internal NativeUiHost NativeUi { get; }
    internal UiOptions Options { get; }
    internal bool IsDisposed => _disposed;

    internal IReadOnlyList<UiScope> Scopes => _scopes;
    internal bool IsDemoVisible => _demo != null && _demo.IsVisible;

    /// <summary>Shows the library's built-in test screens, creating them now if necessary.</summary>
    internal void ShowDemo() {
        if (_disposed) return;
        UiGuard.Run("demo.show", () => {
            _demo ??= new DemoController(this);
            _demo.Show();
        }, "runtime");
    }

    /// <summary>Hides the built-in test screens without disposing them.</summary>
    internal void HideDemo() {
        if (_disposed) return;
        UiGuard.Run("demo.hide", () => _demo?.Hide(), "runtime");
    }

    internal void ToggleDemo() {
        if (_disposed) return;
        UiGuard.Run("demo.toggle", () => {
            _demo ??= new DemoController(this);
            _demo.Toggle();
        }, "runtime");
    }

    /// <summary>Recreates the demo windows and re-scans native templates for their widgets.</summary>
    internal void RebuildDemo() {
        if (_disposed) return;
        UiGuard.Run("demo.rebuild", () => {
            _demo ??= new DemoController(this);
            _demo.Rebuild();
        }, "runtime");
    }

    internal UiScope? CreateScope(string? id) {
        if (_disposed) {
            UiLog.OnceError("runtime:disposed", "cannot create a UiKit scope after the runtime has been disposed");
            return null;
        }

        string scopeId = NormalizeScopeId(id);
        try {
            var scope = new UiScope(this, scopeId);
            _scopes.Add(scope);
            return scope;
        }
        catch (Exception e) {
            UiLog.Error($"could not create UiKit scope '{scopeId}': {e}");
            return null;
        }
    }

    private string NormalizeScopeId(string? id) {
        if (!string.IsNullOrWhiteSpace(id)) return id.Trim();
        return "scope-" + (++_scopeIndex);
    }

    internal void RemoveScope(UiScope scope) {
        if (scope != null) _scopes.Remove(scope);
    }

    internal int NextWindowIndex() => _windowIndex++;

    private static void RegisterComponents() {
        Register<ClickRelay>();
        Register<SliderRelay>();
        Register<WindowDragHandler>();
        Register<PointerReceiver>();
        Register<AutoHeightText>();
        Register<WidthWatcher>();
        Register<ScrollPin>();
    }

    private static void Register<T>() where T : class {
        try { ClassInjector.RegisterTypeInIl2Cpp<T>(); }
        catch (Exception e) { UiLog.Warn($"could not register {typeof(T).Name}: {e.Message}"); }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        if (_disposed) return;

        UiGuard.Run("scene.assets", () => {
            Assets.Invalidate();
            Templates.Invalidate();
        }, "runtime");
        UiGuard.Run("scene.delegates", Il2CppDelegates.Sweep, "runtime");
        UiGuard.Run("scene.layers", Layers.Refresh, "runtime");
        UiGuard.Run("scene.native", NativeUi.Refresh, "runtime");
        UiGuard.Run("scene.demo", () => _demo?.OnSceneLoaded(), "runtime");
    }

    private void OnSceneUnloaded(Scene scene) {
        if (_disposed) return;

        UiGuard.Run("scene.assets", () => {
            Assets.Invalidate();
            Templates.Invalidate();
        }, "runtime");
        UiGuard.Run("scene.delegates", Il2CppDelegates.Sweep, "runtime");
        UiGuard.Run("scene.native", NativeUi.Detach, "runtime");
    }

    internal void Dispose() {
        if (_disposed) return;
        _disposed = true;

        UiGuard.Run("runtime.unsubscribe", () => {
            if (_sceneLoaded != null) SceneManager.sceneLoaded -= _sceneLoaded;
            if (_sceneUnloaded != null) SceneManager.sceneUnloaded -= _sceneUnloaded;
            _sceneLoaded = null;
            _sceneUnloaded = null;
        }, "runtime");

        var demo = _demo;
        _demo = null;
        if (demo != null) UiGuard.Run("demo.dispose", demo.Dispose, "runtime");

        UiGuard.Run("native.dispose", NativeUi.Dispose, "runtime");

        var scopes = _scopes.ToArray();
        _scopes.Clear();
        for (int i = scopes.Length - 1; i >= 0; i--) {
            UiGuard.Run("scope.dispose", scopes[i].Dispose, scopes[i].Id);
        }

        UiGuard.Run("runtime.templates", Templates.Invalidate, "runtime");
        UiGuard.Run("runtime.assets", Assets.Invalidate, "runtime");
        UiGuard.Run("runtime.delegates", Il2CppDelegates.Clear, "runtime");
        UiGuard.Run("runtime.staging", Staging.Reset, "runtime");
        UiGuard.Run("runtime.layers", Layers.Dispose, "runtime");

        UiLog.Info("UI kit runtime disposed");
    }
}
