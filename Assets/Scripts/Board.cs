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
    public Material defaultMaterial;
    public Material selectedMaterial;

    private readonly Dictionary<GameObject, MeshRenderer> rendererCache = new Dictionary<GameObject, MeshRenderer>(32);
    private readonly Dictionary<GameObject, SpriteRenderer> spriteRendererCache = new Dictionary<GameObject, SpriteRenderer>(32);
    private readonly Dictionary<GameObject, Color> defaultSpriteColors = new Dictionary<GameObject, Color>(32);

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
        piece.transform.position = Geometry.PointFromGrid(gridPoint);
    }

    public void SelectPiece(GameObject piece)
    {
        MeshRenderer meshRenderer = GetRenderer(piece);
        if (meshRenderer != null)
        {
            if (selectedMaterial != null)
            {
                meshRenderer.material = selectedMaterial;
            }
            return;
        }

        SpriteRenderer spriteRenderer = GetSpriteRenderer(piece);
        if (spriteRenderer != null)
        {
            if (!defaultSpriteColors.ContainsKey(piece))
            {
                defaultSpriteColors[piece] = spriteRenderer.color;
            }

            spriteRenderer.color = new Color(1f, 0.9f, 0.4f, 1f);
        }
    }

    public void DeselectPiece(GameObject piece)
    {
        MeshRenderer meshRenderer = GetRenderer(piece);
        if (meshRenderer != null)
        {
            if (defaultMaterial != null)
            {
                meshRenderer.material = defaultMaterial;
            }
            return;
        }

        SpriteRenderer spriteRenderer = GetSpriteRenderer(piece);
        if (spriteRenderer != null)
        {
            if (defaultSpriteColors.TryGetValue(piece, out Color defaultColor))
            {
                spriteRenderer.color = defaultColor;
            }
            else
            {
                spriteRenderer.color = Color.white;
            }
        }
    }

    private MeshRenderer GetRenderer(GameObject piece)
    {
        if (!rendererCache.TryGetValue(piece, out MeshRenderer renderer) || renderer == null)
        {
            renderer = piece.GetComponentInChildren<MeshRenderer>();
            rendererCache[piece] = renderer;
        }

        return renderer;
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
}
