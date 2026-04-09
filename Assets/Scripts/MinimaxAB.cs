using System.Collections.Generic;
using UnityEngine;

public class MinimaxAB : ISearchEngine
{
    private readonly struct SearchBias
    {
        public readonly int drawPenalty;
        public readonly int repetitionPenalty;

        public SearchBias(int drawPenalty, int repetitionPenalty)
        {
            this.drawPenalty = Mathf.Max(0, drawPenalty);
            this.repetitionPenalty = Mathf.Max(0, repetitionPenalty);
        }
    }

    private enum EntryType
    {
        Exact,
        LowerBound,
        UpperBound
    }

    private struct TranspositionEntry
    {
        public int depth;
        public int score;
        public EntryType type;
    }

    public const int POS_INF = 999999;
    public const int NEG_INF = -999999;
    private const int MateScore = 900000;
    private readonly Dictionary<ulong, TranspositionEntry> transpositionTable = new Dictionary<ulong, TranspositionEntry>(32768);
    private readonly IEvaluator evaluator;
    private readonly Dictionary<ulong, int> repetitionCounts = new Dictionary<ulong, int>(128);
    private float searchDeadline;
    private bool hasDeadline;
    private bool timedOut;
    private int nodesVisited;
    private int leafEvaluations;
    private int transpositionHits;
    private int alphaBetaCutoffs;
    private float searchStartTime;
    private SearchBias searchBias;

    public MinimaxAB(IEvaluator evaluator)
    {
        this.evaluator = evaluator;
    }

    public SearchResult FindBestMove(BoardState state, PieceColor aiColor, EngineSettings settings)
    {
        var legalMoves = MoveGenerator.getLegalMoves(state, aiColor);
        if (legalMoves.Count == 0)
        {
            return new SearchResult(default, aiColor == PieceColor.Black ? POS_INF : NEG_INF, false, new SearchStats(0, 0, 0, 0, 0, 0f));
        }

        BeginTimedSearch(settings.maxThinkTimeSeconds);
        searchBias = new SearchBias(
            settings.evaluationWeights.drawPenalty,
            settings.evaluationWeights.repetitionPenalty);
        repetitionCounts.Clear();

        ChessMove bestMove = legalMoves[0];
        int bestScore = evaluator.Evaluate(state);
        int completedDepth = 0;

        for (int depth = 1; depth <= settings.searchDepth; depth++)
        {
            ChessMove depthBestMove = default;
            int depthBestScore = aiColor == PieceColor.Black ? POS_INF : NEG_INF;
            bool hasDepthMove = false;

            foreach (ChessMove move in legalMoves)
            {
                var prospective = state.cloneBoard();
                prospective.applyMove(move);
                prospective.switchTurn();

                int score = Search(prospective, depth - 1, NEG_INF, POS_INF);
                if (timedOut)
                {
                    break;
                }

                bool scoreIsBetter = aiColor == PieceColor.Black ? score < depthBestScore : score > depthBestScore;
                if (scoreIsBetter || !hasDepthMove)
                {
                    depthBestScore = score;
                    depthBestMove = move;
                    hasDepthMove = true;
                }
            }

            if (timedOut)
            {
                break;
            }

            completedDepth = depth;
            bestMove = depthBestMove;
            bestScore = depthBestScore;
        }

        EndTimedSearch();
        return new SearchResult(bestMove, bestScore, true, BuildStats(completedDepth));
    }

    private void BeginTimedSearch(float maxThinkTimeSeconds)
    {
        timedOut = false;
        nodesVisited = 0;
        leafEvaluations = 0;
        transpositionHits = 0;
        alphaBetaCutoffs = 0;
        searchStartTime = Time.realtimeSinceStartup;

        if (maxThinkTimeSeconds <= 0f)
        {
            hasDeadline = false;
            return;
        }

        hasDeadline = true;
        searchDeadline = Time.realtimeSinceStartup + maxThinkTimeSeconds;
    }

    private void EndTimedSearch()
    {
        hasDeadline = false;
    }

    private SearchStats BuildStats(int completedDepth)
    {
        return new SearchStats(
            nodesVisited,
            leafEvaluations,
            transpositionHits,
            alphaBetaCutoffs,
            completedDepth,
            (Time.realtimeSinceStartup - searchStartTime) * 1000f);
    }

    private int Search(BoardState state, int depth, int alpha, int beta)
    {
        nodesVisited++;

        if (hasDeadline && Time.realtimeSinceStartup >= searchDeadline)
        {
            timedOut = true;
            return 0;
        }

        ulong hash = state.ComputeHash();
        int priorVisits = PushPathVisit(hash);

        try
        {
            if (priorVisits > 0 && searchBias.repetitionPenalty > 0)
            {
                return RepetitionPenaltyForSideToMove(state.currentTurn, priorVisits);
            }

            int originalAlpha = alpha;
            int originalBeta = beta;

            if (transpositionTable.TryGetValue(hash, out TranspositionEntry cached) && cached.depth >= depth)
            {
                transpositionHits++;
                switch (cached.type)
                {
                    case EntryType.Exact:
                        return cached.score;
                    case EntryType.LowerBound:
                        alpha = Mathf.Max(alpha, cached.score);
                        break;
                    case EntryType.UpperBound:
                        beta = Mathf.Min(beta, cached.score);
                        break;
                }

                if (alpha >= beta)
                {
                    return cached.score;
                }
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
                    terminalScore = DrawPenaltyForSideToMove(state.currentTurn);
                }

                transpositionTable[hash] = new TranspositionEntry { depth = depth, score = terminalScore, type = EntryType.Exact };
                return terminalScore;
            }

            if (depth == 0)
            {
                leafEvaluations++;
                int evaluation = evaluator.Evaluate(state);
                transpositionTable[hash] = new TranspositionEntry { depth = depth, score = evaluation, type = EntryType.Exact };
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
                    int eval = Search(newState, depth - 1, alpha, beta);
                    if (timedOut)
                    {
                        return 0;
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
                        alphaBetaCutoffs++;
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
                    int eval = Search(newState, depth - 1, alpha, beta);
                    if (timedOut)
                    {
                        return 0;
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
                        alphaBetaCutoffs++;
                        break;
                    }
                }
            }

            EntryType entryType = EntryType.Exact;
            if (bestScore <= originalAlpha)
            {
                entryType = EntryType.UpperBound;
            }
            else if (bestScore >= originalBeta)
            {
                entryType = EntryType.LowerBound;
            }

            transpositionTable[hash] = new TranspositionEntry { depth = depth, score = bestScore, type = entryType };
            return bestScore;
        }
        finally
        {
            PopPathVisit(hash, priorVisits);
        }
    }

    private int PushPathVisit(ulong hash)
    {
        int priorVisits = repetitionCounts.TryGetValue(hash, out int visitCount) ? visitCount : 0;
        repetitionCounts[hash] = priorVisits + 1;
        return priorVisits;
    }

    private void PopPathVisit(ulong hash, int priorVisits)
    {
        if (priorVisits == 0)
        {
            repetitionCounts.Remove(hash);
            return;
        }

        repetitionCounts[hash] = priorVisits;
    }

    private int DrawPenaltyForSideToMove(PieceColor sideToMove)
    {
        return SignedPenaltyForSideToMove(sideToMove, searchBias.drawPenalty);
    }

    private int RepetitionPenaltyForSideToMove(PieceColor sideToMove, int priorVisits)
    {
        int scaledPenalty = searchBias.drawPenalty + (searchBias.repetitionPenalty * (priorVisits + 1));
        return SignedPenaltyForSideToMove(sideToMove, scaledPenalty);
    }

    private static int SignedPenaltyForSideToMove(PieceColor sideToMove, int penalty)
    {
        if (penalty <= 0)
        {
            return 0;
        }

        return sideToMove == PieceColor.White ? penalty : -penalty;
    }
}
