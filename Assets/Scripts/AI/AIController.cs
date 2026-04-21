using System.Collections;
using UnityEngine;

public class AIController : MonoBehaviour
{
    // Assumes black is played by the AI
    public bool aiStartColorBlack = true;

    // Move delay for cool factor
    public float moveDelay = 0.5f;
    [SerializeField] private float aiPickupDuration = 0.12f;
    [SerializeField] private float aiPickupLiftRatio = 0.18f;
    public EngineProfile engineProfile;
    public EngineSettings fallbackSettings = EngineSettings.Default;
    private bool calculatingMove = false;
    private ISearchEngine searchEngine;
    private IEvaluator evaluator;
    private OpeningBook openingBook;
    private EngineSettings activeSettings;
    private readonly System.Collections.Generic.List<Move> legalMoveProbeBuffer = new System.Collections.Generic.List<Move>(32);

    void Update()
    {
        if (calculatingMove || GameManager.instance == null || GameManager.instance.IsGameOver) return;

        string aiColor = aiStartColorBlack ? "black" : "white";

        if(GameManager.instance.currentPlayer.name == aiColor) {
            calculatingMove = true;
            StartCoroutine(turnEval());
        }
    }

    private void Awake()
    {
        RebuildEngine();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            RebuildEngine();
        }
    }

    private void RebuildEngine()
    {
        activeSettings = GetActiveSettings();
        evaluator = new ConfigurableEvaluator(activeSettings.evaluationWeights, activeSettings.profileName + "_Evaluator");
        searchEngine = new MinimaxAB(evaluator);
        openingBook = new OpeningBook();
    }

    private EngineSettings GetActiveSettings()
    {
        EngineSettings settings = engineProfile != null ? engineProfile.ToSettings() : fallbackSettings;
        if (settings.searchDepth < 1)
        {
            settings.searchDepth = 1;
        }

        if (string.IsNullOrWhiteSpace(settings.profileName))
        {
            settings.profileName = "Baseline";
        }

        return settings;
    }

    private IEnumerator turnEval() {
        yield return new WaitForSeconds(moveDelay);

        var gm = GameManager.instance;
        string aiColor = aiStartColorBlack ? "black" : "white";
        PieceColor aiColorEnum = aiStartColorBlack ? PieceColor.Black : PieceColor.White;
        EngineSettings settings = GetActiveSettings();
        if (searchEngine == null || evaluator == null || !SettingsMatch(settings, activeSettings))
        {
            RebuildEngine();
            settings = activeSettings;
        }

        BoardState liveState = BoardState.boardSnapshot();

        if (!MoveGenerator.HasAnyLegalMove(liveState, aiColorEnum, legalMoveProbeBuffer))
        {
            if (MoveGenerator.isInCheck(liveState, aiColorEnum))
            {
                bool whiteWins = aiColorEnum == PieceColor.Black;
                gm.EndGame(
                    (whiteWins ? "white" : "black") + " wins by checkmate.",
                    whiteWins ? GameResultType.WhiteWin : GameResultType.BlackWin);
            }
            else
            {
                gm.EndGame("Draw by stalemate.", GameResultType.DrawStalemate);
            }
            calculatingMove = false;
            yield break;
        }

        SearchResult result;
        Move bookMove = default;
        bool usedOpeningBook = openingBook != null && openingBook.TryGetBookMove(liveState, out bookMove);
        if (usedOpeningBook)
        {
            result = new SearchResult(bookMove, 0, true, new SearchStats(0, 0, 0, 0, 0, 0f));
        }
        else
        {
            result = searchEngine.FindBestMove(liveState, aiColorEnum, settings);
        }

        GameObject movedPiece = null;

        if(result.hasMove) {
            Vector2Int fromPos = result.bestMove.FromVector;
            Vector2Int toPos = result.bestMove.ToVector;
            movedPiece = gm.PieceAtGrid(fromPos);
            if (movedPiece != null && gm.board != null)
            {
                yield return AnimateAIPickup(gm.board, movedPiece);
            }

            CsvRecorder csvRecorder = GetComponent<CsvRecorder>();
            if (csvRecorder != null)
            {
                csvRecorder.PrepareEngineMove(
                    liveState,
                    aiColorEnum,
                    result.bestMove,
                    result,
                    settings,
                    evaluator != null ? evaluator.Name : string.Empty,
                    usedOpeningBook);
            }

            gm.ApplyMove(result.bestMove);

            Debug.Log(gm.currentPlayer.name + " (AI) [" + settings.profileName + "] played "
                + fromPos + " to " + toPos
                + (usedOpeningBook ? " from opening book" : "")
                + " with evaluated score of " + result.bestScore
                + " at depth " + result.stats.completedDepth);

            if (settings.logSearchStats && !usedOpeningBook)
            {
                Debug.Log("Search stats [" + settings.profileName + "] "
                    + "nodes=" + result.stats.nodesVisited
                    + ", evals=" + result.stats.leafEvaluations
                    + ", ttHits=" + result.stats.transpositionHits
                    + ", cutoffs=" + result.stats.alphaBetaCutoffs
                    + ", elapsedMs=" + result.stats.elapsedMilliseconds.ToString("F1")
                    + ", evaluator=" + evaluator.Name);
            }
        }

        if (!gm.IsGameOver)
        {
            while (movedPiece != null && gm.board != null && gm.board.IsPieceAnimating(movedPiece))
            {
                yield return null;
            }

            if (movedPiece != null && gm.board != null)
            {
                gm.board.SetPieceDragState(movedPiece, false);
            }

            gm.NextPlayer();
            var moveSelector = GetComponent<MoveSelector>();
            if (!gm.IsGameOver && moveSelector != null)
            {
                moveSelector.EnterState();
            }
        }
        calculatingMove = false;
    }

    private IEnumerator AnimateAIPickup(Board board, GameObject piece)
    {
        if (aiPickupDuration <= 0f)
        {
            board.SetPieceDragState(piece, true);
            yield break;
        }

        Vector3 startPosition = piece.transform.position;
        Vector3 liftedPosition = startPosition + Vector3.up * (Geometry.CellSize * aiPickupLiftRatio);
        board.SetPieceDragState(piece, true);

        float elapsed = 0f;
        while (elapsed < aiPickupDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / aiPickupDuration);
            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            board.SetPieceWorldPosition(piece, Vector3.Lerp(startPosition, liftedPosition, easedT));
            yield return null;
        }

        board.SetPieceWorldPosition(piece, liftedPosition);
    }

    private static bool SettingsMatch(EngineSettings left, EngineSettings right)
    {
        return left.profileName == right.profileName
            && left.searchDepth == right.searchDepth
            && Mathf.Approximately(left.maxThinkTimeSeconds, right.maxThinkTimeSeconds)
            && left.logSearchStats == right.logSearchStats
            && left.evaluationWeights.materialWeight == right.evaluationWeights.materialWeight
            && left.evaluationWeights.pieceSquareWeight == right.evaluationWeights.pieceSquareWeight
            && left.evaluationWeights.mobilityWeight == right.evaluationWeights.mobilityWeight
            && left.evaluationWeights.drawPenalty == right.evaluationWeights.drawPenalty
            && left.evaluationWeights.repetitionPenalty == right.evaluationWeights.repetitionPenalty
            && left.evaluationWeights.endgameMateWeight == right.evaluationWeights.endgameMateWeight
            && left.evaluationWeights.kingEdgeWeight == right.evaluationWeights.kingEdgeWeight
            && left.evaluationWeights.kingDistanceWeight == right.evaluationWeights.kingDistanceWeight;
    }

    public void ResetControllerState()
    {
        StopAllCoroutines();
        calculatingMove = false;
    }
}
