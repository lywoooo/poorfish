using System.Collections;
using UnityEngine;

public enum ChessMatchMode
{
    HumanVsHuman,
    HumanVsAI,
    AIVsAI
}

public enum HumanSide
{
    White,
    Black, 
    Random
}

[DisallowMultipleComponent]
public class ChessMatchCoordinator : MonoBehaviour
{
    [SerializeField] private ChessMatchMode matchMode = ChessMatchMode.HumanVsAI;
    [SerializeField] private HumanSide humanSide = HumanSide.White;
    [SerializeField] private bool recordAIVsAIToCsv = false;
    [SerializeField] private string aiVsAiCsvFileName = "matches.csv";
    [SerializeField] private bool runAIVsAIBatch = false;
    [SerializeField] private int aiVsAiBatchGameCount = 100;
    [SerializeField] private bool rerunStalematesInBatch = false;
    [SerializeField] private float aiVsAiRestartDelay = 0.35f;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Board board;
    [SerializeField] private BoardUI boardUI;
    [SerializeField] private MoveSelector moveSelector;
    [SerializeField] private bool autoConfigureInEditor = true;
    private CsvRecorder csvRecorder;
    private int completedBatchGames;
    private int whiteWins;
    private int blackWins;
    private int draws;
    private bool batchRestartQueued;

    public ChessMatchMode MatchMode => matchMode;
    public int CompletedBatchGames => completedBatchGames;
    public int WhiteWins => whiteWins;
    public int BlackWins => blackWins;
    public int Draws => draws;

    void Reset()
    {
        CacheReferences();
        ApplyMode();
    }

    void Awake()
    {
        CacheReferences();
        ApplyMode();
    }

    void OnEnable()
    {
        GameManager.GameEnded += HandleGameEnded;
    }

    void OnDisable()
    {
        GameManager.GameEnded -= HandleGameEnded;
    }

    void OnValidate()
    {
        CacheReferences();

        if (autoConfigureInEditor)
        {
            ApplyMode();
        }
    }

    private void CacheReferences()
    {
        if (board == null)
        {
            board = GetComponent<Board>();
        }

        if (boardUI == null)
        {
            boardUI = GetComponent<BoardUI>();
        }

        if (moveSelector == null)
        {
            moveSelector = GetComponent<MoveSelector>();
        }

        if (csvRecorder == null)
        {
            csvRecorder = GetComponent<CsvRecorder>();
        }

        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
        }

        if (gameManager != null && board != null && gameManager.board != board)
        {
            gameManager.board = board;
        }
    }

    private void ApplyMode(bool resetBatchProgress = true)
    {
        AIController[] aiControllers = GetComponents<AIController>();
        if (moveSelector != null)
        {
            moveSelector.AllowHumanInput = matchMode != ChessMatchMode.AIVsAI;
        }

        if (Application.isPlaying && csvRecorder == null)
        {
            csvRecorder = gameObject.AddComponent<CsvRecorder>();
        }

        foreach (AIController aiController in aiControllers)
        {
            aiController.enabled = false;
        }

        if (Application.isPlaying && csvRecorder != null)
        {
            bool shouldRecord = matchMode == ChessMatchMode.AIVsAI && recordAIVsAIToCsv;
            int plannedGames = matchMode == ChessMatchMode.AIVsAI && runAIVsAIBatch ? Mathf.Max(1, aiVsAiBatchGameCount) : 1;
            if (resetBatchProgress)
            {
                completedBatchGames = 0;
                whiteWins = 0;
                blackWins = 0;
                draws = 0;
                batchRestartQueued = false;
                csvRecorder.Configure(gameManager, shouldRecord, aiVsAiCsvFileName, aiControllers, plannedGames);
            }
        }

        switch (matchMode)
        {
            case ChessMatchMode.HumanVsHuman:
                return;
            case ChessMatchMode.HumanVsAI:
                ConfigureHumanVsAI(aiControllers);
                return;
            case ChessMatchMode.AIVsAI:
                ConfigureAIVsAI(aiControllers);
                return;
        }
    }

    private void HandleGameEnded(string result, GameResultType resultType)
    {
        if (!Application.isPlaying || matchMode != ChessMatchMode.AIVsAI)
        {
            return;
        }

        bool stalemateResult = resultType == GameResultType.DrawStalemate;
        bool countsTowardTarget = !runAIVsAIBatch || !rerunStalematesInBatch || !stalemateResult;
        if (countsTowardTarget)
        {
            completedBatchGames++;
            TallyResult(resultType);
        }

        int targetGames = runAIVsAIBatch ? Mathf.Max(1, aiVsAiBatchGameCount) : 1;
        if (completedBatchGames >= targetGames)
        {
            if (recordAIVsAIToCsv && csvRecorder != null)
            {
                csvRecorder.FinalizeBatch(completedBatchGames);
            }

            if (runAIVsAIBatch)
            {
                Debug.Log("AI vs AI batch finished. Completed " + completedBatchGames + " counted games.", this);
            }
            return;
        }

        if (runAIVsAIBatch && !batchRestartQueued)
        {
            StartCoroutine(RestartBatchMatch());
        }
    }

    private void TallyResult(GameResultType resultType)
    {
        switch (resultType)
        {
            case GameResultType.WhiteWin:
                whiteWins++;
                return;
            case GameResultType.BlackWin:
                blackWins++;
                return;
            default:
                draws++;
                return;
        }
    }

    private IEnumerator RestartBatchMatch()
    {
        batchRestartQueued = true;
        yield return new WaitForSeconds(aiVsAiRestartDelay);

        CacheReferences();
        if (gameManager == null)
        {
            batchRestartQueued = false;
            yield break;
        }

        gameManager.RestartMatch();

        if (moveSelector != null)
        {
            moveSelector.ResetVisualState();
        }

        foreach (AIController aiController in GetComponents<AIController>())
        {
            aiController.ResetControllerState();
        }

        if (csvRecorder != null)
        {
            csvRecorder.BeginNextGame();
        }

        ApplyMode(false);
        Debug.Log("Starting AI vs AI game " + (completedBatchGames + 1) + " of " + Mathf.Max(1, aiVsAiBatchGameCount) + ".", this);
        batchRestartQueued = false;
    }

    private void ConfigureHumanVsAI(AIController[] aiControllers)
    {
        if (aiControllers.Length == 0)
        {
            Debug.LogWarning("ChessMatchCoordinator needs at least one AIController for Human vs AI mode.", this);
            return;
        }

        bool aiPlaysBlack = humanSide == HumanSide.White;
        AIController selectedController = aiPlaysBlack ? aiControllers[aiControllers.Length - 1] : aiControllers[0];

        selectedController.aiStartColorBlack = aiPlaysBlack;
        selectedController.enabled = true;
    }

    private void ConfigureAIVsAI(AIController[] aiControllers)
    {
        if (aiControllers.Length < 2)
        {
            Debug.LogWarning("ChessMatchCoordinator needs two AIController components for AI vs AI mode.", this);
        }

        if (aiControllers.Length >= 1)
        {
            aiControllers[0].aiStartColorBlack = false;
            aiControllers[0].enabled = true;
        }

        if (aiControllers.Length >= 2)
        {
            aiControllers[1].aiStartColorBlack = true;
            aiControllers[1].enabled = true;
        }
    }

    public void ConfigureBatchFromManager(
        EngineProfile whiteProfile,
        EngineProfile blackProfile,
        int gameCount,
        bool recordCsv,
        string csvFileName,
        bool rerunStalemates,
        float restartDelay)
    {
        CacheReferences();
        matchMode = ChessMatchMode.AIVsAI;
        recordAIVsAIToCsv = recordCsv;
        aiVsAiCsvFileName = string.IsNullOrWhiteSpace(csvFileName) ? "matches.csv" : csvFileName.Trim();
        runAIVsAIBatch = true;
        aiVsAiBatchGameCount = Mathf.Max(1, gameCount);
        rerunStalematesInBatch = rerunStalemates;
        aiVsAiRestartDelay = Mathf.Max(0f, restartDelay);
        AssignEngineProfiles(whiteProfile, blackProfile);

        if (Application.isPlaying)
        {
            ApplyMode();
        }
    }

    private void AssignEngineProfiles(EngineProfile whiteProfile, EngineProfile blackProfile)
    {
        AIController[] aiControllers = GetComponents<AIController>();
        if (aiControllers.Length >= 1)
        {
            aiControllers[0].aiStartColorBlack = false;
            aiControllers[0].engineProfile = whiteProfile;
        }

        if (aiControllers.Length >= 2)
        {
            aiControllers[1].aiStartColorBlack = true;
            aiControllers[1].engineProfile = blackProfile;
        }
    }
}
