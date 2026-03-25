using System.Collections.Generic;
using UnityEngine;

public static class MinimaxAB
{
    private struct TranspositionEntry
    {
        public int depth;
        public int score;
    }

    public const int POS_INF = 999999;
    public const int NEG_INF = -999999;
    private const int MateScore = 900000;
    private static readonly Dictionary<ulong, TranspositionEntry> transpositionTable = new Dictionary<ulong, TranspositionEntry>(32768);
    private static float searchDeadline;
    private static bool hasDeadline;
    private static bool timedOut;

    public static void BeginTimedSearch(float maxThinkTimeSeconds)
    {
        timedOut = false;

        if (maxThinkTimeSeconds <= 0f)
        {
            hasDeadline = false;
            return;
        }

        hasDeadline = true;
        searchDeadline = Time.realtimeSinceStartup + maxThinkTimeSeconds;
    }

    public static void EndTimedSearch()
    {
        hasDeadline = false;
    }

    public static bool TimedOut()
    {
        return timedOut;
    }

    public static int search(BoardState state, int depth, int alpha, int beta)
    {
        if (hasDeadline && Time.realtimeSinceStartup >= searchDeadline)
        {
            timedOut = true;
            return Evaluator.Evaluate(state);
        }

        ulong hash = state.ComputeHash();
        if (transpositionTable.TryGetValue(hash, out TranspositionEntry cached) && cached.depth >= depth)
        {
            return cached.score;
        }

        var legalMoves = MoveGenerator.getLegalMoves(state, state.currentTurn);

        if (legalMoves.Count == 0)
        {
            int terminalScore;
            if (MoveGenerator.isInCheck(state, state.currentTurn))
            {
                terminalScore = state.currentTurn == PieceColor.White
                    ? -MateScore - depth
                    : MateScore + depth;
            }
            else
            {
                terminalScore = 0;
            }

            transpositionTable[hash] = new TranspositionEntry { depth = depth, score = terminalScore };
            return terminalScore;
        }

        if (depth == 0)
        {
            int evaluation = Evaluator.Evaluate(state);
            transpositionTable[hash] = new TranspositionEntry { depth = depth, score = evaluation };
            return evaluation;
        }

        bool maximizing = state.currentTurn == PieceColor.White;
        int bestScore = maximizing ? NEG_INF : POS_INF;

        if (maximizing)
        {
            foreach (var move in legalMoves)
            {
                var newState = state.cloneBoard();
                newState.applyMove(move);
                newState.switchTurn();
                int eval = search(newState, depth - 1, alpha, beta);
                if (timedOut)
                {
                    return Evaluator.Evaluate(state);
                }

                if (eval > bestScore)
                {
                    bestScore = eval;
                }

                if (eval > alpha)
                {
                    alpha = eval;
                }
                if (beta <= alpha)
                {
                    break;
                }
            }
        }
        else
        {
            foreach (var move in legalMoves)
            {
                var newState = state.cloneBoard();
                newState.applyMove(move);
                newState.switchTurn();
                int eval = search(newState, depth - 1, alpha, beta);
                if (timedOut)
                {
                    return Evaluator.Evaluate(state);
                }

                if (eval < bestScore)
                {
                    bestScore = eval;
                }

                if (eval < beta)
                {
                    beta = eval;
                }
                if (beta <= alpha)
                {
                    break;
                }
            }
        }

        transpositionTable[hash] = new TranspositionEntry { depth = depth, score = bestScore };
        return bestScore;
    }
}
