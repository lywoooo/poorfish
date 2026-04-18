using System;
using System.Collections.Generic;
using UnityEngine;

public interface IEvaluator
{
    string Name { get; }
    int Evaluate(BoardState state);
}

[Serializable]
public struct EvaluationWeights
{
    public int materialWeight;
    public int pieceSquareWeight;
    public int mobilityWeight;
    public int drawPenalty;
    public int repetitionPenalty;
    public int endgameMateWeight;
    public int kingEdgeWeight;
    public int kingDistanceWeight;

    public static EvaluationWeights Default => new EvaluationWeights
    {
        materialWeight = 100,
        pieceSquareWeight = 100,
        mobilityWeight = 0,
        drawPenalty = 60,
        repetitionPenalty = 45,
        endgameMateWeight = 100,
        kingEdgeWeight = 80,
        kingDistanceWeight = 20
    };
}

public sealed class ConfigurableEvaluator : IEvaluator
{
    private readonly EvaluationWeights weights;
    private readonly string name;
    private readonly List<Move> whiteMobilityMoves = new List<Move>(64);
    private readonly List<Move> whiteMobilityCandidates = new List<Move>(64);
    private readonly List<Move> blackMobilityMoves = new List<Move>(64);
    private readonly List<Move> blackMobilityCandidates = new List<Move>(64);

    public ConfigurableEvaluator(EvaluationWeights weights, string name = "ConfigurableEvaluator")
    {
        this.weights = weights;
        this.name = name;
    }

    public string Name => name;

    public int Evaluate(BoardState state)
    {
        bool endgame = Evaluator.IsEndgame(state);
        int materialScore = 0;
        int pieceSquareScore = 0;

        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                int piece = state.whatIsAt(col, row);
                if (PieceBits.isEmpty(piece))
                {
                    continue;
                }

                PieceType type = PieceBits.GetType(piece);
                PieceColor color = PieceBits.GetColor(piece);
                int signedColor = color == PieceColor.White ? 1 : -1;
                materialScore += signedColor * Evaluator.GetMaterialValue(type);
                pieceSquareScore += signedColor * PieceSquareTables.GetPST(type, color, col, row, endgame);
            }
        }

        int mobilityScore = 0;
        if (weights.mobilityWeight != 0)
        {
            MoveGenerator.GetLegalMoves(state, PieceColor.White, whiteMobilityMoves, whiteMobilityCandidates);
            MoveGenerator.GetLegalMoves(state, PieceColor.Black, blackMobilityMoves, blackMobilityCandidates);
            mobilityScore = whiteMobilityMoves.Count - blackMobilityMoves.Count;
        }

        int endgameScore = 0;
        if (weights.endgameMateWeight != 0 && endgame)
        {
            endgameScore = Endgame.EvaluateMatePressure(
                state,
                weights.kingEdgeWeight,
                weights.kingDistanceWeight);
        }

        return Scale(materialScore, weights.materialWeight)
            + Scale(pieceSquareScore, weights.pieceSquareWeight)
            + Scale(mobilityScore, weights.mobilityWeight)
            + Scale(endgameScore, weights.endgameMateWeight);
    }

    private static int Scale(int value, int weight)
    {
        return Mathf.RoundToInt(value * (weight / 100f));
    }
}

public static class Evaluator
{
    // source chessprogramming wiki SEF for values
    private const int ValPawn   = 100;
    private const int ValKnight = 320;
    private const int ValBishop = 330;
    private const int ValRook   = 500;
    private const int ValQueen  = 900;
    private const int ValKing   = 20000;

    private const int EndgameThreshold = 1500;

    private static readonly IEvaluator DefaultEvaluator = new ConfigurableEvaluator(EvaluationWeights.Default, "DefaultEvaluator");

    public static bool IsEndgame(BoardState state)
    {
        int totalMaterial = 0;

        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                var piece = state.whatIsAt(col, row);
                if (PieceBits.isEmpty(piece) || PieceBits.GetType(piece) == PieceType.King) continue;

                totalMaterial += GetMaterialValue(PieceBits.GetType(piece));
            }
        }

        return totalMaterial < EndgameThreshold;
    }

    public static int Evaluate(BoardState state)
    {
        return DefaultEvaluator.Evaluate(state);
    }

    public static int GetMaterialValue(PieceType type)
    {
        return type switch
        {
            PieceType.Pawn   => ValPawn,
            PieceType.Knight => ValKnight,
            PieceType.Bishop => ValBishop,
            PieceType.Rook   => ValRook,
            PieceType.Queen  => ValQueen,
            PieceType.King   => ValKing,
            _                => 0
        };
    }
}
