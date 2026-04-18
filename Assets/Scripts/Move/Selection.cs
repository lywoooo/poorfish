using System.Collections.Generic;
using UnityEngine;

public partial class MoveSelector
{
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
            Vector2Int destination = move.ToVector;
            if (!moveLocationLookup.Add(destination))
            {
                continue;
            }

            moveLocations.Add(destination);
            bool isCapture = cachedGameManager.PieceAtGrid(destination) != null || move.isEnPassant;
            GameObject highlight = GetMoveIndicator(destination, isCapture);
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
        int destinationSquare = BoardState.SquareIndex(gridPoint);
        foreach (Move candidateMove in candidateMoves)
        {
            if (candidateMove.to == destinationSquare)
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
}
