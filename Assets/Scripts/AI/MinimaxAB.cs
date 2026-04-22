using System.Collections.Generic;
using UnityEngine;

public class MinimaxAB : ISearchEngine
{
    private enum TranspositionBound
    {
        Exact,
        Lower,
        Upper
    }

    private readonly struct SearchBias
    {
        public readonly int drawPenalty;
        public readonly int repetitionPenalty;
        public readonly int immediateReversalPenalty;
        public readonly int ownReversalPenalty;

        public SearchBias(int drawPenalty, int repetitionPenalty)
        {
            this.drawPenalty = Mathf.Max(0, drawPenalty);
            this.repetitionPenalty = Mathf.Max(0, repetitionPenalty);
            immediateReversalPenalty = this.drawPenalty + (this.repetitionPenalty * 2);
            ownReversalPenalty = this.repetitionPenalty;
        }
    }

    private readonly struct TranspositionEntry
    {
        public readonly int depth;
        public readonly int score;
        public readonly Move bestMove;
        public readonly bool hasBestMove;
        public readonly TranspositionBound bound;

        public TranspositionEntry(int depth, int score, Move bestMove, bool hasBestMove, TranspositionBound bound)
        {
            this.depth = depth;
            this.score = score;
            this.bestMove = bestMove;
            this.hasBestMove = hasBestMove;
            this.bound = bound;
        }
    }

    public const int POS_INF = 999999;
    public const int NEG_INF = -999999;
    private const int MateScore = 900000;
    private const int MaxAdaptiveDepthBonus = 3;
    private const int MaxTranspositionEntries = 262_144;
    private readonly IEvaluator evaluator;
    private float searchDeadline;
    private bool hasDeadline;
    private bool timedOut;
    private int nodesVisited;
    private int leafEvaluations;
    private int alphaBetaCutoffs;
    private int transpositionHits;
    private float searchStartTime;
    private SearchBias searchBias;
    private readonly List<Move> rootLegalMoves = new List<Move>(128);
    private readonly List<Move> rootPseudoMoves = new List<Move>(128);
    private readonly List<int> moveOrderScores = new List<int>(128);
    private readonly Dictionary<ulong, TranspositionEntry> transpositionTable = new Dictionary<ulong, TranspositionEntry>(1 << 16);
    private List<Move>[] legalMoveBuffers;
    private List<Move>[] pseudoMoveBuffers;

    public MinimaxAB(IEvaluator evaluator)
    {
        this.evaluator = evaluator;
    }

    public SearchResult FindBestMove(BoardState state, PieceColor aiColor, EngineSettings settings)
    {
        MoveGenerator.GetLegalMoves(state, aiColor, rootLegalMoves, rootPseudoMoves);
        if (rootLegalMoves.Count == 0)
        {
            return new SearchResult(default, aiColor == PieceColor.Black ? POS_INF : NEG_INF, false, new SearchStats(0, 0, 0, 0, 0, 0f));
        }

        if (settings.useImmediateCheckmateShortcut &&
            TryFindCheckmateMove(state, aiColor, rootLegalMoves, rootPseudoMoves, out Move checkmateMove))
        {
            int mateScore = aiColor == PieceColor.White ? MateScore : -MateScore;
            return new SearchResult(checkmateMove, mateScore, true, new SearchStats(rootLegalMoves.Count, 0, 0, 0, 0, 0f));
        }

        int searchDepth = settings.useAdaptiveEndgameDepth
            ? AdaptiveSearchDepth(state, settings.searchDepth, rootLegalMoves.Count)
            : Mathf.Max(1, settings.searchDepth);
        EnsureSearchBuffers(searchDepth + 2);
        BeginTimedSearch(settings.maxThinkTimeSeconds);
        searchBias = new SearchBias(
            settings.evaluationWeights.drawPenalty,
            settings.evaluationWeights.repetitionPenalty);

        Move bestMove = rootLegalMoves[0];
        int bestScore = evaluator.Evaluate(state);
        int completedDepth = 0;
        Move previousDepthBestMove = default;
        bool hasPreviousDepthBestMove = false;

        for (int depth = 1; depth <= searchDepth; depth++)
        {
            Move depthBestMove = default;
            int depthBestScore = aiColor == PieceColor.Black ? POS_INF : NEG_INF;
            bool hasDepthMove = false;

            if (settings.useMoveOrdering)
            {
                OrderMoves(state, rootLegalMoves, previousDepthBestMove, hasPreviousDepthBestMove);
            }

            foreach (Move move in rootLegalMoves)
            {
                int score = SearchChildMove(state, move, depth, NEG_INF, POS_INF, settings);

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
            previousDepthBestMove = depthBestMove;
            hasPreviousDepthBestMove = hasDepthMove;
        }

        EndTimedSearch();
        return new SearchResult(bestMove, bestScore, true, BuildStats(completedDepth));
    }

    private static bool TryFindCheckmateMove(
        BoardState state,
        PieceColor aiColor,
        List<Move> legalMoves,
        List<Move> candidateBuffer,
        out Move checkmateMove)
    {
        PieceColor opponent = aiColor == PieceColor.White ? PieceColor.Black : PieceColor.White;

        foreach (Move move in legalMoves)
        {
            BoardState.MoveUndo undo = state.MakeMove(move);
            state.switchTurn();

            bool isMate = MoveGenerator.isInCheck(state, opponent)
                && !MoveGenerator.HasAnyLegalMove(state, opponent, candidateBuffer);

            state.UnmakeMove(move, undo);

            if (isMate)
            {
                checkmateMove = move;
                return true;
            }
        }

        checkmateMove = default;
        return false;
    }

    private static int AdaptiveSearchDepth(BoardState state, int baseDepth, int rootMoveCount)
    {
        int depth = Mathf.Max(1, baseDepth);
        if (!Evaluator.IsEndgame(state))
        {
            return depth;
        }

        int bonus = 0;
        if (rootMoveCount <= 8)
        {
            bonus = 3;
        }
        else if (rootMoveCount <= 16)
        {
            bonus = 2;
        }
        else if (rootMoveCount <= 24)
        {
            bonus = 1;
        }

        return depth + Mathf.Min(bonus, MaxAdaptiveDepthBonus);
    }

    private void BeginTimedSearch(float maxThinkTimeSeconds)
    {
        timedOut = false;
        nodesVisited = 0;
        leafEvaluations = 0;
        alphaBetaCutoffs = 0;
        transpositionHits = 0;
        searchStartTime = Time.realtimeSinceStartup;
        transpositionTable.Clear();

        if (maxThinkTimeSeconds <= 0f)
        {
            hasDeadline = false;
            return;
        }

        hasDeadline = true;
        searchDeadline = Time.realtimeSinceStartup + maxThinkTimeSeconds;
    }

    private void EnsureSearchBuffers(int requiredCount)
    {
        if (legalMoveBuffers != null && legalMoveBuffers.Length >= requiredCount)
        {
            return;
        }

        legalMoveBuffers = new List<Move>[requiredCount];
        pseudoMoveBuffers = new List<Move>[requiredCount];
        for (int i = 0; i < requiredCount; i++)
        {
            legalMoveBuffers[i] = new List<Move>(128);
            pseudoMoveBuffers[i] = new List<Move>(128);
        }
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

    private bool TryReadTransposition(EngineSettings settings, ulong positionKey, int depth, int alpha, int beta, out int score)
    {
        score = 0;
        if (!settings.useTranspositionTable)
        {
            return false;
        }

        if (!transpositionTable.TryGetValue(positionKey, out TranspositionEntry entry) || entry.depth < depth)
        {
            return false;
        }

        switch (entry.bound)
        {
            case TranspositionBound.Exact:
                score = entry.score;
                transpositionHits++;
                return true;
            case TranspositionBound.Lower:
                if (entry.score >= beta)
                {
                    score = entry.score;
                    transpositionHits++;
                    return true;
                }
                break;
            case TranspositionBound.Upper:
                if (entry.score <= alpha)
                {
                    score = entry.score;
                    transpositionHits++;
                    return true;
                }
                break;
        }

        return false;
    }

    private void StoreTransposition(
        EngineSettings settings,
        ulong positionKey,
        int depth,
        int score,
        Move bestMove,
        bool hasBestMove,
        TranspositionBound bound)
    {
        if (timedOut || !settings.useTranspositionTable)
        {
            return;
        }

        if (transpositionTable.TryGetValue(positionKey, out TranspositionEntry existing) && existing.depth > depth)
        {
            return;
        }

        if (!transpositionTable.ContainsKey(positionKey) && transpositionTable.Count >= MaxTranspositionEntries)
        {
            return;
        }

        transpositionTable[positionKey] = new TranspositionEntry(depth, score, bestMove, hasBestMove, bound);
    }

    private static TranspositionBound BoundFromScore(int score, int alpha, int beta)
    {
        if (score <= alpha)
        {
            return TranspositionBound.Upper;
        }

        if (score >= beta)
        {
            return TranspositionBound.Lower;
        }

        return TranspositionBound.Exact;
    }

    private bool TryGetStoredBestMove(BoardState state, out Move bestMove)
    {
        return TryGetStoredBestMove(HashPosition(state), out bestMove);
    }

    private bool TryGetStoredBestMove(ulong positionKey, out Move bestMove)
    {
        if (transpositionTable.TryGetValue(positionKey, out TranspositionEntry entry) && entry.hasBestMove)
        {
            bestMove = entry.bestMove;
            return true;
        }

        bestMove = default;
        return false;
    }

    private static ulong HashPosition(BoardState state)
    {
        unchecked
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;

            for (int square = 0; square < state.board.Length; square++)
            {
                hash ^= (ulong)(state.board[square] + 31 + (square * 17));
                hash *= prime;
            }

            hash ^= (ulong)((int)state.currentTurn + 101);
            hash *= prime;
            hash ^= (ulong)((int)state.castlingRights + 211);
            hash *= prime;
            hash ^= (ulong)(state.enPassantTarget + 409);
            hash *= prime;

            return hash;
        }
    }

    private int Search(BoardState state, int depth, int alpha, int beta, EngineSettings settings)
    {
        nodesVisited++;

        if (hasDeadline &&
            (nodesVisited & 2047) == 0 &&
            Time.realtimeSinceStartup >= searchDeadline)
        {
            timedOut = true;
            return 0;
        }

        int bufferIndex = Mathf.Clamp(depth, 0, legalMoveBuffers.Length - 1);
        int originalAlpha = alpha;
        int originalBeta = beta;
        ulong positionKey = HashPosition(state);

        if (TryReadTransposition(settings, positionKey, depth, alpha, beta, out int cachedScore))
        {
            return cachedScore;
        }

        if (depth == 0)
        {
            bool inCheck = MoveGenerator.isInCheck(state, state.currentTurn);
            if (inCheck && !MoveGenerator.HasAnyLegalMove(state, state.currentTurn, pseudoMoveBuffers[bufferIndex]))
            {
                int mateScore = state.currentTurn == PieceColor.White
                    ? -MateScore - depth
                    : MateScore + depth;
                StoreTransposition(settings, positionKey, depth, mateScore, default, false, TranspositionBound.Exact);
                return mateScore;
            }

            leafEvaluations++;
            int staticScore = evaluator.Evaluate(state);
            StoreTransposition(settings, positionKey, depth, staticScore, default, false, TranspositionBound.Exact);
            return staticScore;
        }

        List<Move> legalMoves = legalMoveBuffers[bufferIndex];
        List<Move> pseudoMoves = pseudoMoveBuffers[bufferIndex];
        MoveGenerator.GetLegalMoves(state, state.currentTurn, legalMoves, pseudoMoves);

        if (legalMoves.Count == 0)
        {
            if (MoveGenerator.isInCheck(state, state.currentTurn))
            {
                int mateScore = state.currentTurn == PieceColor.White
                    ? -MateScore - depth
                    : MateScore + depth;
                StoreTransposition(settings, positionKey, depth, mateScore, default, false, TranspositionBound.Exact);
                return mateScore;
            }

            int drawScore = DrawPenaltyForSideToMove(state.currentTurn);
            StoreTransposition(settings, positionKey, depth, drawScore, default, false, TranspositionBound.Exact);
            return drawScore;
        }

        Move preferredMove = default;
        bool hasPreferredMove = settings.useMoveOrdering
            && settings.useTranspositionTable
            && TryGetStoredBestMove(positionKey, out preferredMove);
        if (settings.useMoveOrdering)
        {
            OrderMoves(state, legalMoves, preferredMove, hasPreferredMove);
        }

        bool maximizing = state.currentTurn == PieceColor.White;
        int bestScore = maximizing ? NEG_INF : POS_INF;
        Move bestMove = default;
        bool hasBestMove = false;

        foreach (var move in legalMoves)
        {
            int eval = SearchChildMove(state, move, depth, alpha, beta, settings);

            if (timedOut)
            {
                return 0;
            }

            if (IsBetterScore(eval, bestScore, maximizing))
            {
                bestScore = eval;
                bestMove = move;
                hasBestMove = true;
            }

            if (settings.useAlphaBetaPruning && maximizing)
            {
                alpha = Mathf.Max(alpha, eval);
            }
            else if (settings.useAlphaBetaPruning)
            {
                beta = Mathf.Min(beta, eval);
            }

            if (settings.useAlphaBetaPruning && beta <= alpha)
            {
                alphaBetaCutoffs++;
                break;
            }
        }

        StoreTransposition(
            settings,
            positionKey,
            depth,
            bestScore,
            bestMove,
            hasBestMove,
            BoundFromScore(bestScore, originalAlpha, originalBeta));

        return bestScore;
    }

    private int SearchChildMove(BoardState state, Move move, int depth, int alpha, int beta, EngineSettings settings)
    {
        BoardState.MoveUndo undo = state.MakeMove(move);
        state.switchTurn();
        int eval = Search(state, depth - 1, alpha, beta, settings);
        state.UnmakeMove(move, undo);

        if (timedOut)
        {
            return 0;
        }

        return ApplyMoveHistoryPenalty(state, move, eval);
    }

    private static bool IsBetterScore(int score, int bestScore, bool maximizing)
    {
        return maximizing ? score > bestScore : score < bestScore;
    }

    private int DrawPenaltyForSideToMove(PieceColor sideToMove)
    {
        return SignedPenaltyForSideToMove(sideToMove, searchBias.drawPenalty);
    }

    private int ApplyMoveHistoryPenalty(BoardState state, Move move, int evaluation)
    {
        PieceColor mover = state.currentTurn;

        if (IsImmediateReversal(state, move))
        {
            evaluation += SignedPenaltyAgainstMover(mover, searchBias.immediateReversalPenalty);
        }

        if (IsOwnPreviousMoveReversal(state, mover, move))
        {
            evaluation += SignedPenaltyAgainstMover(mover, searchBias.ownReversalPenalty);
        }

        return evaluation;
    }

    private static bool IsImmediateReversal(BoardState state, Move move)
    {
        if (!state.hasLastMove)
        {
            return false;
        }

        Move previousMove = state.lastMove;
        return move.from == previousMove.to
            && move.to == previousMove.from
            && move.promotionType == PieceType.None
            && !move.isCastling
            && !move.isEnPassant;
    }

    private static bool IsOwnPreviousMoveReversal(BoardState state, PieceColor mover, Move move)
    {
        if (move.isCapture || move.isPromotion || move.isCastling || move.isEnPassant)
        {
            return false;
        }

        if (!state.TryGetLastMoveForColor(mover, out Move previousOwnMove))
        {
            return false;
        }

        return move.from == previousOwnMove.to
            && move.to == previousOwnMove.from;
    }

    private static int SignedPenaltyAgainstMover(PieceColor mover, int penalty)
    {
        if (penalty <= 0)
        {
            return 0;
        }

        return mover == PieceColor.White ? -penalty : penalty;
    }

    private static int SignedPenaltyForSideToMove(PieceColor sideToMove, int penalty)
    {
        if (penalty <= 0)
        {
            return 0;
        }

        return sideToMove == PieceColor.White ? penalty : -penalty;
    }

    private static bool SameMove(Move a, Move b)
    {
        return a.from == b.from
            && a.to == b.to
            && a.flags == b.flags
            && a.promotionType == b.promotionType;
    }

    private void OrderMoves(BoardState state, List<Move> moves, Move preferredMove, bool hasPreferredMove)
    {
        moveOrderScores.Clear();
        for (int i = 0; i < moves.Count; i++)
        {
            moveOrderScores.Add(ScoreMove(state, moves[i], preferredMove, hasPreferredMove));
        }

        for (int i = 1; i < moves.Count; i++)
        {
            Move move = moves[i];
            int score = moveOrderScores[i];
            int j = i - 1;

            while (j >= 0 && moveOrderScores[j] < score)
            {
                moves[j + 1] = moves[j];
                moveOrderScores[j + 1] = moveOrderScores[j];
                j--;
            }

            moves[j + 1] = move;
            moveOrderScores[j + 1] = score;
        }
    }

    private int ScoreMove(BoardState state, Move move, Move preferredMove, bool hasPreferredMove)
    {
        if (hasPreferredMove && SameMove(move, preferredMove))
        {
            return 1_000_000;
        }

        int score = 0;

        if (move.isPromotion)
        {
            score += 800_000 + Evaluator.GetMaterialValue(move.promotionType);
        }

        if (move.isCapture)
        {
            score += 500_000 + CaptureScore(state, move);
        }

        if (move.isCastling)
        {
            score += 10_000;
        }

        return score;
    }

    private int CaptureScore(BoardState state, Move move)
    {
        int attacker = state.board[move.from];

        PieceType victimType;

        if (move.isEnPassant)
        {
            victimType = PieceType.Pawn;
        }
        else
        {
            int victim = state.board[move.to];

            if (PieceBits.isEmpty(victim))
            {
                return 0;
            }

            victimType = PieceBits.GetType(victim);
        }

        PieceType attackerType = PieceBits.GetType(attacker);

        return Evaluator.GetMaterialValue(victimType) * 10 - Evaluator.GetMaterialValue(attackerType);
    }
}
