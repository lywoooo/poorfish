using System;
using UnityEngine;

public enum PieceColor { White, Black }

public class BoardState
{
    private const ulong HashOffset = 1469598103934665603UL;
    private const ulong HashPrime = 1099511628211UL;

    public struct BoardPiece {
        public PieceType type;
        public PieceColor color;

        public BoardPiece(PieceType typeInputted, PieceColor colorInputted)
        {
            type = typeInputted;
            color = colorInputted;
        }

    }

    public BoardPiece?[,] board;
    public PieceColor currentTurn;
    public bool whiteKingMoved;
    public bool whiteKingsideRookMoved;
    public bool whiteQueensideRookMoved;
    public bool blackKingMoved;
    public bool blackKingsideRookMoved;
    public bool blackQueensideRookMoved;
    public Vector2Int? enPassantTarget;

    public BoardState() {
        board = new BoardPiece?[8, 8];
    }

    public static BoardState boardSnapshot() {
        var gm = GameManager.instance;
        var state = new BoardState();

        for(int col = 0; col < 8; col++) {
            for(int row = 0; row < 8; row++) {
                GameObject pieceSnapshotted = gm.PieceAtGrid(new Vector2Int(col, row));

                if(pieceSnapshotted == null) continue;

                state.board[col, row] = new BoardPiece(gm.GetPieceType(pieceSnapshotted), gm.GetPieceColor(pieceSnapshotted));
            }
        }

        state.currentTurn = gm.CurrentTurnColor;
        state.whiteKingMoved = gm.WhiteKingMoved;
        state.whiteKingsideRookMoved = gm.WhiteKingsideRookMoved;
        state.whiteQueensideRookMoved = gm.WhiteQueensideRookMoved;
        state.blackKingMoved = gm.BlackKingMoved;
        state.blackKingsideRookMoved = gm.BlackKingsideRookMoved;
        state.blackQueensideRookMoved = gm.BlackQueensideRookMoved;
        state.enPassantTarget = gm.EnPassantTarget;

        return state;
    }

    public BoardState cloneBoard() {
        var clone = new BoardState {
            currentTurn = currentTurn,
            whiteKingMoved = whiteKingMoved,
            whiteKingsideRookMoved = whiteKingsideRookMoved,
            whiteQueensideRookMoved = whiteQueensideRookMoved,
            blackKingMoved = blackKingMoved,
            blackKingsideRookMoved = blackKingsideRookMoved,
            blackQueensideRookMoved = blackQueensideRookMoved,
            enPassantTarget = enPassantTarget
        };

        for (int col = 0; col < 8; col++)
        {
            for (int row = 0; row < 8; row++)
            {
                clone.board[col, row] = board[col, row];
            }
        }

        return clone;
    }

    public void applyMove(ChessMove move) {
        var piece = board[move.from.x, move.from.y];
        var capturedPiece = board[move.to.x, move.to.y];

        enPassantTarget = null;

        if (piece.HasValue && move.isEnPassant)
        {
            int capturedPawnRow = move.to.y + (piece.Value.color == PieceColor.White ? -1 : 1);
            capturedPiece = board[move.to.x, capturedPawnRow];
            board[move.to.x, capturedPawnRow] = null;
        }

        board[move.to.x, move.to.y] = piece;
        board[move.from.x, move.from.y] = null;

        if(piece == null) return;

        UpdateCastlingState(piece.Value, move.from, capturedPiece, move.to);

        if (move.isCastling)
        {
            var rook = board[move.rookFrom.x, move.rookFrom.y];
            board[move.rookTo.x, move.rookTo.y] = rook;
            board[move.rookFrom.x, move.rookFrom.y] = null;
        }

        if(piece.Value.type == PieceType.Pawn) {
            bool whitePromotes = piece.Value.color == PieceColor.White && move.to.y == 7;
            bool blackPromotes = piece.Value.color == PieceColor.Black && move.to.y == 0;

            if (Mathf.Abs(move.to.y - move.from.y) == 2)
            {
                enPassantTarget = new Vector2Int(move.from.x, (move.from.y + move.to.y) / 2);
            }

            if (whitePromotes || blackPromotes)
            {
                PieceType promotionType = move.promotionType == PieceType.None ? PieceType.Queen : move.promotionType;
                board[move.to.x, move.to.y] = new BoardPiece(promotionType, piece.Value.color);
            }
        }
    }

    public static bool InBounds(int col, int row)
    {
        return col >= 0 && col <= 7 && row >= 0 && row <= 7;
    }

    public BoardPiece? whatIsAt(int col, int row) {
        return InBounds(col, row) ? board[col, row] : null;
    }

    public void switchTurn() {
        currentTurn = currentTurn == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }

    public PieceColor opponent(PieceColor color) {
        return color == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }

    public Vector2Int findKing(PieceColor color)
    {
        for (int col = 0; col < 8; col++)
            for (int row = 0; row < 8; row++)
            {
                var currentTile = board[col, row];
                if (currentTile.HasValue && currentTile.Value.type == PieceType.King && currentTile.Value.color == color) return new Vector2Int(col, row);
            }
        return new Vector2Int(-1, -1);
    }

    public ulong ComputeHash()
    {
        ulong hash = HashOffset;

        for (int col = 0; col < 8; col++)
        {
            for (int row = 0; row < 8; row++)
            {
                ulong encoded = 0UL;
                var piece = board[col, row];

                if (piece.HasValue)
                {
                    encoded = 1UL + (ulong)piece.Value.type + ((ulong)piece.Value.color << 3);
                }

                hash ^= encoded + (ulong)(col * 8 + row);
                hash *= HashPrime;
            }
        }

        hash ^= (ulong)currentTurn + 97UL;
        hash *= HashPrime;

        hash ^= whiteKingMoved ? 0x1UL : 0UL;
        hash *= HashPrime;
        hash ^= whiteKingsideRookMoved ? 0x2UL : 0UL;
        hash *= HashPrime;
        hash ^= whiteQueensideRookMoved ? 0x4UL : 0UL;
        hash *= HashPrime;
        hash ^= blackKingMoved ? 0x8UL : 0UL;
        hash *= HashPrime;
        hash ^= blackKingsideRookMoved ? 0x10UL : 0UL;
        hash *= HashPrime;
        hash ^= blackQueensideRookMoved ? 0x20UL : 0UL;
        hash *= HashPrime;

        if (enPassantTarget.HasValue)
        {
            hash ^= (ulong)(enPassantTarget.Value.x * 8 + enPassantTarget.Value.y + 193);
            hash *= HashPrime;
        }

        return hash;
    }

    private void UpdateCastlingState(BoardPiece movingPiece, Vector2Int from, BoardPiece? capturedPiece, Vector2Int capturePosition)
    {
        if (movingPiece.type == PieceType.King)
        {
            if (movingPiece.color == PieceColor.White)
            {
                whiteKingMoved = true;
            }
            else
            {
                blackKingMoved = true;
            }
        }
        else if (movingPiece.type == PieceType.Rook)
        {
            MarkRookMoved(movingPiece.color, from);
        }

        if (capturedPiece.HasValue && capturedPiece.Value.type == PieceType.Rook)
        {
            MarkRookMoved(capturedPiece.Value.color, capturePosition);
        }
    }

    private void MarkRookMoved(PieceColor color, Vector2Int position)
    {
        if (color == PieceColor.White)
        {
            if (position.x == 0 && position.y == 0) whiteQueensideRookMoved = true;
            if (position.x == 7 && position.y == 0) whiteKingsideRookMoved = true;
            return;
        }

        if (position.x == 0 && position.y == 7) blackQueensideRookMoved = true;
        if (position.x == 7 && position.y == 7) blackKingsideRookMoved = true;
    }
}
