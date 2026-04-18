using System.Collections.Generic;
using UnityEngine;

public partial class MoveSelector
{
    private void BeginPromotionChoice(List<Move> promotionMoves)
    {
        awaitingPromotionChoice = true;
        pendingPromotionMoves = promotionMoves;
        pendingPromotionGridPoint = promotionMoves[0].ToVector;
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
        if (UnityEngine.Input.GetMouseButtonDown(1) || UnityEngine.Input.GetKeyDown(KeyCode.Escape))
        {
            CancelPromotionChoice();
            return;
        }

        if (!UnityEngine.Input.GetMouseButtonDown(0))
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
}
