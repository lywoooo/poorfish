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
        BoardState.BoardPiece? victim = state.board[move.to.x, move.to.y];
        BoardState.BoardPiece? movingPiece = state.board[move.from.x, move.from.y];

        if (move.isEnPassant && movingPiece.HasValue)
        {
            int capturedPawnRow = move.to.y + (movingPiece.Value.color == PieceColor.White ? -1 : 1);
            victim = state.board[move.to.x, capturedPawnRow];
        }

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
                unfilteredMoves.AddRange(getMovesForPiece(state, initialPos, currentTile.Value.type, currentTile.Value.color));
            }
        }
        return unfilteredMoves;
    }

    private static List<ChessMove> getMovesForPiece(BoardState state, Vector2Int initialPos, PieceType type, PieceColor color) {
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
                return new List<ChessMove>(0);
        }
    }

    private static List<ChessMove> bishopRookMoves(BoardState state, Vector2Int initialPos, PieceColor color, Vector2Int[] directions) {
        var destinations = new List<ChessMove>();
        foreach (var direction in directions) {
            for(int i = 1; i < 8; i++) {
                int col = initialPos.x + i * direction.x;
                int row = initialPos.y + i * direction.y;

                if(!BoardState.InBounds(col, row)) break;

                var destination = state.board[col, row];

                if(destination == null) {
                    destinations.Add(new ChessMove(initialPos, new Vector2Int(col, row)));
                }
                else {
                    if(destination.Value.color != color && destination.Value.type != PieceType.King)
                    {
                        destinations.Add(new ChessMove(initialPos, new Vector2Int(col, row)));
                    }
                    break;
                }
            }
        }

        return destinations;
    }

    private static List<ChessMove> kingMoves(BoardState state, Vector2Int initialPos, PieceColor color) {
        var destinations = new List<ChessMove>(10);

        foreach (var direction in bishopDirections) AddIfLegal(state, initialPos, color, initialPos.x + direction.x, initialPos.y + direction.y, destinations);
        foreach (var direction in rookDirections) AddIfLegal(state, initialPos, color, initialPos.x + direction.x, initialPos.y + direction.y, destinations);
        AddCastlingMoves(state, initialPos, color, destinations);

        return destinations;
    }

    private static readonly int[] knightXChange = { -1,  1,  2, -2,  2, -2,  1, -1 };
    private static readonly int[] knightYChange = {  2,  2,  1,  1, -1, -1, -2, -2 };

    private static List<ChessMove> knightMoves(BoardState state, Vector2Int initialPos, PieceColor color) {
        var destinations = new List<ChessMove>(8);
        for (int i = 0; i < 8; i++)
            AddIfLegal(state, initialPos, color, initialPos.x + knightXChange[i], initialPos.y + knightYChange[i], destinations);
        return destinations;
    }

    private static List<ChessMove> pawnMoves(BoardState state, Vector2Int initialPos, PieceColor color) {
        var destinations  = new List<ChessMove>(6);
        int forward  = color == PieceColor.White ? 1 : -1;
        int startRow = color == PieceColor.White ? 1 : 6;
        int promotionRow = color == PieceColor.White ? 7 : 0;

        int oneRow = initialPos.y + forward;
        if (BoardState.InBounds(initialPos.x, oneRow) && state.board[initialPos.x, oneRow] == null) {
            AddPawnMove(destinations, initialPos, new Vector2Int(initialPos.x, oneRow), oneRow == promotionRow);

            if (initialPos.y == startRow) {
                int twoRow = initialPos.y + 2 * forward;
                if (BoardState.InBounds(initialPos.x, twoRow) && state.board[initialPos.x, twoRow] == null) destinations.Add(new ChessMove(initialPos, new Vector2Int(initialPos.x, twoRow)));
            }
        }

        int leftCaptureCol = initialPos.x - 1;
        if (BoardState.InBounds(leftCaptureCol, oneRow))
        {
            var leftCapture = state.board[leftCaptureCol, oneRow];
            if (leftCapture.HasValue && leftCapture.Value.color != color && leftCapture.Value.type != PieceType.King)
            {
                AddPawnMove(destinations, initialPos, new Vector2Int(leftCaptureCol, oneRow), oneRow == promotionRow);
            }
        }

        int rightCaptureCol = initialPos.x + 1;
        if (BoardState.InBounds(rightCaptureCol, oneRow))
        {
            var rightCapture = state.board[rightCaptureCol, oneRow];
            if (rightCapture.HasValue && rightCapture.Value.color != color && rightCapture.Value.type != PieceType.King)
            {
                AddPawnMove(destinations, initialPos, new Vector2Int(rightCaptureCol, oneRow), oneRow == promotionRow);
            }
        }

        if (state.enPassantTarget.HasValue && state.enPassantTarget.Value.y == oneRow)
        {
            int fileDifference = Mathf.Abs(state.enPassantTarget.Value.x - initialPos.x);
            if (fileDifference == 1)
            {
                Vector2Int capturedPawnPosition = new Vector2Int(state.enPassantTarget.Value.x, initialPos.y);
                var capturedPawn = state.board[capturedPawnPosition.x, capturedPawnPosition.y];
                if (capturedPawn.HasValue &&
                    capturedPawn.Value.color != color &&
                    capturedPawn.Value.type == PieceType.Pawn)
                {
                    destinations.Add(new ChessMove(initialPos, state.enPassantTarget.Value, isEnPassant: true));
                }
            }
        }

        return destinations;
    }

    private static void AddIfLegal(BoardState state, Vector2Int initialPos, PieceColor color, int col, int row, List<ChessMove> destinations) {
        if (!BoardState.InBounds(col, row)) return;

        var destination = state.board[col, row];

        if (!destination.HasValue)
        {
            destinations.Add(new ChessMove(initialPos, new Vector2Int(col, row)));
            return;
        }

        if (destination.Value.color != color && destination.Value.type != PieceType.King)
        {
            destinations.Add(new ChessMove(initialPos, new Vector2Int(col, row)));
        }
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

    private static void AddCastlingMoves(BoardState state, Vector2Int kingPosition, PieceColor color, List<ChessMove> destinations)
    {
        bool kingMoved = color == PieceColor.White ? state.whiteKingMoved : state.blackKingMoved;
        if (kingMoved || isInCheck(state, color))
        {
            return;
        }

        int backRank = color == PieceColor.White ? 0 : 7;
        bool kingsideRookMoved = color == PieceColor.White ? state.whiteKingsideRookMoved : state.blackKingsideRookMoved;
        bool queensideRookMoved = color == PieceColor.White ? state.whiteQueensideRookMoved : state.blackQueensideRookMoved;

        TryAddCastleMove(state, color, kingPosition, backRank, kingside: true, rookMoved: kingsideRookMoved, destinations);
        TryAddCastleMove(state, color, kingPosition, backRank, kingside: false, rookMoved: queensideRookMoved, destinations);
    }

    private static void TryAddCastleMove(BoardState state, PieceColor color, Vector2Int kingPosition, int backRank, bool kingside, bool rookMoved, List<ChessMove> destinations)
    {
        if (rookMoved)
        {
            return;
        }

        int rookCol = kingside ? 7 : 0;
        int kingTargetCol = kingside ? 6 : 2;
        int rookTargetCol = kingside ? 5 : 3;
        int step = kingside ? 1 : -1;
        var rook = state.board[rookCol, backRank];

        if (!rook.HasValue || rook.Value.color != color || rook.Value.type != PieceType.Rook)
        {
            return;
        }

        for (int col = kingPosition.x + step; col != rookCol; col += step)
        {
            if (state.board[col, backRank].HasValue)
            {
                return;
            }
        }

        for (int col = kingPosition.x + step; col != kingTargetCol + step; col += step)
        {
            var intermediate = state.cloneBoard();
            intermediate.applyMove(new ChessMove(kingPosition, new Vector2Int(col, backRank)));
            if (isInCheck(intermediate, color))
            {
                return;
            }
        }

        destinations.Add(new ChessMove(
            kingPosition,
            new Vector2Int(kingTargetCol, backRank),
            isCastling: true,
            rookFrom: new Vector2Int(rookCol, backRank),
            rookTo: new Vector2Int(rookTargetCol, backRank)));
    }

    private static void AddPawnMove(List<ChessMove> destinations, Vector2Int from, Vector2Int to, bool isPromotion)
    {
        if (isPromotion)
        {
            destinations.Add(new ChessMove(from, to, promotionType: PieceType.Queen));
            destinations.Add(new ChessMove(from, to, promotionType: PieceType.Rook));
            destinations.Add(new ChessMove(from, to, promotionType: PieceType.Bishop));
            destinations.Add(new ChessMove(from, to, promotionType: PieceType.Knight));
            return;
        }

        destinations.Add(new ChessMove(from, to));
    }
}
