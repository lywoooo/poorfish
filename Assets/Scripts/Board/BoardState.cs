using UnityEngine;

public class BoardState
{
    public int[] board;
    public PieceColor currentTurn;
    public CastlingRights castlingRights;
    public int enPassantTarget = -1;
    public Move lastMove;
    public bool hasLastMove;
    public Move lastWhiteMove;
    public bool hasLastWhiteMove;
    public Move lastBlackMove;
    public bool hasLastBlackMove;
    public int halfmoveClock;
    public int whiteKingSquare = -1;
    public int blackKingSquare = -1;

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
                int square = SquareIndex(col, row);
                state.board[square] = PieceBits.CreatePiece(type, color);
                if (type == PieceType.King)
                {
                    state.SetKingSquare(color, square);
                }
            }
        }

        state.currentTurn = gm.CurrentTurnColor;
        state.castlingRights = CastlingRightsFromGameManager(gm);
        state.enPassantTarget = SquareIndexOrNone(gm.EnPassantTarget);
        state.hasLastMove = gm.HasLastAppliedMove;
        state.lastMove = gm.LastAppliedMove;
        state.hasLastWhiteMove = gm.HasLastWhiteAppliedMove;
        state.lastWhiteMove = gm.LastWhiteAppliedMove;
        state.hasLastBlackMove = gm.HasLastBlackAppliedMove;
        state.lastBlackMove = gm.LastBlackAppliedMove;
        state.halfmoveClock = gm.HalfmoveClock;

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
            lastMove = lastMove,
            hasLastWhiteMove = hasLastWhiteMove,
            lastWhiteMove = lastWhiteMove,
            hasLastBlackMove = hasLastBlackMove,
            lastBlackMove = lastBlackMove,
            halfmoveClock = halfmoveClock,
            whiteKingSquare = whiteKingSquare,
            blackKingSquare = blackKingSquare
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
        public readonly Move previousLastWhiteMove;
        public readonly bool previousHasLastWhiteMove;
        public readonly Move previousLastBlackMove;
        public readonly bool previousHasLastBlackMove;
        public readonly int previousHalfmoveClock;
        public readonly int previousWhiteKingSquare;
        public readonly int previousBlackKingSquare;

        public MoveUndo(
            int movedPiece,
            int capturedPiece,
            int capturedSquare,
            PieceColor previousCurrentTurn,
            CastlingRights previousCastlingRights,
            int previousEnPassantTarget,
            Move previousLastMove,
            bool previousHasLastMove,
            Move previousLastWhiteMove,
            bool previousHasLastWhiteMove,
            Move previousLastBlackMove,
            bool previousHasLastBlackMove,
            int previousHalfmoveClock,
            int previousWhiteKingSquare,
            int previousBlackKingSquare)
        {
            this.movedPiece = movedPiece;
            this.capturedPiece = capturedPiece;
            this.capturedSquare = capturedSquare;
            this.previousCurrentTurn = previousCurrentTurn;
            this.previousCastlingRights = previousCastlingRights;
            this.previousEnPassantTarget = previousEnPassantTarget;
            this.previousLastMove = previousLastMove;
            this.previousHasLastMove = previousHasLastMove;
            this.previousLastWhiteMove = previousLastWhiteMove;
            this.previousHasLastWhiteMove = previousHasLastWhiteMove;
            this.previousLastBlackMove = previousLastBlackMove;
            this.previousHasLastBlackMove = previousHasLastBlackMove;
            this.previousHalfmoveClock = previousHalfmoveClock;
            this.previousWhiteKingSquare = previousWhiteKingSquare;
            this.previousBlackKingSquare = previousBlackKingSquare;
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
                hasLastMove,
                lastWhiteMove,
                hasLastWhiteMove,
                lastBlackMove,
                hasLastBlackMove,
                halfmoveClock,
                whiteKingSquare,
                blackKingSquare);
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
            hasLastMove,
            lastWhiteMove,
            hasLastWhiteMove,
            lastBlackMove,
            hasLastBlackMove,
            halfmoveClock,
            whiteKingSquare,
            blackKingSquare);

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
                undo.previousHasLastMove,
                undo.previousLastWhiteMove,
                undo.previousHasLastWhiteMove,
                undo.previousLastBlackMove,
                undo.previousHasLastBlackMove,
                undo.previousHalfmoveClock,
                undo.previousWhiteKingSquare,
                undo.previousBlackKingSquare);
            board[capturedPawnSquare] = PieceBits.None;
        }

        bool resetsHalfmoveClock = type == PieceType.Pawn || !PieceBits.isEmpty(capturedPiece);
        halfmoveClock = resetsHalfmoveClock ? 0 : halfmoveClock + 1;

        board[move.to] = piece;
        board[move.from] = PieceBits.None;
        if (type == PieceType.King)
        {
            SetKingSquare(color, move.to);
        }

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
        SetLastMoveForColor(color, move);

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
            lastWhiteMove = undo.previousLastWhiteMove;
            hasLastWhiteMove = undo.previousHasLastWhiteMove;
            lastBlackMove = undo.previousLastBlackMove;
            hasLastBlackMove = undo.previousHasLastBlackMove;
            halfmoveClock = undo.previousHalfmoveClock;
            whiteKingSquare = undo.previousWhiteKingSquare;
            blackKingSquare = undo.previousBlackKingSquare;
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
        lastWhiteMove = undo.previousLastWhiteMove;
        hasLastWhiteMove = undo.previousHasLastWhiteMove;
        lastBlackMove = undo.previousLastBlackMove;
        hasLastBlackMove = undo.previousHasLastBlackMove;
        halfmoveClock = undo.previousHalfmoveClock;
        whiteKingSquare = undo.previousWhiteKingSquare;
        blackKingSquare = undo.previousBlackKingSquare;
    }

    public bool TryGetLastMoveForColor(PieceColor color, out Move move)
    {
        if (color == PieceColor.White)
        {
            move = lastWhiteMove;
            return hasLastWhiteMove;
        }

        move = lastBlackMove;
        return hasLastBlackMove;
    }

    private void SetLastMoveForColor(PieceColor color, Move move)
    {
        if (color == PieceColor.White)
        {
            lastWhiteMove = move;
            hasLastWhiteMove = true;
            return;
        }

        lastBlackMove = move;
        hasLastBlackMove = true;
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
        int square = FindKingSquare(color);
        return square >= 0
            ? new Vector2Int(square % 8, square / 8)
            : new Vector2Int(-1, -1);
    }

    public int FindKingSquare(PieceColor color)
    {
        int cachedSquare = color == PieceColor.White ? whiteKingSquare : blackKingSquare;
        if (cachedSquare >= 0 &&
            cachedSquare < board.Length &&
            !PieceBits.isEmpty(board[cachedSquare]) &&
            PieceBits.GetType(board[cachedSquare]) == PieceType.King &&
            PieceBits.GetColor(board[cachedSquare]) == color)
        {
            return cachedSquare;
        }

        for (int square = 0; square < board.Length; square++)
        {
            int piece = board[square];
            if (!PieceBits.isEmpty(piece) &&
                PieceBits.GetType(piece) == PieceType.King &&
                PieceBits.GetColor(piece) == color)
            {
                SetKingSquare(color, square);
                return square;
            }
        }

        SetKingSquare(color, -1);
        return -1;
    }

    public void SetKingSquare(PieceColor color, int square)
    {
        if (color == PieceColor.White)
        {
            whiteKingSquare = square;
            return;
        }

        blackKingSquare = square;
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
