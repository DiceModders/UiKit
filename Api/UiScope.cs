using System;
using System.Collections.Generic;
using DiceModders.UiKit.Custom;

namespace DiceModders.UiKit;

/// <summary>
/// A mod's ownership token. Everything created through <see cref="Custom"/> and <see cref="Native"/> is removed by
/// <see cref="Dispose"/>. Disposing a scope never throws.
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

    public void Dispose() {
        if (IsDisposed) return;
        IsDisposed = true;

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
