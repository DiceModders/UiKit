using System;
using UnityEngine;

namespace DiceModders.UiKit.Native;

/// <summary>
/// Reports a scoreboard row's destruction to the native host. Unity has no event for this, and the game's row type
/// does not expose OnDestroy, so the watcher lives on the row object and is cleaned up with it.
/// </summary>
internal sealed class ScoreboardRowLifetime : MonoBehaviour {
    public ScoreboardRowLifetime(IntPtr ptr) : base(ptr) { }

    private NativeUiHost? _host;
    private DiceKingdoms.Interface.ScoreboardRow? _row;

    public void Bind(NativeUiHost host, DiceKingdoms.Interface.ScoreboardRow row) {
        _host = host;
        _row = row;
    }

    private void OnDestroy() {
        var host = _host;
        var row = _row;
        _host = null;
        _row = null;
        try { host?.OnScoreboardRowDestroyed(row); }
        catch (Exception e) { UiLog.Once("scoreboard:lifetime", "scoreboard row lifetime cleanup failed: " + e.Message); }
    }
}
