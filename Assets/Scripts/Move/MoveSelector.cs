using System.Collections.Generic;
using UnityEngine;

public partial class MoveSelector : MonoBehaviour
{
    private static readonly Color SelectedSquareColor = new Color(0.96f, 0.80f, 0.29f, 0.36f);
    private static readonly Color LastMoveColor = new Color(0.96f, 0.80f, 0.29f, 0.28f);
    private static readonly Color MoveDotColor = new Color(0.12f, 0.15f, 0.18f, 0.34f);

    private const float MoveDotScale = 0.28f;
    private const float CaptureOverlayInsetRatio = 0.08f;
    private const float CaptureRingThicknessRatio = 0.12f;
    private const float DragThresholdPixels = 8f;
    private const float DragPointerLift = 0f;
    private const int SelectedSquareSortingOrder = 1;
    private const int LastMoveSortingOrder = 1;
    private const int MoveIndicatorSortingOrder = 4;

    private static Sprite generatedDotSprite;
    private static Sprite generatedRingSprite;

    private BoardUI boardUI;
    private Sprite boardSquareSprite;
    private Vector3 boardSquareScale = Vector3.one;
    private GameObject selectedSquareHighlight;
    private GameObject lastMoveFromHighlight;
    private GameObject lastMoveToHighlight;
    private Vector2Int? lastMoveFromGridPoint;
    private Vector2Int? lastMoveToGridPoint;
    private GameObject movingPiece;
    private GameObject dragCandidatePiece;
    private List<Move> selectedPieceLegalMoves;
    private List<Vector2Int> moveLocations;
    private HashSet<Vector2Int> moveLocationLookup;
    private List<GameObject> locationHighlights;
    private readonly List<GameObject> pooledMoveIndicators = new List<GameObject>(32);
    private Camera cachedCamera;
    private GameManager cachedGameManager;
    [SerializeField] private bool allowHumanInput = true;
    private bool isDraggingPiece;
    private bool clearSelectionOnPointerUp;
    private Vector2Int dragStartGridPoint;
    private Vector3 dragStartWorldPosition;
    private Vector2 dragStartMousePosition;
    private bool visualsInitialized;
    private bool awaitingPromotionChoice;
    private List<Move> pendingPromotionMoves;
    private Vector2Int pendingPromotionGridPoint;
    private readonly Dictionary<Vector2Int, PieceType> promotionOptionLookup = new Dictionary<Vector2Int, PieceType>(4);
    private readonly List<GameObject> promotionOptionObjects = new List<GameObject>(5);
    private bool hasCancelPromotionSquare;
    private Vector2Int cancelPromotionGridPoint;

    public bool AllowHumanInput
    {
        get => allowHumanInput;
        set
        {
            allowHumanInput = value;
            if (!allowHumanInput)
            {
                ClearSelection();
            }

            enabled = allowHumanInput;
        }
    }

    void Awake()
    {
        cachedCamera = Camera.main;
        cachedGameManager = GameManager.instance;
        boardUI = GetComponent<BoardUI>();
        EnsureVisualsInitialized();
    }

    void OnEnable()
    {
        GameManager.MoveApplied += NotifyMoveApplied;
    }

    void OnDisable()
    {
        GameManager.MoveApplied -= NotifyMoveApplied;
    }

    void Start()
    {
        if (!visualsInitialized)
        {
            enabled = false;
        }
    }

    void Update()
    {
        if (cachedGameManager == null)
        {
            cachedGameManager = GameManager.instance;
            if (cachedGameManager == null)
            {
                return;
            }
        }

        if (cachedGameManager.IsGameOver)
        {
            return;
        }

        if (awaitingPromotionChoice)
        {
            HandlePromotionPointerInput();
            return;
        }

        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
            if (cachedCamera == null)
            {
                return;
            }
        }

        bool hasCursorWorldPoint = TryGetCursorWorldPoint(out Vector3 cursorWorldPoint);
        Vector2Int gridPoint = default;
        bool hasGridPoint = hasCursorWorldPoint && Geometry.TryGridFromPoint(cursorWorldPoint, out gridPoint);

        bool consumedPointerDown = false;
        if (UnityEngine.Input.GetMouseButtonDown(0))
        {
            consumedPointerDown = HandlePointerDown(hasGridPoint, gridPoint);
        }

        if (!consumedPointerDown && UnityEngine.Input.GetMouseButton(0))
        {
            HandlePointerHeld(hasCursorWorldPoint, cursorWorldPoint);
        }

        if (!consumedPointerDown && UnityEngine.Input.GetMouseButtonUp(0))
        {
            HandlePointerUp(hasGridPoint, gridPoint);
        }
    }

    public void EnterState()
    {
        EnsureVisualsInitialized();
        enabled = allowHumanInput;
    }

    public void NotifyMoveApplied(Vector2Int fromGridPoint, Vector2Int toGridPoint)
    {
        EnsureVisualsInitialized();
        ClearSelection();
        ShowLastMove(fromGridPoint, toGridPoint);
    }

    public void ResetVisualState()
    {
        ClearSelection();
        lastMoveFromGridPoint = null;
        lastMoveToGridPoint = null;

        if (lastMoveFromHighlight != null)
        {
            lastMoveFromHighlight.SetActive(false);
        }

        if (lastMoveToHighlight != null)
        {
            lastMoveToHighlight.SetActive(false);
        }
    }
}
