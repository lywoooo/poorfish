using System.Collections.Generic;
using UnityEngine;

public class MoveSelector : MonoBehaviour
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
        if (Input.GetMouseButtonDown(0))
        {
            consumedPointerDown = HandlePointerDown(hasGridPoint, gridPoint);
        }

        if (!consumedPointerDown && Input.GetMouseButton(0))
        {
            HandlePointerHeld(hasCursorWorldPoint, cursorWorldPoint);
        }

        if (!consumedPointerDown && Input.GetMouseButtonUp(0))
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

    private void EnsureVisualsInitialized()
    {
        if (visualsInitialized)
        {
            return;
        }

        boardUI = GetComponent<BoardUI>();
        if (!TryCacheBoardSquareVisual())
        {
            Debug.LogError("MoveSelector could not find a usable square sprite from BoardUI.", this);
            return;
        }

        selectedSquareHighlight = CreateSquareOverlay("SelectedSquare", SelectedSquareColor, SelectedSquareSortingOrder);
        lastMoveFromHighlight = CreateSquareOverlay("LastMoveFrom", LastMoveColor, LastMoveSortingOrder);
        lastMoveToHighlight = CreateSquareOverlay("LastMoveTo", LastMoveColor, LastMoveSortingOrder);
        visualsInitialized = true;
    }

    private bool TryCacheBoardSquareVisual()
    {
        if (boardUI == null || boardUI.SquarePrefab == null)
        {
            return false;
        }

        SpriteRenderer squareRenderer = boardUI.SquarePrefab.GetComponent<SpriteRenderer>();
        if (squareRenderer == null || squareRenderer.sprite == null)
        {
            return false;
        }

        boardSquareSprite = squareRenderer.sprite;
        boardSquareScale = boardUI.SquarePrefab.transform.localScale;
        return true;
    }

    private bool HandlePointerDown(bool hasGridPoint, Vector2Int gridPoint)
    {
        if (!hasGridPoint)
        {
            if (movingPiece != null)
            {
                clearSelectionOnPointerUp = true;
            }
            return false;
        }

        GameObject pieceAtGrid = cachedGameManager.PieceAtGrid(gridPoint);

        if (movingPiece == null)
        {
            if (cachedGameManager.DoesPieceBelongToCurrentPlayer(pieceAtGrid))
            {
                SelectPiece(pieceAtGrid);
                PrepareDragCandidate(pieceAtGrid);
            }
            return false;
        }

        if (cachedGameManager.DoesPieceBelongToCurrentPlayer(pieceAtGrid))
        {
            SelectPiece(pieceAtGrid);
            PrepareDragCandidate(pieceAtGrid);
            return false;
        }

        if (moveLocationLookup != null && moveLocationLookup.Contains(gridPoint))
        {
            ExecuteMove(gridPoint);
            return true;
        }

        clearSelectionOnPointerUp = true;
        return false;
    }

    private void HandlePointerHeld(bool hasCursorWorldPoint, Vector3 cursorWorldPoint)
    {
        if (dragCandidatePiece == null || movingPiece == null)
        {
            return;
        }

        if (!isDraggingPiece)
        {
            float dragDistance = ((Vector2)Input.mousePosition - dragStartMousePosition).sqrMagnitude;
            if (dragDistance < DragThresholdPixels * DragThresholdPixels)
            {
                return;
            }

            BeginDrag();
        }

        if (hasCursorWorldPoint)
        {
            MoveDraggedPieceToCursor(cursorWorldPoint);
        }
    }

    private void HandlePointerUp(bool hasGridPoint, Vector2Int gridPoint)
    {
        dragCandidatePiece = null;

        if (!isDraggingPiece)
        {
            if (clearSelectionOnPointerUp)
            {
                ClearSelection();
            }

            clearSelectionOnPointerUp = false;
            return;
        }

        clearSelectionOnPointerUp = false;
        cachedGameManager.board.SetPieceDragState(movingPiece, false);
        isDraggingPiece = false;

        if (hasGridPoint && moveLocationLookup != null && moveLocationLookup.Contains(gridPoint))
        {
            ExecuteMove(gridPoint);
            return;
        }

        cachedGameManager.board.SetPieceWorldPosition(movingPiece, dragStartWorldPosition);
        ShowSelectedSquare(dragStartGridPoint);
    }

    private void SelectPiece(GameObject piece)
    {
        ClearHighlights();
        movingPiece = piece;
        clearSelectionOnPointerUp = false;
        dragStartGridPoint = cachedGameManager.GridForPiece(movingPiece);
        dragStartWorldPosition = Geometry.PointFromGrid(dragStartGridPoint);
        ShowSelectedSquare(dragStartGridPoint);

        selectedPieceLegalMoves = cachedGameManager.LegalMovesForPiece(movingPiece) ?? new List<Move>(0);
        moveLocations = new List<Vector2Int>(selectedPieceLegalMoves.Count);
        moveLocationLookup = new HashSet<Vector2Int>(selectedPieceLegalMoves.Count);
        locationHighlights = new List<GameObject>(selectedPieceLegalMoves.Count);

        if (selectedPieceLegalMoves.Count == 0)
        {
            ClearSelection();
            return;
        }

        foreach (Move move in selectedPieceLegalMoves)
        {
            if (!moveLocationLookup.Add(move.to))
            {
                continue;
            }

            moveLocations.Add(move.to);
            bool isCapture = cachedGameManager.PieceAtGrid(move.to) != null || move.isEnPassant;
            GameObject highlight = GetMoveIndicator(move.to, isCapture);
            if (highlight != null)
            {
                locationHighlights.Add(highlight);
            }
        }
    }

    private void ExecuteMove(Vector2Int gridPoint)
    {
        if (movingPiece == null)
        {
            return;
        }

        if (isDraggingPiece)
        {
            cachedGameManager.board.SetPieceDragState(movingPiece, false);
            isDraggingPiece = false;
        }

        List<Move> candidateMoves = selectedPieceLegalMoves ?? cachedGameManager.LegalMovesForPiece(movingPiece);
        List<Move> matchingMoves = new List<Move>();
        foreach (Move candidateMove in candidateMoves)
        {
            if (candidateMove.to == gridPoint)
            {
                matchingMoves.Add(candidateMove);
            }
        }

        if (matchingMoves.Count == 0)
        {
            cachedGameManager.board.SetPieceWorldPosition(movingPiece, dragStartWorldPosition);
            ShowSelectedSquare(dragStartGridPoint);
            return;
        }

        Move move = matchingMoves[0];
        if (matchingMoves.Count > 1 && matchingMoves[0].promotionType != PieceType.None)
        {
            BeginPromotionChoice(matchingMoves);
            return;
        }

        cachedGameManager.ApplyMove(move);
        FinishMove();
    }

    private void FinishMove()
    {
        ClearSelection();
        HidePromotionPrompt();

        if (!cachedGameManager.IsGameOver)
        {
            cachedGameManager.NextPlayer();
        }
    }

    private void ClearSelection()
    {
        ClearHighlights();

        if (movingPiece != null)
        {
            cachedGameManager.board.SetPieceDragState(movingPiece, false);
            movingPiece = null;
        }

        dragCandidatePiece = null;
        isDraggingPiece = false;
        clearSelectionOnPointerUp = false;
        awaitingPromotionChoice = false;
        pendingPromotionMoves = null;
        HidePromotionPrompt();

        if (selectedSquareHighlight != null)
        {
            selectedSquareHighlight.SetActive(false);
        }
    }

    private void ClearHighlights()
    {
        if (locationHighlights != null)
        {
            foreach (GameObject highlight in locationHighlights)
            {
                if (highlight != null)
                {
                    highlight.SetActive(false);
                    pooledMoveIndicators.Add(highlight);
                }
            }

            locationHighlights.Clear();
        }

        moveLocations?.Clear();
        selectedPieceLegalMoves?.Clear();
        selectedPieceLegalMoves = null;
        moveLocationLookup?.Clear();
        moveLocationLookup = null;
    }

    private void ShowLastMove(Vector2Int fromGridPoint, Vector2Int toGridPoint)
    {
        lastMoveFromGridPoint = fromGridPoint;
        lastMoveToGridPoint = toGridPoint;

        if (lastMoveFromHighlight != null)
        {
            lastMoveFromHighlight.transform.position = Geometry.PointFromGrid(fromGridPoint);
            lastMoveFromHighlight.SetActive(true);
        }

        if (lastMoveToHighlight != null)
        {
            lastMoveToHighlight.transform.position = Geometry.PointFromGrid(toGridPoint);
            lastMoveToHighlight.SetActive(true);
        }
    }

    private GameObject CreateMoveIndicator(Vector2Int gridPoint, bool isCapture)
    {
        return isCapture
            ? CreateCaptureOverlay(gridPoint)
            : CreateMoveDot(gridPoint);
    }

    private GameObject GetMoveIndicator(Vector2Int gridPoint, bool isCapture)
    {
        GameObject indicator = null;
        if (pooledMoveIndicators.Count > 0)
        {
            int lastIndex = pooledMoveIndicators.Count - 1;
            indicator = pooledMoveIndicators[lastIndex];
            pooledMoveIndicators.RemoveAt(lastIndex);
        }

        if (indicator == null)
        {
            indicator = CreateMoveIndicator(gridPoint, isCapture);
        }
        else
        {
            ConfigureMoveIndicator(indicator, gridPoint, isCapture);
            indicator.SetActive(true);
        }

        return indicator;
    }

    private GameObject CreateCaptureOverlay(Vector2Int gridPoint)
    {
        GameObject overlay = new GameObject($"CaptureOverlay_{gridPoint.x}_{gridPoint.y}");
        overlay.transform.SetParent(transform, false);
        overlay.AddComponent<SpriteRenderer>();
        ConfigureMoveIndicator(overlay, gridPoint, true);
        return overlay;
    }

    private void BeginPromotionChoice(List<Move> promotionMoves)
    {
        awaitingPromotionChoice = true;
        pendingPromotionMoves = promotionMoves;
        pendingPromotionGridPoint = promotionMoves[0].to;
        ShowPromotionPrompt();
    }

    private void ShowPromotionPrompt()
    {
        RebuildPromotionPrompt();
    }

    private void HidePromotionPrompt()
    {
        foreach (GameObject promotionObject in promotionOptionObjects)
        {
            if (promotionObject != null)
            {
                Destroy(promotionObject);
            }
        }

        promotionOptionObjects.Clear();
        promotionOptionLookup.Clear();
        hasCancelPromotionSquare = false;
    }

    private void RebuildPromotionPrompt()
    {
        HidePromotionPrompt();

        if (cachedGameManager == null || movingPiece == null)
        {
            return;
        }

        PieceColor color = cachedGameManager.GetPieceColor(movingPiece);
        int direction = pendingPromotionGridPoint.y >= 4 ? -1 : 1;
        PieceType[] promotionOrder = { PieceType.Queen, PieceType.Knight, PieceType.Rook, PieceType.Bishop };

        for (int i = 0; i < promotionOrder.Length; i++)
        {
            Vector2Int optionGrid = new Vector2Int(pendingPromotionGridPoint.x, pendingPromotionGridPoint.y + (direction * i));
            promotionOptionLookup[optionGrid] = promotionOrder[i];
            CreatePromotionOptionObject(optionGrid, cachedGameManager.SpriteForPiece(promotionOrder[i], color));
        }

        Vector2Int cancelGrid = new Vector2Int(pendingPromotionGridPoint.x, pendingPromotionGridPoint.y + (direction * promotionOrder.Length));
        if (BoardState.InBounds(cancelGrid.x, cancelGrid.y))
        {
            hasCancelPromotionSquare = true;
            cancelPromotionGridPoint = cancelGrid;
            CreatePromotionCancelObject(cancelGrid);
        }
    }

    private void HandlePromotionPointerInput()
    {
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPromotionChoice();
            return;
        }

        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (!TryGetCursorWorldPoint(out Vector3 cursorWorldPoint))
        {
            CancelPromotionChoice();
            return;
        }

        if (!Geometry.TryGridFromPoint(cursorWorldPoint, out Vector2Int gridPoint))
        {
            CancelPromotionChoice();
            return;
        }

        if (promotionOptionLookup.TryGetValue(gridPoint, out PieceType promotionType))
        {
            ChoosePromotion(promotionType);
            return;
        }

        if (hasCancelPromotionSquare && gridPoint == cancelPromotionGridPoint)
        {
            CancelPromotionChoice();
            return;
        }

        CancelPromotionChoice();
    }

    private void ChoosePromotion(PieceType promotionType)
    {
        if (pendingPromotionMoves == null)
        {
            return;
        }

        foreach (Move promotionMove in pendingPromotionMoves)
        {
            if (promotionMove.promotionType != promotionType)
            {
                continue;
            }

            awaitingPromotionChoice = false;
            pendingPromotionMoves = null;
            pendingPromotionGridPoint = default;
            HidePromotionPrompt();
            cachedGameManager.ApplyMove(promotionMove);
            FinishMove();
            return;
        }
    }

    private void CancelPromotionChoice()
    {
        awaitingPromotionChoice = false;
        pendingPromotionMoves = null;
        pendingPromotionGridPoint = default;
        HidePromotionPrompt();
        if (movingPiece != null)
        {
            cachedGameManager.board.SetPieceWorldPosition(movingPiece, dragStartWorldPosition);
            ShowSelectedSquare(dragStartGridPoint);
        }
    }

    private GameObject CreatePromotionOptionObject(Vector2Int gridPoint, Sprite pieceSprite)
    {
        GameObject optionObject = CreateSquareOverlay($"PromotionOption_{gridPoint.x}_{gridPoint.y}", Color.white, MoveIndicatorSortingOrder + 1);
        optionObject.transform.position = Geometry.PointFromGrid(gridPoint);
        optionObject.SetActive(true);

        GameObject iconObject = new GameObject("Icon");
        iconObject.transform.SetParent(optionObject.transform, false);
        SpriteRenderer iconRenderer = iconObject.AddComponent<SpriteRenderer>();
        iconRenderer.sprite = pieceSprite;
        iconRenderer.sortingOrder = MoveIndicatorSortingOrder + 2;
        FitSpriteToSquare(iconObject.transform, pieceSprite, 0.72f);

        promotionOptionObjects.Add(optionObject);
        return optionObject;
    }

    private GameObject CreatePromotionCancelObject(Vector2Int gridPoint)
    {
        GameObject cancelObject = CreateSquareOverlay($"PromotionCancel_{gridPoint.x}_{gridPoint.y}", new Color(0.9f, 0.9f, 0.9f, 1f), MoveIndicatorSortingOrder + 1);
        cancelObject.transform.position = Geometry.PointFromGrid(gridPoint);
        ApplyInsetScale(cancelObject.transform, 0.72f);
        cancelObject.SetActive(true);

        GameObject iconObject = new GameObject("CancelIcon");
        iconObject.transform.SetParent(cancelObject.transform, false);
        TextMesh textMesh = iconObject.AddComponent<TextMesh>();
        textMesh.text = "\u00D7";
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontSize = 64;
        textMesh.characterSize = 0.13f;
        textMesh.color = new Color(0.45f, 0.45f, 0.45f, 1f);
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            textMesh.font = font;
            MeshRenderer meshRenderer = iconObject.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = font.material;
            meshRenderer.sortingOrder = MoveIndicatorSortingOrder + 2;
        }

        promotionOptionObjects.Add(cancelObject);
        return cancelObject;
    }

    private void FitSpriteToSquare(Transform iconTransform, Sprite sprite, float squareFillRatio)
    {
        if (sprite == null)
        {
            return;
        }

        Vector3 spriteSize = sprite.bounds.size;
        if (spriteSize.x <= Mathf.Epsilon || spriteSize.y <= Mathf.Epsilon)
        {
            return;
        }

        Vector3 parentScale = iconTransform.parent != null ? iconTransform.parent.lossyScale : Vector3.one;
        float targetSize = Geometry.CellSize * squareFillRatio;
        float xScale = targetSize / (spriteSize.x * Mathf.Max(parentScale.x, Mathf.Epsilon));
        float yScale = targetSize / (spriteSize.y * Mathf.Max(parentScale.y, Mathf.Epsilon));
        float uniformScale = Mathf.Min(xScale, yScale);
        iconTransform.localScale = new Vector3(uniformScale, uniformScale, 1f);
    }

    private GameObject CreateMoveDot(Vector2Int gridPoint)
    {
        GameObject dot = new GameObject($"MoveDot_{gridPoint.x}_{gridPoint.y}");
        dot.transform.SetParent(transform, false);
        dot.AddComponent<SpriteRenderer>();
        ConfigureMoveIndicator(dot, gridPoint, false);
        return dot;
    }

    private void ConfigureMoveIndicator(GameObject indicator, Vector2Int gridPoint, bool isCapture)
    {
        indicator.name = (isCapture ? "CaptureOverlay_" : "MoveDot_") + gridPoint.x + "_" + gridPoint.y;
        indicator.transform.position = Geometry.PointFromGrid(gridPoint);

        SpriteRenderer spriteRenderer = indicator.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = indicator.AddComponent<SpriteRenderer>();
        }

        spriteRenderer.sprite = isCapture ? GetGeneratedRingSprite() : GetGeneratedDotSprite();
        spriteRenderer.color = MoveDotColor;
        spriteRenderer.sortingOrder = MoveIndicatorSortingOrder;

        if (isCapture)
        {
            float ringSize = Geometry.CellSize * (1f - (CaptureOverlayInsetRatio * 2f));
            indicator.transform.localScale = new Vector3(ringSize, ringSize, 1f);
        }
        else
        {
            float dotSize = Geometry.CellSize * MoveDotScale;
            indicator.transform.localScale = new Vector3(dotSize, dotSize, 1f);
        }
    }

    private GameObject CreateSquareOverlay(string objectName, Color color, int sortingOrder)
    {
        GameObject overlay = new GameObject(objectName);
        overlay.transform.SetParent(transform, false);
        overlay.transform.localScale = boardSquareScale;

        SpriteRenderer spriteRenderer = overlay.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = boardSquareSprite;
        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = sortingOrder;

        overlay.SetActive(false);
        return overlay;
    }

    private void ApplyInsetScale(Transform targetTransform, float scaleMultiplier)
    {
        targetTransform.localScale = new Vector3(
            boardSquareScale.x * scaleMultiplier,
            boardSquareScale.y * scaleMultiplier,
            boardSquareScale.z);
    }

    private Sprite GetGeneratedDotSprite()
    {
        if (generatedDotSprite != null)
        {
            return generatedDotSprite;
        }

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f;
        float feather = 1.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01((radius - distance) / feather);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        generatedDotSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return generatedDotSprite;
    }

    private Sprite GetGeneratedRingSprite()
    {
        if (generatedRingSprite != null)
        {
            return generatedRingSprite;
        }

        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outerRadius = size * 0.5f;
        float innerRadius = outerRadius * (1f - CaptureRingThicknessRatio * 2f);
        float feather = 1.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float outerAlpha = Mathf.Clamp01((outerRadius - distance) / feather);
                float innerAlpha = Mathf.Clamp01((innerRadius - distance) / feather);
                float alpha = Mathf.Clamp01(outerAlpha - innerAlpha);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        generatedRingSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return generatedRingSprite;
    }

    private void ShowSelectedSquare(Vector2Int gridPoint)
    {
        if (selectedSquareHighlight == null)
        {
            return;
        }

        if (lastMoveFromHighlight != null && lastMoveFromGridPoint.HasValue)
        {
            lastMoveFromHighlight.SetActive(lastMoveFromGridPoint.Value != gridPoint);
        }

        if (lastMoveToHighlight != null && lastMoveToGridPoint.HasValue)
        {
            lastMoveToHighlight.SetActive(lastMoveToGridPoint.Value != gridPoint);
        }

        selectedSquareHighlight.transform.position = Geometry.PointFromGrid(gridPoint);
        selectedSquareHighlight.SetActive(true);
    }

    private void PrepareDragCandidate(GameObject piece)
    {
        if (piece == null)
        {
            return;
        }

        dragCandidatePiece = piece;
        dragStartMousePosition = Input.mousePosition;
        dragStartGridPoint = cachedGameManager.GridForPiece(piece);
        dragStartWorldPosition = Geometry.PointFromGrid(dragStartGridPoint);
    }

    private void BeginDrag()
    {
        if (movingPiece == null)
        {
            return;
        }

        isDraggingPiece = true;
        cachedGameManager.board.SetPieceDragState(movingPiece, true);
        ShowSelectedSquare(dragStartGridPoint);
    }

    private void BeginDragAtCursor(bool hasCursorWorldPoint, Vector3 cursorWorldPoint)
    {
        BeginDrag();

        if (hasCursorWorldPoint)
        {
            MoveDraggedPieceToCursor(cursorWorldPoint);
        }
    }

    private void MoveDraggedPieceToCursor(Vector3 cursorWorldPoint)
    {
        if (movingPiece == null)
        {
            return;
        }

        Vector3 liftedPosition = cursorWorldPoint + Vector3.up * (Geometry.CellSize * DragPointerLift);
        cachedGameManager.board.SetPieceWorldPosition(movingPiece, liftedPosition);
    }

    private bool TryGetCursorWorldPoint(out Vector3 worldPoint)
    {
        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }

        if (cachedCamera == null)
        {
            worldPoint = default;
            return false;
        }

        float distanceToBoard = -cachedCamera.transform.position.z;
        worldPoint = cachedCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, distanceToBoard));
        worldPoint.z = 0f;
        return true;
    }
}
