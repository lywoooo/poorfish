using UnityEngine;

public readonly struct ChessMove
{
    public readonly Vector2Int from;
    public readonly Vector2Int to;

    public ChessMove(Vector2Int from, Vector2Int to) {
        this.from = from;
        this.to   = to;
    }
}
