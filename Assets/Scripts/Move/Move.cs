using UnityEngine;
using System;

[Flags]
public enum MoveFlags
{
    None = 0,
    Capture = 1 << 0,
    EnPassant = 1 << 1,
    Castling = 1 << 2,
    Promotion = 1 << 3
}

public readonly struct Move
{
    // readonly fields
    public readonly int from;
    public readonly int to;
    public readonly MoveFlags flags; 
    public readonly PieceType promotionType; 

    // bool move updates
    public bool isCapture => (flags & MoveFlags.Capture) != 0;
    public bool isEnPassant => (flags & MoveFlags.EnPassant) != 0;
    public bool isCastling => (flags & MoveFlags.Castling) != 0;
    public bool isPromotion => (flags & MoveFlags.Promotion) != 0;

    // move constructor; flexible params with default params from & to and flex params flags and promoType
    public Move(
        int from,
        int to,
        MoveFlags flags = MoveFlags.None,
        PieceType promotionType = PieceType.None)
    {
        this.from = from;
        this.to = to;
        this.flags = flags;
        this.promotionType = promotionType;
    }
}
