/*
 * Copyright (c) 2018 Razeware LLC
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * Notwithstanding the foregoing, you may not use, copy, modify, merge, publish, 
 * distribute, sublicense, create a derivative work, and/or sell copies of the 
 * Software in any work that is designed, intended, or marketed for pedagogical or 
 * instructional purposes related to programming, coding, application development, 
 * or information technology.  Permission for such use, copying, modification,
 * merger, publication, distribution, sublicensing, creation of derivative works, 
 * or sale is expressly withheld.
 *    
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */
using System.Collections.Generic;
using UnityEngine;

public class Board : MonoBehaviour
{
    [SerializeField] private float moveAnimationDuration = 0.12f;
    [SerializeField] private float dragScaleMultiplier = 1.08f;
    [SerializeField] private int draggingSortingOrder = 10;

    private readonly Dictionary<GameObject, SpriteRenderer> spriteRendererCache = new Dictionary<GameObject, SpriteRenderer>(32);
    private readonly Dictionary<GameObject, Vector3> defaultPieceScales = new Dictionary<GameObject, Vector3>(32);
    private readonly Dictionary<GameObject, int> defaultSortingOrders = new Dictionary<GameObject, int>(32);
    private readonly Dictionary<GameObject, Coroutine> moveAnimations = new Dictionary<GameObject, Coroutine>(32);

    public GameObject AddPiece(GameObject piece, int col, int row)
    {
        Vector2Int gridPoint = Geometry.GridPoint(col, row);
        GameObject newPiece = Instantiate(piece, Geometry.PointFromGrid(gridPoint), Quaternion.identity, gameObject.transform);
        return newPiece;
    }

    public void RemovePiece(GameObject piece)
    {
        Destroy(piece);
    }

    public void MovePiece(GameObject piece, Vector2Int gridPoint)
    {
        Vector3 targetPosition = Geometry.PointFromGrid(gridPoint);
        StopMoveAnimation(piece);

        if (moveAnimationDuration <= 0f)
        {
            piece.transform.position = targetPosition;
            return;
        }

        moveAnimations[piece] = StartCoroutine(AnimatePieceMove(piece, targetPosition));
    }

    public void SetPieceWorldPosition(GameObject piece, Vector3 position)
    {
        if (piece == null)
        {
            return;
        }

        StopMoveAnimation(piece);
        piece.transform.position = new Vector3(position.x, position.y, 0f);
    }

    public void SetPieceDragState(GameObject piece, bool isDragging)
    {
        if (piece == null)
        {
            return;
        }

        if (!defaultPieceScales.ContainsKey(piece))
        {
            defaultPieceScales[piece] = piece.transform.localScale;
        }

        SpriteRenderer spriteRenderer = GetSpriteRenderer(piece);
        if (spriteRenderer != null && !defaultSortingOrders.ContainsKey(piece))
        {
            defaultSortingOrders[piece] = spriteRenderer.sortingOrder;
        }

        piece.transform.localScale = isDragging
            ? defaultPieceScales[piece] * dragScaleMultiplier
            : defaultPieceScales[piece];

        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = isDragging
                ? draggingSortingOrder
                : defaultSortingOrders[piece];
        }
    }

    private SpriteRenderer GetSpriteRenderer(GameObject piece)
    {
        if (!spriteRendererCache.TryGetValue(piece, out SpriteRenderer renderer) || renderer == null)
        {
            renderer = piece.GetComponentInChildren<SpriteRenderer>();
            spriteRendererCache[piece] = renderer;
        }

        return renderer;
    }

    private void StopMoveAnimation(GameObject piece)
    {
        if (piece != null && moveAnimations.TryGetValue(piece, out Coroutine animation) && animation != null)
        {
            StopCoroutine(animation);
            moveAnimations.Remove(piece);
        }
    }

    private System.Collections.IEnumerator AnimatePieceMove(GameObject piece, Vector3 targetPosition)
    {
        Vector3 startPosition = piece.transform.position;
        float elapsed = 0f;

        while (elapsed < moveAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveAnimationDuration);
            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            piece.transform.position = Vector3.Lerp(startPosition, targetPosition, easedT);
            yield return null;
        }

        piece.transform.position = targetPosition;
        moveAnimations.Remove(piece);
    }
}
