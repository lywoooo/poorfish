using System.Collections.Generic;
using UnityEngine;

public static class Endgame
{
    private const int WinningThreshold = 500;
    private const int LoneKingMobilityWeight = 70;
    private const int BoxConfinementWeight = 45;
    private const int SafeMajorPieceBonus = 80;
    private const int HangingMajorPiecePenalty = 350;
    private static readonly List<Move> KingLegalMoveBuffer = new List<Move>(8);
    private static readonly List<Move> KingCandidateMoveBuffer = new List<Move>(8);

    public static int EvaluateMatePressure(
        BoardState state,
        int kingEdgeWeight,
        int kingDistanceWeight)
    {
        int whiteMaterial = GetNonKingMaterial(state, PieceColor.White);
        int blackMaterial = GetNonKingMaterial(state, PieceColor.Black);
        int materialDifference = whiteMaterial - blackMaterial;

        if (Mathf.Abs(materialDifference) < WinningThreshold)
        {
            return 0;
        }

        PieceColor winningSide = materialDifference > 0
            ? PieceColor.White
            : PieceColor.Black;
        PieceColor losingSide = winningSide == PieceColor.White
            ? PieceColor.Black
            : PieceColor.White;

        if (!HasBasicMatingMaterial(state, winningSide))
        {
            return 0;
        }

        if (GetNonKingMaterial(state, losingSide) > 0)
        {
            return 0;
        }

        Vector2Int winningKing = state.findKing(winningSide);
        Vector2Int losingKing = state.findKing(losingSide);
        if (winningKing.x < 0 || losingKing.x < 0)
        {
            return 0;
        }

        int edgeScore = KingEdgeScore(losingKing) * kingEdgeWeight;
        int distanceScore = KingDistanceScore(winningKing, losingKing) * kingDistanceWeight;
        int score = edgeScore + distanceScore;

        if (TryGetMajorMateInfo(state, winningSide, losingKing, out MajorMateInfo majorMate))
        {
            score += EvaluateMajorPieceMatePressure(
                state,
                losingSide,
                winningKing,
                losingKing,
                majorMate);
        }

        return winningSide == PieceColor.White ? score : -score;
    }

    private readonly struct MajorMateInfo
    {
        public readonly PieceType type;
        public readonly Vector2Int square;

        public MajorMateInfo(PieceType type, Vector2Int square)
        {
            this.type = type;
            this.square = square;
        }
    }

    private static int EvaluateMajorPieceMatePressure(
        BoardState state,
        PieceColor losingSide,
        Vector2Int winningKing,
        Vector2Int losingKing,
        MajorMateInfo majorMate)
    {
        int score = 0;
        int losingKingMoves = CountLegalKingMoves(state, losingSide, losingKing);
        score += (8 - losingKingMoves) * LoneKingMobilityWeight;
        score += BoxConfinementScore(majorMate, losingKing) * BoxConfinementWeight;
        score += MajorPieceSafetyScore(majorMate.square, winningKing, losingKing);

        if (MoveGenerator.isInCheck(state, losingSide) && losingKingMoves > 0)
        {
            score -= LoneKingMobilityWeight;
        }

        return score;
    }

    private static bool TryGetMajorMateInfo(
        BoardState state,
        PieceColor winningSide,
        Vector2Int losingKing,
        out MajorMateInfo majorMate)
    {
        majorMate = default;
        int bestScore = int.MinValue;

        for (int square = 0; square < state.board.Length; square++)
        {
            int piece = state.board[square];
            if (PieceBits.isEmpty(piece) || PieceBits.GetColor(piece) != winningSide)
            {
                continue;
            }

            PieceType type = PieceBits.GetType(piece);
            if (type != PieceType.Queen && type != PieceType.Rook)
            {
                continue;
            }

            MajorMateInfo candidate = new MajorMateInfo(type, new Vector2Int(square % 8, square / 8));
            int candidateScore = MajorMateCandidateScore(candidate, losingKing);
            if (candidateScore > bestScore)
            {
                bestScore = candidateScore;
                majorMate = candidate;
            }
        }

        return bestScore != int.MinValue;
    }

    private static int MajorMateCandidateScore(MajorMateInfo majorMate, Vector2Int losingKing)
    {
        int queenPreference = majorMate.type == PieceType.Queen ? 100 : 0;
        return queenPreference + BoxConfinementScore(majorMate, losingKing);
    }

    private static int CountLegalKingMoves(BoardState state, PieceColor color, Vector2Int kingSquare)
    {
        MoveGenerator.GetLegalMovesFromSquare(
            state,
            color,
            BoardState.SquareIndex(kingSquare),
            KingLegalMoveBuffer,
            KingCandidateMoveBuffer);
        return KingLegalMoveBuffer.Count;
    }

    private static int BoxConfinementScore(MajorMateInfo majorMate, Vector2Int losingKing)
    {
        int fileCutScore = 0;
        if (majorMate.square.x != losingKing.x)
        {
            int boxWidth = losingKing.x < majorMate.square.x
                ? majorMate.square.x
                : 7 - majorMate.square.x;
            fileCutScore = 7 - boxWidth;
        }

        int rankCutScore = 0;
        if (majorMate.square.y != losingKing.y)
        {
            int boxHeight = losingKing.y < majorMate.square.y
                ? majorMate.square.y
                : 7 - majorMate.square.y;
            rankCutScore = 7 - boxHeight;
        }

        int strongestCut = Mathf.Max(fileCutScore, rankCutScore);
        if (majorMate.type == PieceType.Queen)
        {
            int secondCut = Mathf.Min(fileCutScore, rankCutScore);
            return strongestCut + (secondCut / 2);
        }

        return strongestCut;
    }

    private static int MajorPieceSafetyScore(Vector2Int majorSquare, Vector2Int winningKing, Vector2Int losingKing)
    {
        if (ChebyshevDistance(majorSquare, losingKing) > 1)
        {
            return SafeMajorPieceBonus;
        }

        return ChebyshevDistance(majorSquare, winningKing) <= 1
            ? SafeMajorPieceBonus
            : -HangingMajorPiecePenalty;
    }

    private static int GetNonKingMaterial(BoardState state, PieceColor color)
    {
        int material = 0;

        for (int square = 0; square < state.board.Length; square++)
        {
            int piece = state.board[square];
            if (PieceBits.isEmpty(piece) || PieceBits.GetColor(piece) != color)
            {
                continue;
            }

            PieceType type = PieceBits.GetType(piece);
            if (type == PieceType.King)
            {
                continue;
            }

            material += Evaluator.GetMaterialValue(type);
        }

        return material;
    }

    private static bool HasBasicMatingMaterial(BoardState state, PieceColor color)
    {
        for (int square = 0; square < state.board.Length; square++)
        {
            int piece = state.board[square];
            if (PieceBits.isEmpty(piece) || PieceBits.GetColor(piece) != color)
            {
                continue;
            }

            PieceType type = PieceBits.GetType(piece);
            if (type == PieceType.Queen || type == PieceType.Rook)
            {
                return true;
            }
        }

        return false;
    }

    private static int KingEdgeScore(Vector2Int kingSquare)
    {
        int distanceFromCenterFile = Mathf.Min(
            Mathf.Abs(kingSquare.x - 3),
            Mathf.Abs(kingSquare.x - 4));
        int distanceFromCenterRank = Mathf.Min(
            Mathf.Abs(kingSquare.y - 3),
            Mathf.Abs(kingSquare.y - 4));

        return distanceFromCenterFile + distanceFromCenterRank;
    }

    private static int KingDistanceScore(Vector2Int winningKing, Vector2Int losingKing)
    {
        int fileDistance = Mathf.Abs(winningKing.x - losingKing.x);
        int rankDistance = Mathf.Abs(winningKing.y - losingKing.y);
        int kingDistance = fileDistance + rankDistance;

        return 14 - kingDistance;
    }

    private static int ChebyshevDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }
}
