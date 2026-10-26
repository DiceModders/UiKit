using System;

namespace DiceModders.UiKit.Native;

internal enum NativeDetachReason {
    Manual,
    SceneChanged,
    TargetLost,
    Unsupported,
}

/// <summary>
/// Base class for every object inserted into the game's own UI. The public handle is stable for its whole lifetime;
/// only the game objects it represents are attached and detached as scenes and screens change. Instances are created
/// by the module APIs, never by mods.
/// </summary>
public abstract class NativeHandle : IDisposable {
    private readonly NativeUiHost _host;
    private bool _everAttached;

    internal NativeHandle(UiScope scope, NativeUiHost host, string id) {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        Id = string.IsNullOrWhiteSpace(id) ? "native" : id.Trim();
    }

    /// <summary>Stable diagnostic id. It is unique inside the owning scope, not necessarily across scopes.</summary>
    public string Id { get; }

    /// <summary>Current lifecycle state of this registration.</summary>
    public NativeState State { get; private set; } = NativeState.Pending;

    /// <summary>Human-readable detail for the current state, or <c>null</c> when there is nothing to report.</summary>
    public string? Status { get; private set; }

    /// <summary>True while this registration has live objects in the current game UI.</summary>
    public bool IsAttached => State == NativeState.Attached;

    /// <summary>True after <see cref="Dispose"/>; a disposed registration never attaches again.</summary>
    public bool IsDisposed => State == NativeState.Disposed;

    internal UiScope Scope { get; }
    internal string OwnerId => Scope.Id;

    /// <summary>Called by the host on scene changes and on events from the relevant game screen.</summary>
    internal abstract void Refresh(NativeScene scene);

    /// <summary>Drops live game objects while keeping the registration and its cached data.</summary>
    internal void Detach(NativeDetachReason reason) {
        if (State is NativeState.Disposed or NativeState.Unsupported) return;

        try { DetachCore(); }
        catch (Exception e) { UiGuard.Report("native.detach", e, OwnerId); }

        MarkWaiting(ReasonText(reason));
    }

    /// <summary>Marks a registration as impossible under the current host policy.</summary>
    internal void MarkUnsupported(string? status) => SetState(NativeState.Unsupported, status);

    protected void SetState(NativeState state, string? status = null) {
        State = state;
        Status = status;
    }

    protected void MarkAttached(string? status = null) {
        _everAttached = true;
        SetState(NativeState.Attached, status);
    }

    /// <summary>Moves to Detached if this registration was attached before, otherwise keeps it Pending.</summary>
    protected void MarkWaiting(string? status = null) {
        if (State is NativeState.Disposed or NativeState.Unsupported) return;
        if (_everAttached) SetState(NativeState.Detached, status);
        else if (State != NativeState.Failed) MarkPending(status);
    }

    protected void MarkFailed(string? status) => SetState(NativeState.Failed, status);

    protected void MarkPending(string? status = null) => SetState(NativeState.Pending, status);

    /// <summary>Removes live objects. Implementations must be idempotent and must not throw.</summary>
    protected virtual void DetachCore() { }

    /// <summary>Module-specific cleanup before the base handle is unregistered.</summary>
    protected virtual void DisposeCore() { }

    public void Dispose() {
        if (State == NativeState.Disposed) return;

        try { DisposeCore(); }
        catch (Exception e) { UiGuard.Report("native.dispose-core", e, OwnerId); }
        try { DetachCore(); }
        catch (Exception e) { UiGuard.Report("native.dispose-detach", e, OwnerId); }

        SetState(NativeState.Disposed);
        try { _host.Unregister(this); }
        catch (Exception e) { UiLog.OnceError("native:unregister", "native registration cleanup failed: " + e); }
        try { Scope.RemoveOwned(this); }
        catch (Exception e) { UiLog.OnceError("native:untrack", "native registration untracking failed: " + e); }
    }

    private static string? ReasonText(NativeDetachReason reason) => reason switch {
        NativeDetachReason.SceneChanged => "waiting for the next scene",
        NativeDetachReason.TargetLost => "target is no longer available",
        NativeDetachReason.Unsupported => "not supported by the game",
        _ => "detached",
    };
}
