using UnityEngine;

public enum ChessMatchMode
{
    HumanVsHuman,
    HumanVsAI,
    AIvsAI
}

public enum HumanSide
{
    White,
    Black
}

[DisallowMultipleComponent]
public class ChessMatchCoordinator : MonoBehaviour
{
    [SerializeField] private ChessMatchMode matchMode = ChessMatchMode.HumanVsAI;
    [SerializeField] private HumanSide humanSide = HumanSide.White;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private Board board;
    [SerializeField] private BoardUI boardUI;
    [SerializeField] private MoveSelector moveSelector;
    [SerializeField] private bool autoConfigureInEditor = true;

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

        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
        }

        if (gameManager != null && board != null && gameManager.board != board)
        {
            gameManager.board = board;
        }
    }

    private void ApplyMode()
    {
        AIController[] aiControllers = GetComponents<AIController>();
        if (moveSelector != null)
        {
            moveSelector.AllowHumanInput = matchMode != ChessMatchMode.AIvsAI;
        }

        foreach (AIController aiController in aiControllers)
        {
            aiController.enabled = false;
        }

        switch (matchMode)
        {
            case ChessMatchMode.HumanVsHuman:
                return;
            case ChessMatchMode.HumanVsAI:
                ConfigureHumanVsAI(aiControllers);
                return;
            case ChessMatchMode.AIvsAI:
                ConfigureAIVsAI(aiControllers);
                return;
        }
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
}
