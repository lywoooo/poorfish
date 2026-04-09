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
    private EngineSettings activeSettings;

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
        var legalMoves = MoveGenerator.getLegalMoves(liveState, aiColorEnum);

        if (legalMoves.Count == 0)
        {
            if (MoveGenerator.isInCheck(liveState, aiColorEnum))
            {
                gm.EndGame((aiColorEnum == PieceColor.White ? "black" : "white") + " wins by checkmate.");
            }
            else
            {
                gm.EndGame("Draw by stalemate.");
            }
            calculatingMove = false;
            yield break;
        }

        SearchResult result = searchEngine.FindBestMove(liveState, aiColorEnum, settings);
        GameObject movedPiece = null;

        if(result.hasMove) {
            Vector2Int fromPos = result.bestMove.from;
            movedPiece = gm.PieceAtGrid(fromPos);
            if (movedPiece != null && gm.board != null)
            {
                yield return AnimateAIPickup(gm.board, movedPiece);
            }

            gm.ApplyMove(result.bestMove);

            Debug.Log(gm.currentPlayer.name + " (AI) [" + settings.profileName + "] played "
                + fromPos + " to " + result.bestMove.to
                + " with evaluated score of " + result.bestScore
                + " at depth " + result.stats.completedDepth);

            if (settings.logSearchStats)
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
            && left.evaluationWeights.mobilityWeight == right.evaluationWeights.mobilityWeight;
    }
}
