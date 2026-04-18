using UnityEngine;

public sealed class EngineStatsOverlay : MonoBehaviour
{
    private const int MateThreshold = 800000;
    private const int PanelWidth = 420;
    private const int Padding = 18;
    private const int LineHeight = 42;

    private static EngineStatsOverlay instance;
    private static EngineStatsSnapshot snapshot = EngineStatsSnapshot.Waiting;

    private GUIStyle lineStyle;
    private GUIStyle smallLineStyle;
    private Texture2D backgroundTexture;

    private readonly Color depthColor = new Color(1f, 0.28f, 0.33f);
    private readonly Color positionsColor = new Color(1f, 0.63f, 0.32f);
    private readonly Color matesColor = new Color(0.63f, 1f, 0.36f);
    private readonly Color transpositionColor = new Color(0.55f, 0.78f, 1f);
    private readonly Color moveColor = new Color(0.78f, 0.52f, 1f);
    private readonly Color evalColor = new Color(1f, 0.93f, 0.46f);
    private readonly Color detailColor = new Color(0.84f, 0.89f, 0.94f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateOverlay()
    {
        if (instance != null)
        {
            return;
        }

        GameObject overlayObject = new GameObject("Engine Stats Overlay");
        DontDestroyOnLoad(overlayObject);
        instance = overlayObject.AddComponent<EngineStatsOverlay>();
    }

    public static void ShowThinking(PieceColor aiColor, string profileName)
    {
        snapshot = EngineStatsSnapshot.Thinking(aiColor, profileName);
    }

    public static void ShowResult(
        SearchResult result,
        PieceColor aiColor,
        string profileName,
        string evaluatorName,
        bool usedOpeningBook)
    {
        snapshot = EngineStatsSnapshot.FromResult(result, aiColor, profileName, evaluatorName, usedOpeningBook);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnGUI()
    {
        EnsureStyles();

        int lines = snapshot.isThinking ? 3 : 10;
        float panelHeight = Padding * 2 + lines * LineHeight;
        float x = Mathf.Max(16f, Screen.width - PanelWidth - 24f);
        Rect panelRect = new Rect(x, 24f, PanelWidth, panelHeight);

        GUI.DrawTexture(panelRect, backgroundTexture);

        float y = panelRect.y + Padding;
        if (snapshot.isThinking)
        {
            string statusText = snapshot.profileName == "Waiting" ? "Waiting..." : "Searching...";
            DrawLine(panelRect.x + Padding, ref y, statusText, depthColor, lineStyle);
            DrawLine(panelRect.x + Padding, ref y, "Side: " + snapshot.aiColorName, moveColor, smallLineStyle);
            DrawLine(panelRect.x + Padding, ref y, "Profile: " + snapshot.profileName, detailColor, smallLineStyle);
            return;
        }

        DrawLine(panelRect.x + Padding, ref y, "Depth searched: " + snapshot.depth, depthColor, lineStyle);
        DrawLine(panelRect.x + Padding, ref y, "Positions evaluated: " + snapshot.positionsEvaluated, positionsColor, lineStyle);
        DrawLine(panelRect.x + Padding, ref y, "Checkmates found: " + snapshot.checkmatesFound, matesColor, lineStyle);
        DrawLine(panelRect.x + Padding, ref y, "Transpositions: " + snapshot.transpositions, transpositionColor, lineStyle);
        DrawLine(panelRect.x + Padding, ref y, "Move: " + snapshot.moveText, moveColor, lineStyle);
        DrawLine(panelRect.x + Padding, ref y, "Eval: " + snapshot.evaluationText, evalColor, lineStyle);
        DrawLine(panelRect.x + Padding, ref y, "Leaf evals: " + snapshot.leafEvaluations, detailColor, smallLineStyle);
        DrawLine(panelRect.x + Padding, ref y, "Cutoffs: " + snapshot.alphaBetaCutoffs, detailColor, smallLineStyle);
        DrawLine(panelRect.x + Padding, ref y, "Time: " + snapshot.elapsedText, detailColor, smallLineStyle);
        DrawLine(panelRect.x + Padding, ref y, "Profile: " + snapshot.profileName, detailColor, smallLineStyle);
    }

    private void EnsureStyles()
    {
        if (lineStyle != null)
        {
            return;
        }

        lineStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Clip
        };

        smallLineStyle = new GUIStyle(lineStyle)
        {
            fontSize = 21,
            fontStyle = FontStyle.Normal
        };

        backgroundTexture = new Texture2D(1, 1);
        backgroundTexture.SetPixel(0, 0, new Color(0.02f, 0.02f, 0.02f, 0.78f));
        backgroundTexture.Apply();
    }

    private static void DrawLine(float x, ref float y, string text, Color color, GUIStyle style)
    {
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.Label(new Rect(x, y, PanelWidth - Padding * 2, LineHeight), text, style);
        GUI.color = previousColor;
        y += LineHeight;
    }

    private readonly struct EngineStatsSnapshot
    {
        public static EngineStatsSnapshot Waiting => new EngineStatsSnapshot(
            true,
            "AI",
            "Waiting",
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            0,
            0,
            0,
            0);

        public readonly bool isThinking;
        public readonly string aiColorName;
        public readonly string profileName;
        public readonly string moveText;
        public readonly string evaluationText;
        public readonly string elapsedText;
        public readonly int depth;
        public readonly int positionsEvaluated;
        public readonly int leafEvaluations;
        public readonly int checkmatesFound;
        public readonly int transpositions;
        public readonly int alphaBetaCutoffs;

        private EngineStatsSnapshot(
            bool isThinking,
            string aiColorName,
            string profileName,
            string moveText,
            string evaluationText,
            string elapsedText,
            int depth,
            int positionsEvaluated,
            int leafEvaluations,
            int checkmatesFound,
            int transpositions,
            int alphaBetaCutoffs)
        {
            this.isThinking = isThinking;
            this.aiColorName = aiColorName;
            this.profileName = profileName;
            this.moveText = moveText;
            this.evaluationText = evaluationText;
            this.elapsedText = elapsedText;
            this.depth = depth;
            this.positionsEvaluated = positionsEvaluated;
            this.leafEvaluations = leafEvaluations;
            this.checkmatesFound = checkmatesFound;
            this.transpositions = transpositions;
            this.alphaBetaCutoffs = alphaBetaCutoffs;
        }

        public static EngineStatsSnapshot Thinking(PieceColor aiColor, string profileName)
        {
            return new EngineStatsSnapshot(
                true,
                aiColor.ToString(),
                profileName,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                0,
                0,
                0,
                0,
                0);
        }

        public static EngineStatsSnapshot FromResult(
            SearchResult result,
            PieceColor aiColor,
            string profileName,
            string evaluatorName,
            bool usedOpeningBook)
        {
            SearchStats stats = result.stats;
            string sourceText = usedOpeningBook ? "Book" : evaluatorName;
            return new EngineStatsSnapshot(
                false,
                aiColor.ToString(),
                profileName + " / " + sourceText,
                FormatMove(result.bestMove),
                FormatEvaluation(result.bestScore),
                stats.elapsedMilliseconds.ToString("F1") + " ms",
                stats.completedDepth,
                stats.nodesVisited,
                stats.leafEvaluations,
                stats.checkmatesFound,
                stats.transpositionHits,
                stats.alphaBetaCutoffs);
        }

        private static string FormatEvaluation(int score)
        {
            if (score >= MateThreshold)
            {
                return "Mate for White";
            }

            if (score <= -MateThreshold)
            {
                return "Mate for Black";
            }

            float pawns = score / 100f;
            return pawns >= 0f ? "+" + pawns.ToString("F2") : pawns.ToString("F2");
        }

        private static string FormatMove(Move move)
        {
            string text = FormatSquare(move.from) + "-" + FormatSquare(move.to);
            if (move.isPromotion)
            {
                text += "=" + PromotionLetter(move.promotionType);
            }

            return text;
        }

        private static string FormatSquare(int square)
        {
            int file = Mathf.Clamp(square % 8, 0, 7);
            int rank = Mathf.Clamp(square / 8, 0, 7) + 1;
            return ((char)('a' + file)).ToString() + rank;
        }

        private static char PromotionLetter(PieceType type)
        {
            switch (type)
            {
                case PieceType.Queen:
                    return 'Q';
                case PieceType.Rook:
                    return 'R';
                case PieceType.Bishop:
                    return 'B';
                case PieceType.Knight:
                    return 'N';
                default:
                    return '?';
            }
        }
    }
}
