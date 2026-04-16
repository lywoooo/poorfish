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

    private static readonly int[] knightXChange = { -1,  1,  2, -2,  2, -2,  1, -1 };
    private static readonly int[] knightYChange = {  2,  2,  1,  1, -1, -1, -2, -2 };

    public static List<Move> getLegalMoves(BoardState state, PieceColor color)
    {
        var legalMoves = new List<Move>(32);

        foreach (var unfilteredMove in getUnfilteredMoves(state, color))
        {
            BoardState.MoveUndo undo = state.MakeMove(unfilteredMove);

            if (!isInCheck(state, color))
            {
                legalMoves.Add(unfilteredMove);
            }

            state.UnmakeMove(unfilteredMove, undo);
        }

        return legalMoves;
    }

    public static bool isInCheck(BoardState state, PieceColor color)
    {
        var kingPos = state.findKing(color);

        if (kingPos.x == -1)
        {
            return true;
        }

        var opponentColor = state.opponent(color);
        int pawnAttackRow = kingPos.y + (opponentColor == PieceColor.White ? -1 : 1);

        if (IsEnemyPiece(state, kingPos.x - 1, pawnAttackRow, opponentColor, PieceType.Pawn) ||
            IsEnemyPiece(state, kingPos.x + 1, pawnAttackRow, opponentColor, PieceType.Pawn))
        {
            return true;
        }

        for (int i = 0; i < knightXChange.Length; i++)
        {
            if (IsEnemyPiece(state, kingPos.x + knightXChange[i], kingPos.y + knightYChange[i], opponentColor, PieceType.Knight))
            {
                return true;
            }
        }

        if (IsAttackedBySlidingPiece(state, kingPos, opponentColor, bishopDirections, PieceType.Bishop) ||
            IsAttackedBySlidingPiece(state, kingPos, opponentColor, rookDirections, PieceType.Rook))
        {
            return true;
        }

        foreach (var direction in bishopDirections)
        {
            if (IsEnemyPiece(state, kingPos.x + direction.x, kingPos.y + direction.y, opponentColor, PieceType.King))
            {
                return true;
            }
        }

        foreach (var direction in rookDirections)
        {
            if (IsEnemyPiece(state, kingPos.x + direction.x, kingPos.y + direction.y, opponentColor, PieceType.King))
            {
                return true;
            }
        }

        return false;
    }

    public static bool isCheckmate(BoardState state, PieceColor color)
    {
        return isInCheck(state, color) && getLegalMoves(state, color).Count == 0;
    }

    public static bool isStalemate(BoardState state, PieceColor color)
    {
        return !isInCheck(state, color) && getLegalMoves(state, color).Count == 0;
    }

    private static List<Move> getUnfilteredMoves(BoardState state, PieceColor color)
    {
        var unfilteredMoves = new List<Move>(32);

        for (int square = 0; square < state.board.Length; square++)
        {
            int currentTile = state.board[square];
            if (PieceBits.isEmpty(currentTile) || PieceBits.GetColor(currentTile) != color)
            {
                continue;
            }

            var initialPos = new Vector2Int(square % 8, square / 8);
            unfilteredMoves.AddRange(getMovesForPiece(state, initialPos, PieceBits.GetType(currentTile), color));
        }

        return unfilteredMoves;
    }

    private static List<Move> getMovesForPiece(BoardState state, Vector2Int initialPos, PieceType type, PieceColor color)
    {
        switch (type)
        {
            case PieceType.Bishop:
                return bishopRookMoves(state, initialPos, color, bishopDirections);
            case PieceType.Rook:
                return bishopRookMoves(state, initialPos, color, rookDirections);
            case PieceType.Queen:
                var queenMoves = bishopRookMoves(state, initialPos, color, bishopDirections);
                queenMoves.AddRange(bishopRookMoves(state, initialPos, color, rookDirections));
                return queenMoves;
            case PieceType.King:
                return kingMoves(state, initialPos, color);
            case PieceType.Knight:
                return knightMoves(state, initialPos, color);
            case PieceType.Pawn:
                return pawnMoves(state, initialPos, color);
            default:
                return new List<Move>(0);
        }
    }

    private static List<Move> bishopRookMoves(BoardState state, Vector2Int initialPos, PieceColor color, Vector2Int[] directions)
    {
        var destinations = new List<Move>();

        foreach (var direction in directions)
        {
            for (int i = 1; i < 8; i++)
            {
                int col = initialPos.x + i * direction.x;
                int row = initialPos.y + i * direction.y;

                if (!BoardState.InBounds(col, row))
                {
                    break;
                }

                int destination = state.whatIsAt(col, row);
                if (PieceBits.isEmpty(destination))
                {
                    destinations.Add(new Move(initialPos, new Vector2Int(col, row)));
                    continue;
                }

                if (PieceBits.GetColor(destination) != color && PieceBits.GetType(destination) != PieceType.King)
                {
                    destinations.Add(new Move(initialPos, new Vector2Int(col, row), MoveFlags.Capture));
                }

                break;
            }
        }

        return destinations;
    }

    private static List<Move> kingMoves(BoardState state, Vector2Int initialPos, PieceColor color)
    {
        var destinations = new List<Move>(10);

        foreach (var direction in bishopDirections)
        {
            AddIfLegal(state, initialPos, color, initialPos.x + direction.x, initialPos.y + direction.y, destinations);
        }

        foreach (var direction in rookDirections)
        {
            AddIfLegal(state, initialPos, color, initialPos.x + direction.x, initialPos.y + direction.y, destinations);
        }

        AddCastlingMoves(state, initialPos, color, destinations);
        return destinations;
    }

    private static List<Move> knightMoves(BoardState state, Vector2Int initialPos, PieceColor color)
    {
        var destinations = new List<Move>(8);

        for (int i = 0; i < knightXChange.Length; i++)
        {
            AddIfLegal(state, initialPos, color, initialPos.x + knightXChange[i], initialPos.y + knightYChange[i], destinations);
        }

        return destinations;
    }

    private static List<Move> pawnMoves(BoardState state, Vector2Int initialPos, PieceColor color)
    {
        var destinations = new List<Move>(6);
        int forward = color == PieceColor.White ? 1 : -1;
        int startRow = color == PieceColor.White ? 1 : 6;
        int promotionRow = color == PieceColor.White ? 7 : 0;

        int oneRow = initialPos.y + forward;
        if (BoardState.InBounds(initialPos.x, oneRow) && PieceBits.isEmpty(state.whatIsAt(initialPos.x, oneRow)))
        {
            AddPawnMove(destinations, initialPos, new Vector2Int(initialPos.x, oneRow), oneRow == promotionRow, MoveFlags.None);

            if (initialPos.y == startRow)
            {
                int twoRow = initialPos.y + 2 * forward;
                if (BoardState.InBounds(initialPos.x, twoRow) && PieceBits.isEmpty(state.whatIsAt(initialPos.x, twoRow)))
                {
                    destinations.Add(new Move(initialPos, new Vector2Int(initialPos.x, twoRow)));
                }
            }
        }

        AddPawnCapture(state, destinations, initialPos, color, initialPos.x - 1, oneRow, promotionRow);
        AddPawnCapture(state, destinations, initialPos, color, initialPos.x + 1, oneRow, promotionRow);
        AddEnPassantMove(state, destinations, initialPos, color, oneRow);

        return destinations;
    }

    private static void AddPawnCapture(BoardState state, List<Move> destinations, Vector2Int initialPos, PieceColor color, int col, int row, int promotionRow)
    {
        if (!BoardState.InBounds(col, row))
        {
            return;
        }

        int capturedPiece = state.whatIsAt(col, row);
        if (!PieceBits.isEmpty(capturedPiece) && PieceBits.GetColor(capturedPiece) != color && PieceBits.GetType(capturedPiece) != PieceType.King)
        {
            AddPawnMove(destinations, initialPos, new Vector2Int(col, row), row == promotionRow, MoveFlags.Capture);
        }
    }

    private static void AddEnPassantMove(BoardState state, List<Move> destinations, Vector2Int initialPos, PieceColor color, int oneRow)
    {
        if (state.enPassantTarget < 0)
        {
            return;
        }

        int targetCol = state.enPassantTarget % 8;
        int targetRow = state.enPassantTarget / 8;
        if (targetRow != oneRow || Mathf.Abs(targetCol - initialPos.x) != 1)
        {
            return;
        }

        int capturedPawnSquare = BoardState.SquareIndex(targetCol, initialPos.y);
        int capturedPawn = state.board[capturedPawnSquare];
        if (!PieceBits.isEmpty(capturedPawn) &&
            PieceBits.GetColor(capturedPawn) != color &&
            PieceBits.GetType(capturedPawn) == PieceType.Pawn)
        {
            destinations.Add(new Move(initialPos, new Vector2Int(targetCol, targetRow), MoveFlags.Capture | MoveFlags.EnPassant));
        }
    }

    private static void AddIfLegal(BoardState state, Vector2Int initialPos, PieceColor color, int col, int row, List<Move> destinations)
    {
        if (!BoardState.InBounds(col, row))
        {
            return;
        }

        int destination = state.whatIsAt(col, row);
        if (PieceBits.isEmpty(destination))
        {
            destinations.Add(new Move(initialPos, new Vector2Int(col, row)));
            return;
        }

        if (PieceBits.GetColor(destination) != color && PieceBits.GetType(destination) != PieceType.King)
        {
            destinations.Add(new Move(initialPos, new Vector2Int(col, row), MoveFlags.Capture));
        }
    }

    private static bool IsEnemyPiece(BoardState state, int col, int row, PieceColor color, PieceType type)
    {
        if (!BoardState.InBounds(col, row))
        {
            return false;
        }

        int piece = state.whatIsAt(col, row);
        return !PieceBits.isEmpty(piece) && PieceBits.GetColor(piece) == color && PieceBits.GetType(piece) == type;
    }

    private static bool IsAttackedBySlidingPiece(BoardState state, Vector2Int kingPos, PieceColor attackerColor, Vector2Int[] directions, PieceType matchingSlider)
    {
        foreach (var direction in directions)
        {
            int col = kingPos.x + direction.x;
            int row = kingPos.y + direction.y;

            while (BoardState.InBounds(col, row))
            {
                int piece = state.whatIsAt(col, row);
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

    private static void AddPawnMove(List<Move> destinations, Vector2Int from, Vector2Int to, bool isPromotion, MoveFlags flags)
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
