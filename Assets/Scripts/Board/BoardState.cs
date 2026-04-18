using UnityEngine;

public class BoardState
{
    public int[] board;
    public PieceColor currentTurn;
    public CastlingRights castlingRights;
    public int enPassantTarget = -1;
    public Move lastMove;
    public bool hasLastMove;

    public BoardState()
    {
        board = new int[64];
    }

    public static BoardState boardSnapshot()
    {
        var gm = GameManager.instance;
        var state = new BoardState();

        for (int col = 0; col < 8; col++)
        {
            for (int row = 0; row < 8; row++)
            {
                GameObject pieceSnapshotted = gm.PieceAtGrid(new Vector2Int(col, row));
                if (pieceSnapshotted == null)
                {
                    continue;
                }

                PieceType type = gm.GetPieceType(pieceSnapshotted);
                PieceColor color = gm.GetPieceColor(pieceSnapshotted);
                state.board[SquareIndex(col, row)] = PieceBits.CreatePiece(type, color);
            }
        }

        state.currentTurn = gm.CurrentTurnColor;
        state.castlingRights = CastlingRightsFromGameManager(gm);
        state.enPassantTarget = SquareIndexOrNone(gm.EnPassantTarget);
        state.hasLastMove = gm.HasLastAppliedMove;
        state.lastMove = gm.LastAppliedMove;

        return state;
    }

    public BoardState cloneBoard()
    {
        var clone = new BoardState
        {
            currentTurn = currentTurn,
            castlingRights = castlingRights,
            enPassantTarget = enPassantTarget,
            hasLastMove = hasLastMove,
            lastMove = lastMove
        };

        System.Array.Copy(board, clone.board, board.Length);
        return clone;
    }

    public readonly struct MoveUndo
    {
        public readonly int movedPiece;
        public readonly int capturedPiece;
        public readonly int capturedSquare;
        public readonly PieceColor previousCurrentTurn;
        public readonly CastlingRights previousCastlingRights;
        public readonly int previousEnPassantTarget;
        public readonly Move previousLastMove;
        public readonly bool previousHasLastMove;

        public MoveUndo(
            int movedPiece,
            int capturedPiece,
            int capturedSquare,
            PieceColor previousCurrentTurn,
            CastlingRights previousCastlingRights,
            int previousEnPassantTarget,
            Move previousLastMove,
            bool previousHasLastMove)
        {
            this.movedPiece = movedPiece;
            this.capturedPiece = capturedPiece;
            this.capturedSquare = capturedSquare;
            this.previousCurrentTurn = previousCurrentTurn;
            this.previousCastlingRights = previousCastlingRights;
            this.previousEnPassantTarget = previousEnPassantTarget;
            this.previousLastMove = previousLastMove;
            this.previousHasLastMove = previousHasLastMove;
        }
    }

    public MoveUndo MakeMove(Move move)
    {
        int piece = board[move.from];
        if (PieceBits.isEmpty(piece))
        {
            return new MoveUndo(
                PieceBits.None,
                PieceBits.None,
                move.to,
                currentTurn,
                castlingRights,
                enPassantTarget,
                lastMove,
                hasLastMove);
        }

        int capturedPiece = board[move.to];
        int capturedSquare = move.to;
        PieceColor color = PieceBits.GetColor(piece);
        PieceType type = PieceBits.GetType(piece);
        int fromRow = move.from / 8;
        int toRow = move.to / 8;

        MoveUndo undo = new MoveUndo(
            piece,
            capturedPiece,
            capturedSquare,
            currentTurn,
            castlingRights,
            enPassantTarget,
            lastMove,
            hasLastMove);

        enPassantTarget = -1;

        if (move.isEnPassant)
        {
            int capturedPawnSquare = move.to + (color == PieceColor.White ? -8 : 8);
            capturedPiece = board[capturedPawnSquare];
            capturedSquare = capturedPawnSquare;
            undo = new MoveUndo(
                piece,
                capturedPiece,
                capturedSquare,
                undo.previousCurrentTurn,
                undo.previousCastlingRights,
                undo.previousEnPassantTarget,
                undo.previousLastMove,
                undo.previousHasLastMove);
            board[capturedPawnSquare] = PieceBits.None;
        }

        board[move.to] = piece;
        board[move.from] = PieceBits.None;

        UpdateCastlingState(piece, move.from, capturedPiece, move.to);

        if (move.isCastling)
        {
            int rookFrom = move.to > move.from ? move.to + 1 : move.to - 2;
            int rookTo = move.to > move.from ? move.to - 1 : move.to + 1;
            board[rookTo] = board[rookFrom];
            board[rookFrom] = PieceBits.None;
        }

        if (type == PieceType.Pawn)
        {
            if (Mathf.Abs(toRow - fromRow) == 2)
            {
                enPassantTarget = (move.from + move.to) / 2;
            }

            bool whitePromotes = color == PieceColor.White && toRow == 7;
            bool blackPromotes = color == PieceColor.Black && toRow == 0;
            if (whitePromotes || blackPromotes)
            {
                PieceType promotionType = move.promotionType == PieceType.None ? PieceType.Queen : move.promotionType;
                board[move.to] = PieceBits.CreatePiece(promotionType, color);
            }
        }

        lastMove = move;
        hasLastMove = true;

        return undo;
    }

    public void UnmakeMove(Move move, MoveUndo undo)
    {
        if (PieceBits.isEmpty(undo.movedPiece))
        {
            currentTurn = undo.previousCurrentTurn;
            castlingRights = undo.previousCastlingRights;
            enPassantTarget = undo.previousEnPassantTarget;
            lastMove = undo.previousLastMove;
            hasLastMove = undo.previousHasLastMove;
            return;
        }

        if (move.isCastling)
        {
            int rookFrom = move.to > move.from ? move.to + 1 : move.to - 2;
            int rookTo = move.to > move.from ? move.to - 1 : move.to + 1;
            board[rookFrom] = board[rookTo];
            board[rookTo] = PieceBits.None;
        }

        board[move.from] = undo.movedPiece;
        board[move.to] = PieceBits.None;

        if (move.isEnPassant)
        {
            board[undo.capturedSquare] = undo.capturedPiece;
        }
        else
        {
            board[move.to] = undo.capturedPiece;
        }

        castlingRights = undo.previousCastlingRights;
        currentTurn = undo.previousCurrentTurn;
        enPassantTarget = undo.previousEnPassantTarget;
        lastMove = undo.previousLastMove;
        hasLastMove = undo.previousHasLastMove;
    }

    public static bool InBounds(int col, int row)
    {
        return col >= 0 && col <= 7 && row >= 0 && row <= 7;
    }

    public static int SquareIndex(int col, int row)
    {
        return col + row * 8;
    }

    public static int SquareIndex(Vector2Int square)
    {
        return SquareIndex(square.x, square.y);
    }

    public int whatIsAt(int col, int row)
    {
        return InBounds(col, row) ? board[SquareIndex(col, row)] : PieceBits.None;
    }

    public void switchTurn()
    {
        currentTurn = currentTurn == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }

    public PieceColor opponent(PieceColor color)
    {
        return color == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }

    public Vector2Int findKing(PieceColor color)
    {
        for (int square = 0; square < board.Length; square++)
        {
            int piece = board[square];
            if (!PieceBits.isEmpty(piece) &&
                PieceBits.GetType(piece) == PieceType.King &&
                PieceBits.GetColor(piece) == color)
            {
                return new Vector2Int(square % 8, square / 8);
            }
        }

        return new Vector2Int(-1, -1);
    }

    private void UpdateCastlingState(int movingPiece, int from, int capturedPiece, int capturePosition)
    {
        PieceType movingType = PieceBits.GetType(movingPiece);
        PieceColor movingColor = PieceBits.GetColor(movingPiece);

        if (movingType == PieceType.King)
        {
            ClearCastlingRights(movingColor);
        }
        else if (movingType == PieceType.Rook)
        {
            MarkRookMoved(movingColor, from);
        }

        if (!PieceBits.isEmpty(capturedPiece) && PieceBits.GetType(capturedPiece) == PieceType.Rook)
        {
            MarkRookMoved(PieceBits.GetColor(capturedPiece), capturePosition);
        }
    }

    private void ClearCastlingRights(PieceColor color)
    {
        if (color == PieceColor.White)
        {
            castlingRights &= ~(CastlingRights.WhiteKingside | CastlingRights.WhiteQueenside);
        }
        else
        {
            castlingRights &= ~(CastlingRights.BlackKingside | CastlingRights.BlackQueenside);
        }
    }

    private void MarkRookMoved(PieceColor color, int square)
    {
        if (color == PieceColor.White)
        {
            if (square == SquareIndex(0, 0)) castlingRights &= ~CastlingRights.WhiteQueenside;
            if (square == SquareIndex(7, 0)) castlingRights &= ~CastlingRights.WhiteKingside;
            return;
        }

        if (square == SquareIndex(0, 7)) castlingRights &= ~CastlingRights.BlackQueenside;
        if (square == SquareIndex(7, 7)) castlingRights &= ~CastlingRights.BlackKingside;
    }

    private static CastlingRights CastlingRightsFromGameManager(GameManager gm)
    {
        CastlingRights rights = CastlingRights.None;

        if (!gm.WhiteKingMoved)
        {
            if (!gm.WhiteKingsideRookMoved) rights |= CastlingRights.WhiteKingside;
            if (!gm.WhiteQueensideRookMoved) rights |= CastlingRights.WhiteQueenside;
        }

        if (!gm.BlackKingMoved)
        {
            if (!gm.BlackKingsideRookMoved) rights |= CastlingRights.BlackKingside;
            if (!gm.BlackQueensideRookMoved) rights |= CastlingRights.BlackQueenside;
        }

        return rights;
    }

    private static int SquareIndexOrNone(Vector2Int? square)
    {
        return square.HasValue ? SquareIndex(square.Value) : -1;
    }
}
