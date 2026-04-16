using System.Collections.Generic;
using UnityEngine;

public class MinimaxAB : ISearchEngine
{
    private readonly struct SearchBias
    {
        public readonly int drawPenalty;
        public readonly int repetitionPenalty;
        public readonly int immediateReversalPenalty;

        public SearchBias(int drawPenalty, int repetitionPenalty)
        {
            this.drawPenalty = Mathf.Max(0, drawPenalty);
            this.repetitionPenalty = Mathf.Max(0, repetitionPenalty);
            immediateReversalPenalty = this.drawPenalty + (this.repetitionPenalty * 2);
        }
    }

    public const int POS_INF = 999999;
    public const int NEG_INF = -999999;
    private const int MateScore = 900000;
    private readonly IEvaluator evaluator;
    private float searchDeadline;
    private bool hasDeadline;
    private bool timedOut;
    private int nodesVisited;
    private int leafEvaluations;
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

        Move bestMove = legalMoves[0];
        int bestScore = evaluator.Evaluate(state);
        int completedDepth = 0;
        Move previousDepthBestMove = default;
        bool hasPreviousDepthBestMove = false;

        for (int depth = 1; depth <= settings.searchDepth; depth++)
        {
            Move depthBestMove = default;
            int depthBestScore = aiColor == PieceColor.Black ? POS_INF : NEG_INF;
            bool hasDepthMove = false;

            OrderMoves(state, legalMoves, previousDepthBestMove, hasPreviousDepthBestMove);

            foreach (Move move in legalMoves)
            {
                int score = SearchChildMove(state, move, depth, NEG_INF, POS_INF);

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

    private void BeginTimedSearch(float maxThinkTimeSeconds)
    {
        timedOut = false;
        nodesVisited = 0;
        leafEvaluations = 0;
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
            0,
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

        if (depth == 0)
        {
            leafEvaluations++;
            return evaluator.Evaluate(state);
        }

        var legalMoves = MoveGenerator.getLegalMoves(state, state.currentTurn);
        OrderMoves(state, legalMoves, default, false);

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

            return terminalScore;
        }

        bool maximizing = state.currentTurn == PieceColor.White;
        int bestScore = maximizing ? NEG_INF : POS_INF;

        foreach (var move in legalMoves)
        {
            int eval = SearchChildMove(state, move, depth, alpha, beta);

            if (timedOut)
            {
                return 0;
            }

            if (IsBetterScore(eval, bestScore, maximizing))
            {
                bestScore = eval;
            }

            if (maximizing)
            {
                alpha = Mathf.Max(alpha, eval);
            }
            else
            {
                beta = Mathf.Min(beta, eval);
            }

            if (beta <= alpha)
            {
                alphaBetaCutoffs++;
                break;
            }
        }

        return bestScore;
    }

    private int SearchChildMove(BoardState state, Move move, int depth, int alpha, int beta)
    {
        BoardState.MoveUndo undo = state.MakeMove(move);
        state.switchTurn();
        int eval = Search(state, depth - 1, alpha, beta);
        state.UnmakeMove(move, undo);

        if (timedOut)
        {
            return 0;
        }

        return ApplyImmediateReversalPenalty(state, move, eval);
    }

    private static bool IsBetterScore(int score, int bestScore, bool maximizing)
    {
        return maximizing ? score > bestScore : score < bestScore;
    }

    private int DrawPenaltyForSideToMove(PieceColor sideToMove)
    {
        return SignedPenaltyForSideToMove(sideToMove, searchBias.drawPenalty);
    }

    private int ApplyImmediateReversalPenalty(BoardState state, Move move, int evaluation)
    {
        if (!IsImmediateReversal(state, move))
        {
            return evaluation;
        }

        return evaluation + SignedPenaltyAgainstMover(state.currentTurn, searchBias.immediateReversalPenalty);
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
        moves.Sort((a, b) => ScoreMove(state, b, preferredMove, hasPreferredMove).CompareTo(ScoreMove(state, a, preferredMove, hasPreferredMove)));
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
