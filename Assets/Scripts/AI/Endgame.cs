using System.Collections.Generic;
using UnityEngine;

public static class Endgame
{
    private const int WinningThreshold = 500;
    private const int MaxDefenderMaterialForMatePressure = 400;
    private const int EdgeFirstWeight = 220;
    private const int CornerWeight = 90;
    private const int BoxWeight = 120;
    private const int SupportWeight = 45;
    private const int MobilityWeight = 80;
    private const int SafeMajorPieceBonus = 120;
    private const int HangingMajorPiecePenalty = 600;
    private const int BadCheckPenalty = 300;
    private const int StrongCheckBonus = 250;
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
        int losingMaterial = winningSide == PieceColor.White
            ? blackMaterial
            : whiteMaterial;

        if (!HasBasicMatingMaterial(state, winningSide))
        {
            return 0;
        }

        if (losingMaterial > MaxDefenderMaterialForMatePressure)
        {
            return 0;
        }

        Vector2Int winningKing = state.findKing(winningSide);
        Vector2Int losingKing = state.findKing(losingSide);
        if (winningKing.x < 0 || losingKing.x < 0)
        {
            return 0;
        }

        Vector2Int targetCorner = TargetCorner(winningKing, losingKing);
        if (!TryGetMajorMateInfo(state, winningSide, losingKing, targetCorner, out MajorMateInfo majorMate))
        {
            return 0;
        }

        int score = EvaluateMajorPieceMatePressure(
            state,
            losingSide,
            winningKing,
            losingKing,
            majorMate,
            targetCorner,
            kingEdgeWeight,
            kingDistanceWeight);

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
        MajorMateInfo majorMate,
        Vector2Int targetCorner,
        int kingEdgeWeight,
        int kingDistanceWeight)
    {
        int losingKingMoves = CountLegalKingMoves(state, losingSide, losingKing);
        int score = 0;

        score += KingEdgeScore(losingKing) * (kingEdgeWeight + EdgeFirstWeight);
        score += CornerScore(losingKing, targetCorner) * CornerWeight;
        score += KingDistanceScore(winningKing, losingKing) * kingDistanceWeight;
        score += BoxConfinementScore(majorMate, losingKing, targetCorner) * BoxWeight;
        score += KingSupportScore(winningKing, losingKing) * SupportWeight;
        score += (8 - losingKingMoves) * MobilityWeight;
        score += MajorPieceSafetyScore(majorMate.square, winningKing, losingKing);

        if (MoveGenerator.isInCheck(state, losingSide))
        {
            score += losingKingMoves <= 1 ? StrongCheckBonus : -BadCheckPenalty;
        }

        return score;
    }

    private static bool TryGetMajorMateInfo(
        BoardState state,
        PieceColor winningSide,
        Vector2Int losingKing,
        Vector2Int targetCorner,
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
            int candidateScore = MajorMateCandidateScore(candidate, losingKing, targetCorner);
            if (candidateScore > bestScore)
            {
                bestScore = candidateScore;
                majorMate = candidate;
            }
        }

        return bestScore != int.MinValue;
    }

    private static int MajorMateCandidateScore(MajorMateInfo majorMate, Vector2Int losingKing, Vector2Int targetCorner)
    {
        int queenPreference = majorMate.type == PieceType.Queen ? 100 : 0;
        return queenPreference + BoxConfinementScore(majorMate, losingKing, targetCorner);
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

    private static int BoxConfinementScore(MajorMateInfo majorMate, Vector2Int losingKing, Vector2Int targetCorner)
    {
        int fileCutScore = CutTowardCornerScore(majorMate.square.x, losingKing.x, targetCorner.x);
        int rankCutScore = CutTowardCornerScore(majorMate.square.y, losingKing.y, targetCorner.y);

        int strongestCut = Mathf.Max(fileCutScore, rankCutScore);
        if (majorMate.type == PieceType.Queen)
        {
            int secondCut = Mathf.Min(fileCutScore, rankCutScore);
            return strongestCut + (secondCut / 2);
        }

        return strongestCut;
    }

    private static int CutTowardCornerScore(int majorLine, int kingLine, int cornerLine)
    {
        if (cornerLine == 0)
        {
            return majorLine > kingLine ? 7 - majorLine : 0;
        }

        return majorLine < kingLine ? majorLine : 0;
    }

    private static Vector2Int TargetCorner(Vector2Int winningKing, Vector2Int losingKing)
    {
        Vector2Int bestCorner = new Vector2Int(0, 0);
        int bestScore = int.MaxValue;

        for (int file = 0; file <= 7; file += 7)
        {
            for (int rank = 0; rank <= 7; rank += 7)
            {
                Vector2Int corner = new Vector2Int(file, rank);
                int score = ManhattanDistance(losingKing, corner) * 4
                    + ManhattanDistance(winningKing, corner);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestCorner = corner;
                }
            }
        }

        return bestCorner;
    }

    private static int CornerScore(Vector2Int kingSquare, Vector2Int targetCorner)
    {
        int distance = Mathf.Abs(kingSquare.x - targetCorner.x) + Mathf.Abs(kingSquare.y - targetCorner.y);
        return 14 - distance;
    }

    private static int KingSupportScore(Vector2Int winningKing, Vector2Int losingKing)
    {
        return 7 - ChebyshevDistance(winningKing, losingKing);
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
        return 14 - ManhattanDistance(winningKing, losingKing);
    }

    private static int ManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static int ChebyshevDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }
}
