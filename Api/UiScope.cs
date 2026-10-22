using System;
using System.Collections.Generic;
using DiceModders.UiKit.Custom;

namespace DiceModders.UiKit;

/// <summary>
/// A mod's ownership token. Everything created through <see cref="Custom"/> and <see cref="Native"/> is removed by <see cref="Dispose"/>.
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

    internal List<IDisposable> Owned => _owned;

    internal void RemoveOwned(IDisposable item) => _owned.Remove(item);

    public void Dispose() {
        if (IsDisposed) return;
        IsDisposed = true;

        for (int i = _owned.Count - 1; i >= 0; i--) {
            try { _owned[i].Dispose(); }
            catch (Exception e) { UiLog.Error($"scope '{Id}' dispose failed: {e}"); }
        }
        _owned.Clear();

        Runtime.RemoveScope(this);
    }
}
