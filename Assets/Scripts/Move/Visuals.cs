using UnityEngine;

public partial class MoveSelector
{
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
}
