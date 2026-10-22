using System;
using System.Collections.Generic;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
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
public sealed class UiKitRuntime {
    private readonly List<UiScope> _scopes = new();
    private readonly UnityAction<Scene, LoadSceneMode> _sceneLoaded;
    private readonly UnityAction<Scene> _sceneUnloaded;
    private DemoController _demo;
    private int _windowIndex;
    private bool _disposed;

    internal UiKitRuntime(ManualLogSource log, UiOptions options) {
        Options = options ?? new UiOptions();
        UiLog.SetLogger(log);

        Assets = new AssetCatalog();
        Templates = new TemplateCatalog();
        Theme = new UiTheme(Assets);
        Layers = new LayerCanvases(this);
        NativeUi = new NativeUiHost(this);

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
            _demo = new DemoController(this);
            _demo.Request();
            _demo.OnSceneLoaded();
        }

        UiLog.Info("UI kit runtime created");
    }

    internal AssetCatalog Assets { get; }
    internal TemplateCatalog Templates { get; }
    internal UiTheme Theme { get; }
    internal LayerCanvases Layers { get; }
    internal NativeUiHost NativeUi { get; }
    internal UiOptions Options { get; }

    public IReadOnlyList<UiScope> Scopes => _scopes;
    public bool IsDemoVisible => _demo != null && _demo.IsVisible;

    /// <summary>Shows the library's built-in test screens, creating them now if necessary.</summary>
    public void ShowDemo() {
        if (_disposed) return;
        _demo ??= new DemoController(this);
        _demo.Show();
    }

    /// <summary>Hides the built-in test screens without disposing them.</summary>
    public void HideDemo() => _demo?.Hide();

    public void ToggleDemo() {
        if (_disposed) return;
        _demo ??= new DemoController(this);
        _demo.Toggle();
    }

    /// <summary>Recreates the demo windows and re-scans native templates for their widgets.</summary>
    public void RebuildDemo() {
        if (_disposed) return;
        _demo ??= new DemoController(this);
        _demo.Rebuild();
    }

    public UiScope CreateScope(string id) {
        if (_disposed) throw new ObjectDisposedException(nameof(UiKitRuntime));
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A scope id is required.", nameof(id));

        var scope = new UiScope(this, id);
        _scopes.Add(scope);
        return scope;
    }

    internal void RemoveScope(UiScope scope) {
        _scopes.Remove(scope);
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
        Assets.Invalidate();
        Templates.Invalidate();
        Il2CppDelegates.Sweep();
        Layers.Refresh();
        NativeUi.Refresh();
        _demo?.OnSceneLoaded();
    }

    private void OnSceneUnloaded(Scene scene) {
        Assets.Invalidate();
        Templates.Invalidate();
        Il2CppDelegates.Sweep();
        NativeUi.Detach();
    }

    internal void Dispose() {
        if (_disposed) return;
        _disposed = true;

        if (_sceneLoaded != null) SceneManager.sceneLoaded -= _sceneLoaded;
        if (_sceneUnloaded != null) SceneManager.sceneUnloaded -= _sceneUnloaded;

        _demo?.Dispose();
        _demo = null;

        NativeUi.Dispose();

        for (int i = _scopes.Count - 1; i >= 0; i--) {
            try { _scopes[i].Dispose(); }
            catch (Exception e) { UiLog.Error("scope dispose failed: " + e); }
        }
        _scopes.Clear();

        Templates.Invalidate();
        Il2CppDelegates.Clear();
        Staging.Reset();
        Layers.Dispose();
        UiLog.Info("UI kit runtime disposed");
    }
}
