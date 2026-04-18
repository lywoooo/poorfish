using System.Collections.Generic;
using UnityEngine;

public static class MoveGenerator
{
    private static readonly Vector2Int[] rookDirections = {
        new Vector2Int(0, 1), new Vector2Int(1, 0),
        new Vector2Int(0, -1), new Vector2Int(-1, 0)
    };

    private static readonly Vector2Int[] bishopDirections = {
        new Vector2Int(1, 1), new Vector2Int(1, -1),
        new Vector2Int(-1, -1), new Vector2Int(-1, 1)
    };

    private static readonly Vector2Int[] kingDirections = {
        new Vector2Int(0, 1), new Vector2Int(1, 1),
        new Vector2Int(1, 0), new Vector2Int(1, -1),
        new Vector2Int(0, -1), new Vector2Int(-1, -1),
        new Vector2Int(-1, 0), new Vector2Int(-1, 1)
    };

    private static readonly int[] knightXChange = { -1,  1,  2, -2,  2, -2,  1, -1 };
    private static readonly int[] knightYChange = {  2,  2,  1,  1, -1, -1, -2, -2 };

    public static List<Move> getLegalMoves(BoardState state, PieceColor color)
    {
        var legalMoves = new List<Move>(64);
        var unfilteredMoves = new List<Move>(64);
        GetLegalMoves(state, color, legalMoves, unfilteredMoves);
        return legalMoves;
    }

    public static void GetLegalMoves(
        BoardState state,
        PieceColor color,
        List<Move> legalMoves,
        List<Move> unfilteredMoves)
    {
        legalMoves.Clear();
        unfilteredMoves.Clear();
        AddUnfilteredMoves(state, color, unfilteredMoves);

        int kingSquare = state.FindKingSquare(color);
        if (kingSquare < 0)
        {
            return;
        }

        PieceColor opponentColor = Opponent(color);

        foreach (var unfilteredMove in unfilteredMoves)
        {
            if (MoveLeavesKingSafe(state, unfilteredMove, kingSquare, opponentColor))
            {
                legalMoves.Add(unfilteredMove);
            }
        }
    }

    public static void GetLegalMovesFromSquare(
        BoardState state,
        PieceColor color,
        int fromSquare,
        List<Move> legalMoves,
        List<Move> candidateMoves)
    {
        legalMoves.Clear();
        candidateMoves.Clear();

        if (fromSquare < 0 || fromSquare >= state.board.Length)
        {
            return;
        }

        int piece = state.board[fromSquare];
        if (PieceBits.isEmpty(piece) || PieceBits.GetColor(piece) != color)
        {
            return;
        }

        int kingSquare = state.FindKingSquare(color);
        if (kingSquare < 0)
        {
            return;
        }

        AddMovesForPiece(state, fromSquare, PieceBits.GetType(piece), color, candidateMoves);
        PieceColor opponentColor = Opponent(color);
        foreach (Move candidateMove in candidateMoves)
        {
            if (MoveLeavesKingSafe(state, candidateMove, kingSquare, opponentColor))
            {
                legalMoves.Add(candidateMove);
            }
        }
    }

    public static bool isInCheck(BoardState state, PieceColor color)
    {
        int kingSquare = state.FindKingSquare(color);

        if (kingSquare < 0)
        {
            return true;
        }

        return IsSquareAttacked(state, kingSquare, Opponent(color));
    }

    public static bool hasAnyLegalMove(BoardState state, PieceColor color)
    {
        var candidateMoves = new List<Move>(16);
        return HasAnyLegalMove(state, color, candidateMoves);
    }

    public static bool HasAnyLegalMove(BoardState state, PieceColor color, List<Move> candidateMoves)
    {
        int kingSquare = state.FindKingSquare(color);
        if (kingSquare < 0)
        {
            return false;
        }

        PieceColor opponentColor = Opponent(color);

        for (int square = 0; square < state.board.Length; square++)
        {
            int currentTile = state.board[square];
            if (PieceBits.isEmpty(currentTile) || PieceBits.GetColor(currentTile) != color)
            {
                continue;
            }

            candidateMoves.Clear();
            AddMovesForPiece(state, square, PieceBits.GetType(currentTile), color, candidateMoves);
            foreach (Move candidateMove in candidateMoves)
            {
                if (MoveLeavesKingSafe(state, candidateMove, kingSquare, opponentColor))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool MoveLeavesKingSafe(
        BoardState state,
        Move move,
        int kingSquare,
        PieceColor opponentColor)
    {
        BoardState.MoveUndo undo = state.MakeMove(move);
        int checkedKingSquare = move.from == kingSquare ? move.to : kingSquare;
        bool isSafe = !IsSquareAttacked(state, checkedKingSquare, opponentColor);
        state.UnmakeMove(move, undo);
        return isSafe;
    }

    private static bool IsSquareAttacked(BoardState state, int square, PieceColor attackerColor)
    {
        int squareCol = square % 8;
        int squareRow = square / 8;
        int pawnAttackRow = squareRow + (attackerColor == PieceColor.White ? -1 : 1);

        if (IsEnemyPiece(state, squareCol - 1, pawnAttackRow, attackerColor, PieceType.Pawn) ||
            IsEnemyPiece(state, squareCol + 1, pawnAttackRow, attackerColor, PieceType.Pawn))
        {
            return true;
        }

        for (int i = 0; i < knightXChange.Length; i++)
        {
            if (IsEnemyPiece(state, squareCol + knightXChange[i], squareRow + knightYChange[i], attackerColor, PieceType.Knight))
            {
                return true;
            }
        }

        if (IsAttackedBySlidingPiece(state, squareCol, squareRow, attackerColor, bishopDirections, PieceType.Bishop) ||
            IsAttackedBySlidingPiece(state, squareCol, squareRow, attackerColor, rookDirections, PieceType.Rook))
        {
            return true;
        }

        foreach (var direction in kingDirections)
        {
            if (IsEnemyPiece(state, squareCol + direction.x, squareRow + direction.y, attackerColor, PieceType.King))
            {
                return true;
            }
        }

        return false;
    }

    public static bool isCheckmate(BoardState state, PieceColor color)
    {
        return isInCheck(state, color) && !hasAnyLegalMove(state, color);
    }

    public static bool isStalemate(BoardState state, PieceColor color)
    {
        return !isInCheck(state, color) && !hasAnyLegalMove(state, color);
    }

    private static void AddUnfilteredMoves(BoardState state, PieceColor color, List<Move> unfilteredMoves)
    {
        for (int square = 0; square < state.board.Length; square++)
        {
            int currentTile = state.board[square];
            if (PieceBits.isEmpty(currentTile) || PieceBits.GetColor(currentTile) != color)
            {
                continue;
            }

            AddMovesForPiece(state, square, PieceBits.GetType(currentTile), color, unfilteredMoves);
        }
    }

    private static void AddMovesForPiece(BoardState state, int fromSquare, PieceType type, PieceColor color, List<Move> moves)
    {
        int col = fromSquare % 8;
        int row = fromSquare / 8;

        switch (type)
        {
            case PieceType.Bishop:
                AddSlidingMoves(state, fromSquare, col, row, color, bishopDirections, moves);
                return;
            case PieceType.Rook:
                AddSlidingMoves(state, fromSquare, col, row, color, rookDirections, moves);
                return;
            case PieceType.Queen:
                AddSlidingMoves(state, fromSquare, col, row, color, bishopDirections, moves);
                AddSlidingMoves(state, fromSquare, col, row, color, rookDirections, moves);
                return;
            case PieceType.King:
                AddKingMoves(state, fromSquare, col, row, color, moves);
                return;
            case PieceType.Knight:
                AddKnightMoves(state, fromSquare, col, row, color, moves);
                return;
            case PieceType.Pawn:
                AddPawnMoves(state, fromSquare, col, row, color, moves);
                return;
            default:
                return;
        }
    }

    private static void AddSlidingMoves(
        BoardState state,
        int fromSquare,
        int startCol,
        int startRow,
        PieceColor color,
        Vector2Int[] directions,
        List<Move> moves)
    {
        foreach (var direction in directions)
        {
            for (int i = 1; i < 8; i++)
            {
                int col = startCol + i * direction.x;
                int row = startRow + i * direction.y;

                if (!BoardState.InBounds(col, row))
                {
                    break;
                }

                int destination = state.board[BoardState.SquareIndex(col, row)];
                if (PieceBits.isEmpty(destination))
                {
                    moves.Add(new Move(fromSquare, BoardState.SquareIndex(col, row)));
                    continue;
                }

                if (PieceBits.GetColor(destination) != color && PieceBits.GetType(destination) != PieceType.King)
                {
                    moves.Add(new Move(fromSquare, BoardState.SquareIndex(col, row), MoveFlags.Capture));
                }

                break;
            }
        }
    }

    private static void AddKingMoves(BoardState state, int fromSquare, int startCol, int startRow, PieceColor color, List<Move> moves)
    {
        foreach (var direction in kingDirections)
        {
            AddIfLegal(state, fromSquare, color, startCol + direction.x, startRow + direction.y, moves);
        }

        AddCastlingMoves(state, new Vector2Int(startCol, startRow), color, moves);
    }

    private static void AddKnightMoves(BoardState state, int fromSquare, int startCol, int startRow, PieceColor color, List<Move> moves)
    {
        for (int i = 0; i < knightXChange.Length; i++)
        {
            AddIfLegal(state, fromSquare, color, startCol + knightXChange[i], startRow + knightYChange[i], moves);
        }
    }

    private static void AddPawnMoves(BoardState state, int fromSquare, int startCol, int currentRow, PieceColor color, List<Move> moves)
    {
        int forward = color == PieceColor.White ? 1 : -1;
        int pawnStartRow = color == PieceColor.White ? 1 : 6;
        int promotionRow = color == PieceColor.White ? 7 : 0;

        int oneRow = currentRow + forward;
        if (BoardState.InBounds(startCol, oneRow) && PieceBits.isEmpty(state.board[BoardState.SquareIndex(startCol, oneRow)]))
        {
            AddPawnMove(moves, fromSquare, BoardState.SquareIndex(startCol, oneRow), oneRow == promotionRow, MoveFlags.None);

            if (currentRow == pawnStartRow)
            {
                int twoRow = currentRow + 2 * forward;
                if (BoardState.InBounds(startCol, twoRow) && PieceBits.isEmpty(state.board[BoardState.SquareIndex(startCol, twoRow)]))
                {
                    moves.Add(new Move(fromSquare, BoardState.SquareIndex(startCol, twoRow)));
                }
            }
        }

        AddPawnCapture(state, moves, fromSquare, color, startCol - 1, oneRow, promotionRow);
        AddPawnCapture(state, moves, fromSquare, color, startCol + 1, oneRow, promotionRow);
        AddEnPassantMove(state, moves, fromSquare, currentRow, color, oneRow);
    }

    private static void AddPawnCapture(BoardState state, List<Move> destinations, int fromSquare, PieceColor color, int col, int row, int promotionRow)
    {
        if (!BoardState.InBounds(col, row))
        {
            return;
        }

        int capturedPiece = state.board[BoardState.SquareIndex(col, row)];
        if (!PieceBits.isEmpty(capturedPiece) && PieceBits.GetColor(capturedPiece) != color && PieceBits.GetType(capturedPiece) != PieceType.King)
        {
            AddPawnMove(destinations, fromSquare, BoardState.SquareIndex(col, row), row == promotionRow, MoveFlags.Capture);
        }
    }

    private static void AddEnPassantMove(BoardState state, List<Move> destinations, int fromSquare, int fromRow, PieceColor color, int oneRow)
    {
        if (state.enPassantTarget < 0)
        {
            return;
        }

        int targetCol = state.enPassantTarget % 8;
        int targetRow = state.enPassantTarget / 8;
        int fromCol = fromSquare % 8;
        if (targetRow != oneRow || Mathf.Abs(targetCol - fromCol) != 1)
        {
            return;
        }

        int capturedPawnSquare = BoardState.SquareIndex(targetCol, fromRow);
        int capturedPawn = state.board[capturedPawnSquare];
        if (!PieceBits.isEmpty(capturedPawn) &&
            PieceBits.GetColor(capturedPawn) != color &&
            PieceBits.GetType(capturedPawn) == PieceType.Pawn)
        {
            destinations.Add(new Move(fromSquare, BoardState.SquareIndex(targetCol, targetRow), MoveFlags.Capture | MoveFlags.EnPassant));
        }
    }

    private static void AddIfLegal(BoardState state, int fromSquare, PieceColor color, int col, int row, List<Move> destinations)
    {
        if (!BoardState.InBounds(col, row))
        {
            return;
        }

        int destination = state.board[BoardState.SquareIndex(col, row)];
        if (PieceBits.isEmpty(destination))
        {
            destinations.Add(new Move(fromSquare, BoardState.SquareIndex(col, row)));
            return;
        }

        if (PieceBits.GetColor(destination) != color && PieceBits.GetType(destination) != PieceType.King)
        {
            destinations.Add(new Move(fromSquare, BoardState.SquareIndex(col, row), MoveFlags.Capture));
        }
    }

    private static bool IsEnemyPiece(BoardState state, int col, int row, PieceColor color, PieceType type)
    {
        if (!BoardState.InBounds(col, row))
        {
            return false;
        }

        int piece = state.board[BoardState.SquareIndex(col, row)];
        return !PieceBits.isEmpty(piece) && PieceBits.GetColor(piece) == color && PieceBits.GetType(piece) == type;
    }

    private static bool IsAttackedBySlidingPiece(
        BoardState state,
        int targetCol,
        int targetRow,
        PieceColor attackerColor,
        Vector2Int[] directions,
        PieceType matchingSlider)
    {
        foreach (var direction in directions)
        {
            int col = targetCol + direction.x;
            int row = targetRow + direction.y;

            while (BoardState.InBounds(col, row))
            {
                int piece = state.board[BoardState.SquareIndex(col, row)];
                if (!PieceBits.isEmpty(piece))
                {
                    if (PieceBits.GetColor(piece) == attackerColor &&
                        (PieceBits.GetType(piece) == matchingSlider || PieceBits.GetType(piece) == PieceType.Queen))
                    {
                        return true;
                    }

                    break;
                }

                col += direction.x;
                row += direction.y;
            }
        }

        return false;
    }

    private static void AddCastlingMoves(BoardState state, Vector2Int kingPosition, PieceColor color, List<Move> destinations)
    {
        if (isInCheck(state, color))
        {
            return;
        }

        int backRank = color == PieceColor.White ? 0 : 7;
        TryAddCastleMove(state, color, kingPosition, backRank, kingside: true, destinations);
        TryAddCastleMove(state, color, kingPosition, backRank, kingside: false, destinations);
    }

    private static void TryAddCastleMove(BoardState state, PieceColor color, Vector2Int kingPosition, int backRank, bool kingside, List<Move> destinations)
    {
        CastlingRights requiredRight = CastlingRight(color, kingside);
        if ((state.castlingRights & requiredRight) == 0)
        {
            return;
        }

        int rookCol = kingside ? 7 : 0;
        int kingTargetCol = kingside ? 6 : 2;
        int step = kingside ? 1 : -1;
        int rook = state.whatIsAt(rookCol, backRank);

        if (PieceBits.isEmpty(rook) || PieceBits.GetColor(rook) != color || PieceBits.GetType(rook) != PieceType.Rook)
        {
            return;
        }

        for (int col = kingPosition.x + step; col != rookCol; col += step)
        {
            if (!PieceBits.isEmpty(state.whatIsAt(col, backRank)))
            {
                return;
            }
        }

        for (int col = kingPosition.x + step; col != kingTargetCol + step; col += step)
        {
            Move intermediateMove = new Move(kingPosition, new Vector2Int(col, backRank));
            BoardState.MoveUndo undo = state.MakeMove(intermediateMove);
            bool isAttacked = isInCheck(state, color);
            state.UnmakeMove(intermediateMove, undo);

            if (isAttacked)
            {
                return;
            }
        }

        destinations.Add(new Move(kingPosition, new Vector2Int(kingTargetCol, backRank), MoveFlags.Castling));
    }

    private static CastlingRights CastlingRight(PieceColor color, bool kingside)
    {
        if (color == PieceColor.White)
        {
            return kingside ? CastlingRights.WhiteKingside : CastlingRights.WhiteQueenside;
        }

        return kingside ? CastlingRights.BlackKingside : CastlingRights.BlackQueenside;
    }

    private static PieceColor Opponent(PieceColor color)
    {
        return color == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }

    private static void AddPawnMove(List<Move> destinations, int from, int to, bool isPromotion, MoveFlags flags)
    {
        if (isPromotion)
        {
            MoveFlags promotionFlags = flags | MoveFlags.Promotion;
            destinations.Add(new Move(from, to, promotionFlags, PieceType.Queen));
            destinations.Add(new Move(from, to, promotionFlags, PieceType.Rook));
            destinations.Add(new Move(from, to, promotionFlags, PieceType.Bishop));
            destinations.Add(new Move(from, to, promotionFlags, PieceType.Knight));
            return;
        }

        destinations.Add(new Move(from, to, flags));
    }
}
