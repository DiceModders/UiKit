using System.Collections.Generic;
using UnityEngine;
using GameRules = DiceKingdoms.Meta.Rules;
using GameScoreboardRow = DiceKingdoms.Interface.ScoreboardRow;
using GameSettings = DiceKingdoms.Meta.SettingSetsInterface;

namespace DiceModders.UiKit.Native;

/// <summary>A snapshot of every game-owned target the native layer can attach to right now.</summary>
internal sealed class NativeScene {
    public Transform? TopBar;
    public GameSettings? Settings;
    public IReadOnlyList<GameScoreboardRow> ScoreboardRows = System.Array.Empty<GameScoreboardRow>();
    public GameRules? RulesScreen;
}
