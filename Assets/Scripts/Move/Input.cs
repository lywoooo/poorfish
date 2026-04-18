using UnityEngine;

public partial class MoveSelector
{
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
            float dragDistance = ((Vector2)UnityEngine.Input.mousePosition - dragStartMousePosition).sqrMagnitude;
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
    private void PrepareDragCandidate(GameObject piece)
    {
        if (piece == null)
        {
            return;
        }

        dragCandidatePiece = piece;
        dragStartMousePosition = UnityEngine.Input.mousePosition;
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
        worldPoint = cachedCamera.ScreenToWorldPoint(new Vector3(UnityEngine.Input.mousePosition.x, UnityEngine.Input.mousePosition.y, distanceToBoard));
        worldPoint.z = 0f;
        return true;
    }
}
