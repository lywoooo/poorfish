public readonly struct SearchStats
{
    public readonly int nodesVisited;
    public readonly int leafEvaluations;
    public readonly int transpositionHits;
    public readonly int alphaBetaCutoffs;
    public readonly int completedDepth;
    public readonly float elapsedMilliseconds;

    public SearchStats(
        int nodesVisited,
        int leafEvaluations,
        int transpositionHits,
        int alphaBetaCutoffs,
        int completedDepth,
        float elapsedMilliseconds)
    {
        this.nodesVisited = nodesVisited;
        this.leafEvaluations = leafEvaluations;
        this.transpositionHits = transpositionHits;
        this.alphaBetaCutoffs = alphaBetaCutoffs;
        this.completedDepth = completedDepth;
        this.elapsedMilliseconds = elapsedMilliseconds;
    }
}

public readonly struct SearchResult
{
    public readonly Move bestMove;
    public readonly int bestScore;
    public readonly bool hasMove;
    public readonly SearchStats stats;

    public SearchResult(Move bestMove, int bestScore, bool hasMove, SearchStats stats)
    {
        this.bestMove = bestMove;
        this.bestScore = bestScore;
        this.hasMove = hasMove;
        this.stats = stats;
    }
}

public interface ISearchEngine
{
    SearchResult FindBestMove(BoardState state, PieceColor aiColor, EngineSettings settings);
}
