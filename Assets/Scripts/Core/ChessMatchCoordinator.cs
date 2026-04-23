using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    [SerializeField] private int maxFullMovesPerGame = 100;
    [SerializeField] private bool alternateColorsInBatch = true;
    [SerializeField] private bool useEqualPositionFenStarts = false;
    [SerializeField] private string equalPositionFenResourceName = "equal_positions";
    [SerializeField] private bool rerunStalematesInBatch = false;
    [SerializeField] private float aiVsAiRestartDelay = 0.35f;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Board board;
    [SerializeField] private BoardUI boardUI;
    [SerializeField] private MoveSelector moveSelector;
    [SerializeField] private bool autoConfigureInEditor = true;
    [SerializeField] private EngineProfile configuredWhiteProfile;
    [SerializeField] private EngineProfile configuredBlackProfile;
    private CsvRecorder csvRecorder;
    private int completedBatchGames;
    private int selectedWhiteProfileWins;
    private int selectedBlackProfileWins;
    private int draws;
    private int whiteSideWins;
    private int blackSideWins;
    private bool batchRestartQueued;
    private readonly List<string> equalPositionFens = new List<string>(1024);
    private string loadedEqualPositionResourceName;
    private string currentBatchStartingFen;

    public ChessMatchMode MatchMode => matchMode;
    public int CompletedBatchGames => completedBatchGames;
    public int WhiteWins => selectedWhiteProfileWins;
    public int BlackWins => selectedBlackProfileWins;
    public int Draws => draws;
    public int WhiteSideWins => whiteSideWins;
    public int BlackSideWins => blackSideWins;

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
            int plannedGames = matchMode == ChessMatchMode.AIVsAI && runAIVsAIBatch ? EffectiveBatchTargetGameCount() : 1;
            if (resetBatchProgress)
            {
                completedBatchGames = 0;
                selectedWhiteProfileWins = 0;
                selectedBlackProfileWins = 0;
                draws = 0;
                whiteSideWins = 0;
                blackSideWins = 0;
                batchRestartQueued = false;
                currentBatchStartingFen = null;
                PrepareStartingFenForBatchGame();
                AssignEngineProfilesForBatchGame(aiControllers);
                csvRecorder.Configure(gameManager, shouldRecord, aiVsAiCsvFileName, aiControllers, plannedGames);
            }
        }

        if (gameManager != null)
        {
            gameManager.MaxFullMoves = maxFullMovesPerGame;
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
            TallyResult(resultType);
            completedBatchGames++;
        }

        int targetGames = runAIVsAIBatch ? EffectiveBatchTargetGameCount() : 1;
        if (completedBatchGames >= targetGames)
        {
            if (recordAIVsAIToCsv && csvRecorder != null)
            {
                StartCoroutine(FinalizeBatchAfterRecorders());
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

    private IEnumerator FinalizeBatchAfterRecorders()
    {
        yield return null;

        if (csvRecorder != null)
        {
            csvRecorder.FinalizeBatch(completedBatchGames, selectedWhiteProfileWins, selectedBlackProfileWins, draws);
        }
    }

    private void TallyResult(GameResultType resultType)
    {
        bool swapProfiles = ShouldSwapProfilesForCurrentGame();

        switch (resultType)
        {
            case GameResultType.WhiteWin:
                whiteSideWins++;
                if (swapProfiles)
                {
                    selectedBlackProfileWins++;
                }
                else
                {
                    selectedWhiteProfileWins++;
                }
                return;
            case GameResultType.BlackWin:
                blackSideWins++;
                if (swapProfiles)
                {
                    selectedWhiteProfileWins++;
                }
                else
                {
                    selectedBlackProfileWins++;
                }
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

        AIController[] aiControllers = GetComponents<AIController>();
        PrepareStartingFenForBatchGame();
        AssignEngineProfilesForBatchGame(aiControllers);

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
            csvRecorder.BeginNextGame(aiControllers);
        }

        ApplyMode(false);
        Debug.Log("Starting AI vs AI game " + (completedBatchGames + 1) + " of " + EffectiveBatchTargetGameCount() + ".", this);
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
        int maxFullMoves,
        bool alternateColors,
        bool rerunStalemates,
        float restartDelay,
        bool useEqualPositionFens = false,
        string equalPositionFenResource = "equal_positions")
    {
        CacheReferences();
        matchMode = ChessMatchMode.AIVsAI;
        recordAIVsAIToCsv = recordCsv;
        aiVsAiCsvFileName = string.IsNullOrWhiteSpace(csvFileName) ? "matches.csv" : csvFileName.Trim();
        runAIVsAIBatch = true;
        useEqualPositionFenStarts = useEqualPositionFens;
        equalPositionFenResourceName = string.IsNullOrWhiteSpace(equalPositionFenResource)
            ? "equal_positions"
            : equalPositionFenResource.Trim();
        aiVsAiBatchGameCount = NormalizeBatchGameCount(gameCount);
        maxFullMovesPerGame = Mathf.Max(1, maxFullMoves);
        alternateColorsInBatch = alternateColors;
        rerunStalematesInBatch = rerunStalemates;
        aiVsAiRestartDelay = Mathf.Max(0f, restartDelay);
        configuredWhiteProfile = whiteProfile;
        configuredBlackProfile = blackProfile;
        currentBatchStartingFen = null;
        PrepareStartingFenForBatchGame();
        AssignEngineProfilesForBatchGame(GetComponents<AIController>());

        if (Application.isPlaying)
        {
            ApplyMode();
        }
    }

    private void AssignEngineProfilesForBatchGame(AIController[] aiControllers)
    {
        if (configuredWhiteProfile == null && configuredBlackProfile == null)
        {
            return;
        }

        bool swapProfiles = ShouldSwapProfilesForCurrentGame();

        EngineProfile whiteProfile = swapProfiles ? configuredBlackProfile : configuredWhiteProfile;
        EngineProfile blackProfile = swapProfiles ? configuredWhiteProfile : configuredBlackProfile;
        AssignEngineProfiles(whiteProfile, blackProfile, aiControllers);
    }

    private bool ShouldSwapProfilesForCurrentGame()
    {
        bool shouldPairColors = useEqualPositionFenStarts || alternateColorsInBatch;
        return shouldPairColors
            && configuredWhiteProfile != configuredBlackProfile
            && completedBatchGames % 2 == 1;
    }

    private int NormalizeBatchGameCount(int gameCount)
    {
        int normalizedCount = Mathf.Max(useEqualPositionFenStarts ? 2 : 1, gameCount);
        if (useEqualPositionFenStarts && normalizedCount % 2 == 1)
        {
            normalizedCount++;
        }

        return normalizedCount;
    }

    private int EffectiveBatchTargetGameCount()
    {
        return NormalizeBatchGameCount(aiVsAiBatchGameCount);
    }

    private void PrepareStartingFenForBatchGame()
    {
        if (gameManager == null)
        {
            return;
        }

        if (!useEqualPositionFenStarts)
        {
            currentBatchStartingFen = null;
            gameManager.ClearRuntimeStartingFen();
            return;
        }

        bool shouldPickNewFen = string.IsNullOrWhiteSpace(currentBatchStartingFen)
            || (completedBatchGames > 0 && completedBatchGames % 2 == 0);
        if (shouldPickNewFen)
        {
            if (!TryPickRandomEqualPositionFen(out currentBatchStartingFen))
            {
                gameManager.ClearRuntimeStartingFen();
                Debug.LogWarning("Could not load an equal-position FEN. Falling back to the configured/default start.", this);
                return;
            }
        }

        gameManager.SetRuntimeStartingFen(currentBatchStartingFen);
    }

    private bool TryPickRandomEqualPositionFen(out string fen)
    {
        fen = null;
        EnsureEqualPositionFensLoaded();
        if (equalPositionFens.Count == 0)
        {
            return false;
        }

        int attempts = Mathf.Min(20, equalPositionFens.Count);
        for (int i = 0; i < attempts; i++)
        {
            string candidate = equalPositionFens[Random.Range(0, equalPositionFens.Count)];
            if (FEN.TryLoadFen(candidate, out _))
            {
                fen = candidate;
                return true;
            }
        }

        return false;
    }

    private void EnsureEqualPositionFensLoaded()
    {
        string resourceName = string.IsNullOrWhiteSpace(equalPositionFenResourceName)
            ? "equal_positions"
            : equalPositionFenResourceName.Trim();

        if (loadedEqualPositionResourceName == resourceName && equalPositionFens.Count > 0)
        {
            return;
        }

        loadedEqualPositionResourceName = resourceName;
        equalPositionFens.Clear();

        string fenText = LoadEqualPositionFenText(resourceName);
        if (string.IsNullOrWhiteSpace(fenText))
        {
            return;
        }

        string[] lines = fenText.Split('\n');
        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
            {
                continue;
            }

            equalPositionFens.Add(trimmedLine);
        }
    }

    private string LoadEqualPositionFenText(string resourceName)
    {
        TextAsset fenAsset = Resources.Load<TextAsset>(resourceName);
        if (fenAsset != null)
        {
            return fenAsset.text;
        }

        string fileName = Path.HasExtension(resourceName) ? resourceName : resourceName + ".fens";
        string filePath = Path.Combine(Application.dataPath, "Resources", fileName);
        return File.Exists(filePath) ? File.ReadAllText(filePath) : null;
    }

    private void AssignEngineProfiles(EngineProfile whiteProfile, EngineProfile blackProfile, AIController[] aiControllers)
    {
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
