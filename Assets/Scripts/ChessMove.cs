using UnityEngine;

public readonly struct ChessMove
{
    public readonly Vector2Int from;
    public readonly Vector2Int to;
    public readonly bool isEnPassant;
    public readonly bool isCastling;
    public readonly Vector2Int rookFrom;
    public readonly Vector2Int rookTo;
    public readonly PieceType promotionType;

    public ChessMove(Vector2Int from, Vector2Int to) {
        this.from = from;
        this.to   = to;
        isEnPassant = false;
        isCastling = false;
        rookFrom = new Vector2Int(-1, -1);
        rookTo = new Vector2Int(-1, -1);
        promotionType = PieceType.None;
    }

    public ChessMove(
        Vector2Int from,
        Vector2Int to,
        bool isEnPassant = false,
        bool isCastling = false,
        Vector2Int rookFrom = default,
        Vector2Int rookTo = default,
        PieceType promotionType = PieceType.None)
    {
        this.from = from;
        this.to = to;
        this.isEnPassant = isEnPassant;
        this.isCastling = isCastling;
        this.rookFrom = rookFrom;
        this.rookTo = rookTo;
        this.promotionType = promotionType;
    }
}
