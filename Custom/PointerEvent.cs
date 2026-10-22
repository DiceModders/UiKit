using UnityEngine;

namespace DiceModders.UiKit.Custom;

public enum PointerKind { Down, Up, Click, Move, Drag, Enter, Exit }

/// <summary>A pointer event on a custom image. Positions are inside the element: (0,0) is its bottom-left corner.</summary>
public readonly struct PointerEvent {
    public PointerKind Kind { get; }
    /// <summary>0 = left, 1 = right, 2 = middle; -1 for move/enter/exit.</summary>
    public int Button { get; }
    /// <summary>Position inside the element, 0..1 on both axes (may leave that range during a drag that started on the element).</summary>
    public Vector2 Uv { get; }
    /// <summary>Position inside the element in canvas units, from the bottom-left corner.</summary>
    public Vector2 Position { get; }
    /// <summary>Movement since the previous event in canvas units (Drag and Move).</summary>
    public Vector2 Delta { get; }

    public PointerEvent(PointerKind kind, int button, Vector2 uv, Vector2 position, Vector2 delta) {
        Kind = kind;
        Button = button;
        Uv = uv;
        Position = position;
        Delta = delta;
    }
}
