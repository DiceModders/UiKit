using System.Collections.Generic;
using BepInEx.Logging;

namespace DiceModders.UiKit;

internal static class UiLog {
    private static ManualLogSource _log;
    private static readonly HashSet<string> OnceKeys = new();

    public static void SetLogger(ManualLogSource log) => _log = log;

    public static void Info(string message) => _log?.LogInfo(message);
    public static void Warn(string message) => _log?.LogWarning(message);
    public static void Error(string message) => _log?.LogError(message);

    public static void Once(string key, string message) {
        lock (OnceKeys) {
            if (!OnceKeys.Add(key)) return;
        }
        Warn(message);
    }

    public static void InfoOnce(string key, string message) {
        lock (OnceKeys) {
            if (!OnceKeys.Add(key)) return;
        }
        Info(message);
    }

    public static void OnceError(string key, string message) {
        lock (OnceKeys) {
            if (!OnceKeys.Add(key)) return;
        }
        Error(message);
    }
}
