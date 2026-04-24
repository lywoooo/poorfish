using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MatchManagerWindow : EditorWindow
{
    private ChessMatchCoordinator coordinator;
    private EngineProfile whiteProfile;
    private EngineProfile blackProfile;
    private int gameCount = 50;
    private int maxFullMoves = 160;
    private bool recordCsv = true;
    private string csvFileName = "experiment_matches.csv";
    private bool alternateColors = true;
    private bool useEqualPositionFens;
    private string equalPositionFenResource = "equal_positions";
    private bool rerunStalemates;
    private float restartDelay = 0.05f;
    private bool useFixedBatchSeed = true;
    private int batchSeed = 1;
    private bool allowMirrorMatch;
    private bool allowSettingMismatch;
    private Vector2 scrollPosition;
    private GUIStyle titleStyle;
    private GUIStyle accentStyle;
    private GUIStyle labelStyle;
    private GUIStyle mutedStyle;
    private GUIStyle panelStyle;
    private GUIStyle playerNameStyle;
    private GUIStyle buttonStyle;
    private Texture2D panelTexture;
    private Texture2D inputTexture;
    private Texture2D buttonTexture;

    [MenuItem("Poorfish/Match Manager")]
    public static void ShowWindow()
    {
        MatchManagerWindow window = GetWindow<MatchManagerWindow>("Match Manager");
        window.minSize = new Vector2(900f, 620f);
    }

    private void OnEnable()
    {
        FindCoordinator();
    }

    private void OnGUI()
    {
        EnsureStyles();
        EditorGUI.DrawRect(new Rect(0f, 0f, position.width, position.height), new Color(0.11f, 0.12f, 0.12f));

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        EditorGUILayout.BeginVertical(GUILayout.MinHeight(position.height - 24f));

        DrawMainPanel();
        GUILayout.Space(14f);
        DrawPlayerPanel(BlackName, true);
        GUILayout.Space(12f);
        DrawPlayerPanel(WhiteName, false);

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    private void FindCoordinator()
    {
        coordinator = FindFirstObjectByType<ChessMatchCoordinator>();
    }

    private void ApplyConfiguration()
    {
        if (coordinator == null)
        {
            Debug.LogWarning("No ChessMatchCoordinator found for Match Manager.");
            return;
        }

        Undo.RecordObject(coordinator, "Configure Poorfish Batch");
        foreach (AIController aiController in coordinator.GetComponents<AIController>())
        {
            Undo.RecordObject(aiController, "Configure Poorfish Batch AI");
        }

        coordinator.ConfigureBatchFromManager(
            whiteProfile,
            blackProfile,
            gameCount,
            recordCsv,
            csvFileName,
            maxFullMoves,
            alternateColors,
            rerunStalemates,
            restartDelay,
            useEqualPositionFens,
            equalPositionFenResource,
            useFixedBatchSeed,
            batchSeed);

        EditorUtility.SetDirty(coordinator);
        foreach (AIController aiController in coordinator.GetComponents<AIController>())
        {
            EditorUtility.SetDirty(aiController);
        }

        if (!Application.isPlaying)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }

    private string WhiteName => ProfileName(whiteProfile, "White");
    private string BlackName => ProfileName(blackProfile, "Black");

    private int CompletedGames => coordinator != null ? coordinator.CompletedBatchGames : 0;

    private static string ProfileName(EngineProfile profile, string fallback)
    {
        if (profile == null)
        {
            return fallback;
        }

        return string.IsNullOrWhiteSpace(profile.profileName) ? profile.name : profile.profileName.Trim();
    }

    private void DrawMainPanel()
    {
        using (new EditorGUILayout.VerticalScope(panelStyle, GUILayout.MinHeight(430f)))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawControlColumn();
                GUILayout.Space(28f);
                DrawBoardColumn();
            }
        }
    }

    private void DrawControlColumn()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(420f), GUILayout.ExpandHeight(true)))
        {
            EditorGUILayout.LabelField("Match Manager", titleStyle);
            EditorGUILayout.LabelField(BlackName + " vs " + WhiteName, accentStyle);

            GUILayout.Space(28f);
            EditorGUILayout.LabelField("Game number: " + CompletedGames + " / " + TargetGameCount(), labelStyle);
            DrawScoreboard();

            GUILayout.Space(24f);
            EditorGUILayout.LabelField("Settings:", labelStyle);
            EditorGUILayout.LabelField("Max think time: " + ThinkTimeMilliseconds() + " ms", labelStyle);
            EditorGUILayout.LabelField("Max game length: " + maxFullMoves + " moves", labelStyle);
            EditorGUILayout.LabelField("CSV: " + (recordCsv ? csvFileName : "off"), mutedStyle);

            GUILayout.Space(30f);
            DrawBatchControls();

            GUILayout.FlexibleSpace();
            DrawDebugInfo();
        }
    }

    private void DrawBatchControls()
    {
        EditorGUILayout.LabelField("Coordinator", mutedStyle);
        coordinator = (ChessMatchCoordinator)EditorGUILayout.ObjectField(
            coordinator,
            typeof(ChessMatchCoordinator),
            true,
            GUILayout.Height(24f));

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Find", buttonStyle, GUILayout.Width(86f), GUILayout.Height(26f)))
            {
                FindCoordinator();
            }

            using (new EditorGUI.DisabledScope(coordinator == null))
            {
                if (GUILayout.Button("Select", buttonStyle, GUILayout.Width(86f), GUILayout.Height(26f)))
                {
                    Selection.activeObject = coordinator;
                }
            }
        }

        GUILayout.Space(8f);
        DrawProfileField("Black", ref blackProfile);
        DrawProfileField("White", ref whiteProfile);

        GUILayout.Space(8f);
        gameCount = Mathf.Max(1, EditorGUILayout.IntField("Games", gameCount));
        maxFullMoves = Mathf.Max(1, EditorGUILayout.IntField("Max Moves", maxFullMoves));
        restartDelay = Mathf.Max(0f, EditorGUILayout.FloatField("Restart Delay", restartDelay));
        useFixedBatchSeed = EditorGUILayout.Toggle("Fixed Batch Seed", useFixedBatchSeed);
        using (new EditorGUI.DisabledScope(!useFixedBatchSeed))
        {
            batchSeed = EditorGUILayout.IntField("Batch Seed", batchSeed);
        }
        useEqualPositionFens = EditorGUILayout.Toggle("Equal FEN Starts", useEqualPositionFens);
        using (new EditorGUI.DisabledScope(useEqualPositionFens))
        {
            alternateColors = EditorGUILayout.Toggle("Alternate Colors", alternateColors);
        }

        using (new EditorGUI.DisabledScope(!useEqualPositionFens))
        {
            equalPositionFenResource = EditorGUILayout.TextField("FEN Resource", equalPositionFenResource);
        }

        recordCsv = EditorGUILayout.Toggle("Record CSV", recordCsv);
        using (new EditorGUI.DisabledScope(!recordCsv))
        {
            csvFileName = EditorGUILayout.TextField("CSV File", csvFileName);
        }

        rerunStalemates = EditorGUILayout.Toggle("Rerun Stalemates", rerunStalemates);
        allowMirrorMatch = EditorGUILayout.Toggle("Allow Mirror Match", allowMirrorMatch);
        allowSettingMismatch = EditorGUILayout.Toggle("Allow Setting Mismatch", allowSettingMismatch);

        string validationMessage = BuildValidationMessage();
        if (!string.IsNullOrEmpty(validationMessage))
        {
            EditorGUILayout.HelpBox(validationMessage, MessageType.Warning);
        }

        GUILayout.Space(10f);
        using (new EditorGUI.DisabledScope(coordinator == null || !CanStartMatch()))
        {
            if (GUILayout.Button("Start Match", buttonStyle, GUILayout.Height(30f)))
            {
                ApplyConfiguration();
                if (!Application.isPlaying)
                {
                    EditorApplication.EnterPlaymode();
                }
            }
        }

        if (Application.isPlaying && GUILayout.Button("Stop Match", buttonStyle, GUILayout.Height(28f)))
        {
            EditorApplication.ExitPlaymode();
        }
    }

    private void DrawScoreboard()
    {
        string firstEngineName = BlackName == WhiteName ? BlackName + " (Engine 1)" : BlackName;
        string secondEngineName = BlackName == WhiteName ? WhiteName + " (Engine 2)" : WhiteName;

        EditorGUILayout.LabelField(firstEngineName + ": Wins: " + BlackWins + " Losses: " + WhiteWins + " Draws: " + Draws, labelStyle);
        EditorGUILayout.LabelField(secondEngineName + ": Wins: " + WhiteWins + " Losses: " + BlackWins + " Draws: " + Draws, labelStyle);
    }

    private void DrawProfileField(string label, ref EngineProfile profile)
    {
        EditorGUILayout.LabelField(label + " Name", mutedStyle);
        profile = (EngineProfile)EditorGUILayout.ObjectField(
            profile,
            typeof(EngineProfile),
            false,
            GUILayout.Height(24f));
    }

    private void DrawDebugInfo()
    {
        EditorGUILayout.LabelField("Debug Info:", labelStyle);
        EditorGUILayout.LabelField("Scene connected: " + (coordinator != null), labelStyle);
        EditorGUILayout.LabelField("Play mode: " + Application.isPlaying, labelStyle);
        EditorGUILayout.LabelField("Register " + BlackName, mutedStyle);
        EditorGUILayout.LabelField("Register " + WhiteName, mutedStyle);
    }

    private void DrawBoardColumn()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
        {
            EditorGUILayout.LabelField("Black Name", labelStyle);
            Rect boardRect = GUILayoutUtility.GetAspectRect(1f, GUILayout.ExpandWidth(true), GUILayout.MaxWidth(540f));
            DrawBoard(boardRect);
            GUILayout.Space(8f);
            EditorGUILayout.LabelField("White Name", labelStyle);
        }
    }

    private void DrawBoard(Rect rect)
    {
        EditorGUI.DrawRect(new Rect(rect.x - 6f, rect.y - 6f, rect.width + 12f, rect.height + 12f), new Color(0.16f, 0.16f, 0.16f));
        float squareSize = rect.width / 8f;
        for (int rank = 0; rank < 8; rank++)
        {
            for (int file = 0; file < 8; file++)
            {
                Rect square = new Rect(
                    rect.x + file * squareSize,
                    rect.y + rank * squareSize,
                    squareSize,
                    squareSize);
                Color squareColor = ((rank + file) & 1) == 0
                    ? new Color(0.86f, 0.79f, 0.68f)
                    : new Color(0.57f, 0.41f, 0.33f);
                EditorGUI.DrawRect(square, squareColor);
            }
        }
    }

    private void DrawPlayerPanel(string playerName, bool isBlack)
    {
        using (new EditorGUILayout.VerticalScope(panelStyle, GUILayout.Height(132f)))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Name: " + playerName, playerNameStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Open Profile", buttonStyle, GUILayout.Width(130f), GUILayout.Height(30f)))
                {
                    Selection.activeObject = isBlack ? blackProfile : whiteProfile;
                }
            }

            EditorGUILayout.LabelField("connected: " + (isBlack ? blackProfile != null : whiteProfile != null), accentStyle);
            EditorGUILayout.LabelField("Game Info:", accentStyle);
            EditorGUILayout.LabelField("Message Received:", labelStyle);
            EditorGUILayout.LabelField("Message Sent: try register player -> " + playerName, labelStyle);
        }
    }

    private int ThinkTimeMilliseconds()
    {
        EngineProfile profile = blackProfile != null ? blackProfile : whiteProfile;
        if (profile != null)
        {
            return Mathf.RoundToInt(profile.maxThinkTimeSeconds * 1000f);
        }

        return 0;
    }

    private int TargetGameCount()
    {
        int target = Mathf.Max(useEqualPositionFens ? 2 : 1, gameCount);
        return useEqualPositionFens && target % 2 == 1 ? target + 1 : target;
    }

    private int WhiteWins => coordinator != null ? coordinator.WhiteWins : 0;

    private int BlackWins => coordinator != null ? coordinator.BlackWins : 0;

    private int Draws => coordinator != null ? coordinator.Draws : 0;

    private bool CanStartMatch()
    {
        return string.IsNullOrEmpty(BuildValidationMessage());
    }

    private string BuildValidationMessage()
    {
        if (whiteProfile == null || blackProfile == null)
        {
            return "Select both engine profiles before starting a batch.";
        }

        if (whiteProfile == blackProfile && !allowMirrorMatch)
        {
            return "Both sides are using the same profile. Enable Allow Mirror Match if that is intentional.";
        }

        if (!allowSettingMismatch && HasExperimentSettingMismatch(out string mismatchMessage))
        {
            return mismatchMessage + " Enable Allow Setting Mismatch only if that difference is the experiment.";
        }

        if (recordCsv && string.IsNullOrWhiteSpace(csvFileName))
        {
            return "Provide a CSV file name or turn off CSV recording.";
        }

        return string.Empty;
    }

    private bool HasExperimentSettingMismatch(out string mismatchMessage)
    {
        mismatchMessage = string.Empty;

        if (whiteProfile == null || blackProfile == null)
        {
            return false;
        }

        if (whiteProfile.searchDepth != blackProfile.searchDepth)
        {
            mismatchMessage = "Search depth differs between the selected profiles.";
            return true;
        }

        if (!Mathf.Approximately(whiteProfile.maxThinkTimeSeconds, blackProfile.maxThinkTimeSeconds))
        {
            mismatchMessage = "Think time differs between the selected profiles.";
            return true;
        }

        if (whiteProfile.useOpeningBook != blackProfile.useOpeningBook)
        {
            mismatchMessage = "Opening-book usage differs between the selected profiles.";
            return true;
        }

        return false;
    }

    private void EnsureStyles()
    {
        if (panelTexture == null)
        {
            panelTexture = MakeTexture(new Color(0.13f, 0.14f, 0.14f));
        }

        if (inputTexture == null)
        {
            inputTexture = MakeTexture(new Color(0.82f, 0.82f, 0.82f));
        }

        if (buttonTexture == null)
        {
            buttonTexture = MakeTexture(new Color(0.28f, 0.28f, 0.28f));
        }

        titleStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 28,
            normal = { textColor = new Color(0.92f, 0.36f, 0.42f) }
        };

        accentStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            normal = { textColor = new Color(0.31f, 0.95f, 0.31f) }
        };

        labelStyle ??= new GUIStyle(EditorStyles.label)
        {
            fontSize = 15,
            normal = { textColor = new Color(0.86f, 0.86f, 0.86f) }
        };

        mutedStyle ??= new GUIStyle(labelStyle)
        {
            normal = { textColor = new Color(0.64f, 0.66f, 0.66f) }
        };

        panelStyle ??= new GUIStyle
        {
            padding = new RectOffset(22, 22, 18, 18),
            normal = { background = panelTexture }
        };

        playerNameStyle ??= new GUIStyle(titleStyle)
        {
            fontSize = 24,
            normal = { textColor = new Color(0.31f, 0.95f, 0.31f) }
        };

        buttonStyle ??= new GUIStyle(EditorStyles.miniButton)
        {
            fontSize = 14,
            normal =
            {
                background = buttonTexture,
                textColor = new Color(0.9f, 0.9f, 0.9f)
            }
        };
    }

    private static Texture2D MakeTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}
