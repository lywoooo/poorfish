using UnityEngine;

public static class FenEngineTools
{
    public static bool TryFindBestMove(string fen, EngineSettings settings, out SearchResult result)
    {
        result = default;

        if (!FEN.TryLoadFen(fen, out BoardState state))
        {
            return false;
        }

        IEvaluator evaluator = new ConfigurableEvaluator(settings.evaluationWeights, settings.profileName + "_FenEvaluator");
        ISearchEngine engine = new MinimaxAB(evaluator);
        result = engine.FindBestMove(state, state.currentTurn, settings);
        return result.hasMove;
    }

    public static bool TryPerft(string fen, int depth, out long nodes)
    {
        nodes = 0;

        if (depth < 0 || !FEN.TryLoadFen(fen, out BoardState state))
        {
            return false;
        }

        nodes = Perft(state, depth);
        return true;
    }

    public static long Perft(BoardState state, int depth)
    {
        if (depth == 0)
        {
            return 1;
        }

        var legalMoves = MoveGenerator.getLegalMoves(state, state.currentTurn);
        if (depth == 1)
        {
            return legalMoves.Count;
        }

        long nodes = 0;
        foreach (Move move in legalMoves)
        {
            BoardState.MoveUndo undo = state.MakeMove(move);
            state.switchTurn();
            nodes += Perft(state, depth - 1);
            state.UnmakeMove(move, undo);
        }

        return nodes;
    }

    public static void LogSearch(string fen, EngineSettings settings)
    {
        if (!TryFindBestMove(fen, settings, out SearchResult result))
        {
            Debug.LogError("Invalid FEN or no legal moves: " + fen);
            return;
        }

        Debug.Log(
            "FEN search best move "
            + result.bestMove.FromVector + " to " + result.bestMove.ToVector
            + ", score=" + result.bestScore
            + ", depth=" + result.stats.completedDepth
            + ", nodes=" + result.stats.nodesVisited
            + ", evals=" + result.stats.leafEvaluations
            + ", cutoffs=" + result.stats.alphaBetaCutoffs
            + ", elapsedMs=" + result.stats.elapsedMilliseconds.ToString("F1"));
    }
}
