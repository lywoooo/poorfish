using System;
using UnityEngine;

[CreateAssetMenu(fileName = "EngineProfile", menuName = "Poorfish/Engine Profile")]
public class EngineProfile : ScriptableObject
{
    [Header("Identity")]
    public string profileName = "Baseline";

    [Header("Search")]
    [Min(1)]
    public int searchDepth = 6;
    [Min(0f)]
    public float maxThinkTimeSeconds = 1.5f;
    public bool logSearchStats = true;
    public bool useAlphaBetaPruning = true;
    public bool useMoveOrdering = true;
    public bool useTranspositionTable = true;
    public bool useAdaptiveEndgameDepth = true;
    public bool useImmediateCheckmateShortcut = true;
    public bool useOpeningBook = true;

    [Header("Evaluation")]
    public EvaluationWeights evaluationWeights = EvaluationWeights.Default;

    public EngineSettings ToSettings()
    {
        return new EngineSettings
        {
            profileName = string.IsNullOrWhiteSpace(profileName) ? name : profileName,
            searchDepth = searchDepth,
            maxThinkTimeSeconds = maxThinkTimeSeconds,
            logSearchStats = logSearchStats,
            useAlphaBetaPruning = useAlphaBetaPruning,
            useMoveOrdering = useMoveOrdering,
            useTranspositionTable = useTranspositionTable,
            useAdaptiveEndgameDepth = useAdaptiveEndgameDepth,
            useImmediateCheckmateShortcut = useImmediateCheckmateShortcut,
            useOpeningBook = useOpeningBook,
            evaluationWeights = evaluationWeights
        };
    }
}

[Serializable]
public struct EngineSettings
{
    public string profileName;
    public int searchDepth;
    public float maxThinkTimeSeconds;
    public bool logSearchStats;
    public bool useAlphaBetaPruning;
    public bool useMoveOrdering;
    public bool useTranspositionTable;
    public bool useAdaptiveEndgameDepth;
    public bool useImmediateCheckmateShortcut;
    public bool useOpeningBook;
    public EvaluationWeights evaluationWeights;

    public static EngineSettings Default => new EngineSettings
    {
        profileName = "Baseline",
        searchDepth = 6,
        maxThinkTimeSeconds = 1.5f,
        logSearchStats = true,
        useAlphaBetaPruning = true,
        useMoveOrdering = true,
        useTranspositionTable = true,
        useAdaptiveEndgameDepth = true,
        useImmediateCheckmateShortcut = true,
        useOpeningBook = true,
        evaluationWeights = EvaluationWeights.Default
    };

    public string SearchAlgorithmName
    {
        get
        {
            string algorithm = useAlphaBetaPruning ? "MinimaxAlphaBeta" : "Minimax";

            if (useMoveOrdering)
            {
                algorithm += "+MoveOrdering";
            }

            if (useTranspositionTable)
            {
                algorithm += "+TranspositionTable";
            }

            if (useAdaptiveEndgameDepth)
            {
                algorithm += "+AdaptiveDepth";
            }

            return algorithm;
        }
    }

    public string TechniqueSummary
    {
        get
        {
            return "alpha_beta=" + useAlphaBetaPruning
                + ";move_ordering=" + useMoveOrdering
                + ";transposition_table=" + useTranspositionTable
                + ";adaptive_depth=" + useAdaptiveEndgameDepth
                + ";mate_shortcut=" + useImmediateCheckmateShortcut
                + ";opening_book=" + useOpeningBook
                + ";material_weight=" + evaluationWeights.materialWeight
                + ";piece_square_weight=" + evaluationWeights.pieceSquareWeight
                + ";mobility_weight=" + evaluationWeights.mobilityWeight
                + ";development_weight=" + evaluationWeights.developmentWeight
                + ";endgame_mate_weight=" + evaluationWeights.endgameMateWeight;
        }
    }
}
