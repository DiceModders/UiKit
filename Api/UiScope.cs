using System;
using System.Collections.Generic;
using DiceModders.UiKit.Custom;

namespace DiceModders.UiKit;

/// <summary>
/// A mod's ownership token. Everything created through <see cref="Custom"/> and <see cref="Native"/> is removed by
/// <see cref="Dispose"/>, and a queued <see cref="WhenReady"/> callback is cancelled. Disposing a scope never throws.
/// </summary>
public sealed class UiScope : IDisposable {
    private readonly List<IDisposable> _owned = new();

    internal UiScope(UiKitRuntime runtime, string id) {
        Runtime = runtime;
        Id = id;
        Custom = new CustomApi(this);
        Native = new NativeApi(this);
    }

    internal UiKitRuntime Runtime { get; }
    public string Id { get; }
    public CustomApi Custom { get; }
    public NativeApi Native { get; }
    public bool IsDisposed { get; private set; }

    internal bool IsAlive => !IsDisposed && !Runtime.IsDisposed;
    internal List<IDisposable> Owned => _owned;

    internal void RemoveOwned(IDisposable item) {
        if (item != null) _owned.Remove(item);
    }

    /// <summary>
    /// Registers a one-shot content callback for the first ready game UI. This is the library's guaranteed startup
    /// registration point: the callback runs on the main thread after the shared layers and the game's top bar exist.
    /// Each callback runs at most once and immediately once the runtime has marked the UI ready. A disposed scope drops
    /// the callback. Must be called from the main thread. Match-only screens and templates may still appear later;
    /// native handles attach to them on their own.
    /// </summary>
    /// <param name="build">Content registration callback. A null callback is ignored.</param>
    /// <returns>This scope, for chaining.</returns>
    public UiScope WhenReady(Action<UiScope>? build) {
        if (build == null) return this;
        if (IsDisposed) {
            UiLog.OnceError("scope:ready-disposed:" + Id, $"cannot register UI on disposed scope '{Id}'");
            return this;
        }
        if (Runtime.IsDisposed) {
            UiLog.OnceError("scope:ready-runtime:" + Id,
                $"cannot register UI on scope '{Id}': the UiKit runtime is shutting down");
            return this;
        }

        try { Runtime.RegisterWhenReady(this, build); }
        catch (Exception e) { UiLog.Error($"scope '{Id}' ready registration failed: {e}"); }
        return this;
    }

    public void Dispose() {
        if (IsDisposed) return;
        IsDisposed = true;
        try { Runtime.RemoveReady(this); }
        catch (Exception e) { UiLog.Error($"scope '{Id}' ready unregister failed: {e}"); }

        var owned = _owned.ToArray();
        _owned.Clear();

        for (int i = owned.Length - 1; i >= 0; i--) {
            try { owned[i].Dispose(); }
            catch (Exception e) { UiLog.Error($"scope '{Id}' dispose failed: {e}"); }
        }

        try { Runtime.RemoveScope(this); }
        catch (Exception e) { UiLog.Error($"scope '{Id}' unregister failed: {e}"); }
    }
}
