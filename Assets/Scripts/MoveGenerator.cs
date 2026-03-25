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

    public static List<ChessMove> getLegalMoves(BoardState state, PieceColor color) {
        var legalMoves = new List<ChessMove>(32);

        foreach(var unfilteredMove in getUnfilteredMoves(state, color)) {
            var stateCheck = state.cloneBoard();
            stateCheck.applyMove(unfilteredMove);

            if(!isInCheck(stateCheck, color)) legalMoves.Add(unfilteredMove);
        }

        legalMoves.Sort((a, b) => {
            int scoreA = captureScore(state, a);
            int scoreB = captureScore(state, b);
            return scoreB.CompareTo(scoreA);
        });

        return legalMoves;
    }

    private static int captureScore(BoardState state, ChessMove move) {
        var victim = state.board[move.to.x, move.to.y];

        if (!victim.HasValue) return 0;

        return Evaluator.GetMaterialValue(victim.Value.type);
    }

    public static bool isInCheck(BoardState state, PieceColor color) {
        var kingPos = state.findKing(color);

        if(kingPos.x == -1) return true;

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

    public static bool isCheckmate(BoardState state, PieceColor color) {
        return isInCheck(state, color) && getLegalMoves(state, color).Count == 0;
    }

    public static bool isStalemate(BoardState state, PieceColor color) {
        return !isInCheck(state, color) && getLegalMoves(state, color).Count == 0;
    }

    private static List<ChessMove> getUnfilteredMoves(BoardState state, PieceColor color) {
        var unfilteredMoves = new List<ChessMove>(32);

        for (int col = 0; col < 8; col++) {
            for (int row = 0; row < 8; row++) {
                var currentTile = state.board[col, row];
                if (!currentTile.HasValue || currentTile.Value.color != color) continue;

                var initialPos = new Vector2Int(col, row);
                foreach (var destination in getTargetsForPiece(state, initialPos, currentTile.Value.type, currentTile.Value.color)) {
                    unfilteredMoves.Add(new ChessMove(initialPos, destination));
                }
            }
        }
        return unfilteredMoves;
    }

    private static List<Vector2Int> getTargetsForPiece(BoardState state, Vector2Int initialPos, PieceType type, PieceColor color) {
        switch(type) {
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
                return new List<Vector2Int>(0);
        }
    }

    private static List<Vector2Int> bishopRookMoves(BoardState state, Vector2Int initialPos, PieceColor color, Vector2Int[] directions) {
        var destinations = new List<Vector2Int>();
        foreach (var direction in directions) {
            for(int i = 1; i < 8; i++) {
                int col = initialPos.x + i * direction.x;
                int row = initialPos.y + i * direction.y;

                if(!BoardState.InBounds(col, row)) break;

                var destination = state.board[col, row];

                if(destination == null) {
                    destinations.Add(new Vector2Int(col, row));
                }
                else {
                    if(destination.Value.color != color) destinations.Add(new Vector2Int(col, row));
                    break;
                }
            }
        }

        return destinations;
    }

    private static List<Vector2Int> kingMoves(BoardState state, Vector2Int initialPos, PieceColor color) {
        var destinations = new List<Vector2Int>(8);

        foreach (var direction in bishopDirections) AddIfLegal(state, initialPos, color, initialPos.x + direction.x, initialPos.y + direction.y, destinations);
        foreach (var direction in rookDirections) AddIfLegal(state, initialPos, color, initialPos.x + direction.x, initialPos.y + direction.y, destinations);

        return destinations;
    }

    private static readonly int[] knightXChange = { -1,  1,  2, -2,  2, -2,  1, -1 };
    private static readonly int[] knightYChange = {  2,  2,  1,  1, -1, -1, -2, -2 };

    private static List<Vector2Int> knightMoves(BoardState state, Vector2Int initialPos, PieceColor color) {
        var destinations = new List<Vector2Int>(8);
        for (int i = 0; i < 8; i++)
            AddIfLegal(state, initialPos, color, initialPos.x + knightXChange[i], initialPos.y + knightYChange[i], destinations);
        return destinations;
    }

    private static List<Vector2Int> pawnMoves(BoardState state, Vector2Int initialPos, PieceColor color) {
        var destinations  = new List<Vector2Int>(4);
        int forward  = color == PieceColor.White ? 1 : -1;
        int startRow = color == PieceColor.White ? 1 : 6;

        int oneRow = initialPos.y + forward;
        if (BoardState.InBounds(initialPos.x, oneRow) && state.board[initialPos.x, oneRow] == null) {
            destinations.Add(new Vector2Int(initialPos.x, oneRow));

            if (initialPos.y == startRow) {
                int twoRow = initialPos.y + 2 * forward;
                if (BoardState.InBounds(initialPos.x, twoRow) && state.board[initialPos.x, twoRow] == null) destinations.Add(new Vector2Int(initialPos.x, twoRow));
            }
        }

        int leftCaptureCol = initialPos.x - 1;
        if (BoardState.InBounds(leftCaptureCol, oneRow))
        {
            var leftCapture = state.board[leftCaptureCol, oneRow];
            if (leftCapture.HasValue && leftCapture.Value.color != color) destinations.Add(new Vector2Int(leftCaptureCol, oneRow));
        }

        int rightCaptureCol = initialPos.x + 1;
        if (BoardState.InBounds(rightCaptureCol, oneRow))
        {
            var rightCapture = state.board[rightCaptureCol, oneRow];
            if (rightCapture.HasValue && rightCapture.Value.color != color) destinations.Add(new Vector2Int(rightCaptureCol, oneRow));
        }

        return destinations;
    }

    private static void AddIfLegal(BoardState state, Vector2Int initialPos, PieceColor color, int col, int row, List<Vector2Int> destinations) {
        if (!BoardState.InBounds(col, row)) return;

        var destination = state.board[col, row];

        if (destination == null || destination.Value.color != color) destinations.Add(new Vector2Int(col, row));
    }

    private static bool IsEnemyPiece(BoardState state, int col, int row, PieceColor color, PieceType type)
    {
        if (!BoardState.InBounds(col, row))
        {
            return false;
        }

        var piece = state.board[col, row];
        return piece.HasValue && piece.Value.color == color && piece.Value.type == type;
    }

    private static bool IsAttackedBySlidingPiece(BoardState state, Vector2Int kingPos, PieceColor attackerColor, Vector2Int[] directions, PieceType matchingSlider)
    {
        foreach (var direction in directions)
        {
            int col = kingPos.x + direction.x;
            int row = kingPos.y + direction.y;

            while (BoardState.InBounds(col, row))
            {
                var piece = state.board[col, row];
                if (piece.HasValue)
                {
                    if (piece.Value.color == attackerColor &&
                        (piece.Value.type == matchingSlider || piece.Value.type == PieceType.Queen))
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
}
