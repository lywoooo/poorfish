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
    private const float DragPointerLift = 0.18f;
    private const int SelectedSquareSortingOrder = 2;
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
    private GameObject movingPiece;
    private GameObject dragCandidatePiece;
    private List<Vector2Int> moveLocations;
    private HashSet<Vector2Int> moveLocationLookup;
    private List<GameObject> locationHighlights;
    private Camera cachedCamera;
    private GameManager cachedGameManager;
    private bool isDraggingPiece;
    private bool clearSelectionOnPointerUp;
    private Vector2Int dragStartGridPoint;
    private Vector3 dragStartWorldPosition;
    private Vector2 dragStartMousePosition;

    void Start()
    {
        cachedCamera = Camera.main;
        cachedGameManager = GameManager.instance;
        boardUI = GetComponent<BoardUI>();

        if (!TryCacheBoardSquareVisual())
        {
            Debug.LogError("MoveSelector could not find a usable square sprite from BoardUI.", this);
            enabled = false;
            return;
        }

        selectedSquareHighlight = CreateSquareOverlay("SelectedSquare", SelectedSquareColor, SelectedSquareSortingOrder);
        lastMoveFromHighlight = CreateSquareOverlay("LastMoveFrom", LastMoveColor, LastMoveSortingOrder);
        lastMoveToHighlight = CreateSquareOverlay("LastMoveTo", LastMoveColor, LastMoveSortingOrder);
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

        if (Input.GetMouseButtonDown(0))
        {
            HandlePointerDown(hasGridPoint, gridPoint);
        }

        if (Input.GetMouseButton(0))
        {
            HandlePointerHeld(hasCursorWorldPoint, cursorWorldPoint);
        }

        if (Input.GetMouseButtonUp(0))
        {
            HandlePointerUp(hasGridPoint, gridPoint);
        }
    }

    public void EnterState()
    {
        enabled = true;
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

    private void HandlePointerDown(bool hasGridPoint, Vector2Int gridPoint)
    {
        if (!hasGridPoint)
        {
            if (movingPiece != null)
            {
                clearSelectionOnPointerUp = true;
            }
            return;
        }

        GameObject pieceAtGrid = cachedGameManager.PieceAtGrid(gridPoint);

        if (movingPiece == null)
        {
            if (cachedGameManager.DoesPieceBelongToCurrentPlayer(pieceAtGrid))
            {
                SelectPiece(pieceAtGrid);
                PrepareDragCandidate(pieceAtGrid);
            }
            return;
        }

        if (cachedGameManager.DoesPieceBelongToCurrentPlayer(pieceAtGrid))
        {
            SelectPiece(pieceAtGrid);
            PrepareDragCandidate(pieceAtGrid);
            return;
        }

        if (moveLocationLookup != null && moveLocationLookup.Contains(gridPoint))
        {
            ExecuteMove(gridPoint);
            return;
        }

        clearSelectionOnPointerUp = true;
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
            Vector3 liftedPosition = cursorWorldPoint + Vector3.up * (Geometry.CellSize * DragPointerLift);
            cachedGameManager.board.SetPieceWorldPosition(movingPiece, liftedPosition);
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

        moveLocations = cachedGameManager.MovesForPiece(movingPiece) ?? new List<Vector2Int>(0);
        moveLocationLookup = new HashSet<Vector2Int>(moveLocations);
        locationHighlights = new List<GameObject>(moveLocations.Count);

        if (moveLocations.Count == 0)
        {
            ClearSelection();
            return;
        }

        foreach (Vector2Int loc in moveLocations)
        {
            bool isCapture = cachedGameManager.PieceAtGrid(loc) != null;
            GameObject highlight = CreateMoveIndicator(loc, isCapture);
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

        Vector2Int startGridPoint = cachedGameManager.GridForPiece(movingPiece);
        GameObject clickedPiece = cachedGameManager.PieceAtGrid(gridPoint);

        if (isDraggingPiece)
        {
            cachedGameManager.board.SetPieceDragState(movingPiece, false);
            isDraggingPiece = false;
        }

        if (clickedPiece == null)
        {
            cachedGameManager.Move(movingPiece, gridPoint);
        }
        else
        {
            cachedGameManager.CapturePieceAt(gridPoint);
            cachedGameManager.Move(movingPiece, gridPoint);
        }

        FinishMove(startGridPoint, gridPoint);
    }

    private void FinishMove(Vector2Int fromGridPoint, Vector2Int toGridPoint)
    {
        ClearSelection();
        ShowLastMove(fromGridPoint, toGridPoint);

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
                    Destroy(highlight);
                }
            }

            locationHighlights.Clear();
        }

        moveLocations?.Clear();
        moveLocationLookup?.Clear();
        moveLocationLookup = null;
    }

    private void ShowLastMove(Vector2Int fromGridPoint, Vector2Int toGridPoint)
    {
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

    private GameObject CreateCaptureOverlay(Vector2Int gridPoint)
    {
        GameObject overlay = new GameObject($"CaptureOverlay_{gridPoint.x}_{gridPoint.y}");
        overlay.transform.SetParent(transform, false);
        overlay.transform.position = Geometry.PointFromGrid(gridPoint);

        SpriteRenderer spriteRenderer = overlay.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetGeneratedRingSprite();
        spriteRenderer.color = MoveDotColor;
        spriteRenderer.sortingOrder = MoveIndicatorSortingOrder;

        float ringSize = Geometry.CellSize * (1f - (CaptureOverlayInsetRatio * 2f));
        overlay.transform.localScale = new Vector3(ringSize, ringSize, 1f);

        return overlay;
    }

    private GameObject CreateMoveDot(Vector2Int gridPoint)
    {
        GameObject dot = new GameObject($"MoveDot_{gridPoint.x}_{gridPoint.y}");
        dot.transform.SetParent(transform, false);
        dot.transform.position = Geometry.PointFromGrid(gridPoint);

        SpriteRenderer spriteRenderer = dot.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetGeneratedDotSprite();
        spriteRenderer.color = MoveDotColor;
        spriteRenderer.sortingOrder = MoveIndicatorSortingOrder;

        float dotSize = Geometry.CellSize * MoveDotScale;
        dot.transform.localScale = new Vector3(dotSize, dotSize, 1f);

        return dot;
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

    private bool TryGetCursorWorldPoint(out Vector3 worldPoint)
    {
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
