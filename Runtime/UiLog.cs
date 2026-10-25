using System;
using System.Collections.Generic;
using BepInEx.Logging;

namespace DiceModders.UiKit;

internal static class UiLog {
    private static ManualLogSource? _log;
    private static readonly HashSet<string> OnceKeys = new();

    public static void SetLogger(ManualLogSource? log) => _log = log;

    public static void ResetOnce() {
        lock (OnceKeys) OnceKeys.Clear();
    }

    public static void Info(string? message) => Write(log => log.LogInfo(message));
    public static void Warn(string? message) => Write(log => log.LogWarning(message));
    public static void Error(string? message) => Write(log => log.LogError(message));

    public static void Once(string key, string? message) {
        lock (OnceKeys) {
            if (!OnceKeys.Add(key)) return;
        }
        Warn(message);
    }

    public static void InfoOnce(string key, string? message) {
        lock (OnceKeys) {
            if (!OnceKeys.Add(key)) return;
        }
        Info(message);
    }

    public static void OnceError(string key, string? message) {
        lock (OnceKeys) {
            if (!OnceKeys.Add(key)) return;
        }
        Error(message);
    }

    private static void Write(Action<ManualLogSource> write) {
        var log = _log;
        if (log == null) return;

        try { write(log); }
        catch {
            // Logging is best-effort: a logger failure must not become a UI failure.
        }
    }
}
