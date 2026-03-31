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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveSelector : MonoBehaviour
{
    public GameObject moveLocationPrefab;
    public GameObject tileHighlightPrefab;
    public GameObject attackLocationPrefab;

    private GameObject tileHighlight;
    private GameObject movingPiece;
    private List<Vector2Int> moveLocations;
    private HashSet<Vector2Int> moveLocationLookup;
    private List<GameObject> locationHighlights;
    private Camera cachedCamera;
    private GameManager cachedGameManager;
    private TileSelector cachedTileSelector;

    void Start ()
    {
        this.enabled = false;
        cachedCamera = Camera.main;
        cachedGameManager = GameManager.instance;
        cachedTileSelector = GetComponent<TileSelector>();
        tileHighlight = Instantiate(tileHighlightPrefab, Geometry.PointFromGrid(new Vector2Int(0, 0)),
            Quaternion.identity, gameObject.transform);
        tileHighlight.SetActive(false);
    }

    void Update ()
    {
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

        Ray ray = cachedCamera.ScreenPointToRay(Input.mousePosition);

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            Vector3 point = hit.point;
            Vector2Int gridPoint = Geometry.GridFromPoint(point);

            tileHighlight.SetActive(true);
            tileHighlight.transform.position = Geometry.PointFromGrid(gridPoint);
            if (Input.GetMouseButtonDown(0))
            {
                GameObject clickedPiece = cachedGameManager.PieceAtGrid(gridPoint);

                if (!cachedGameManager.TouchMoveEnabled() && cachedGameManager.DoesPieceBelongToCurrentPlayer(clickedPiece))
                {
                    if (clickedPiece == movingPiece)
                    {
                        CancelMove();
                    }
                    else
                    {
                        ReselectPiece(clickedPiece);
                    }
                    return;
                }

                // Reference Point 2: check for valid move location
                if (!moveLocationLookup.Contains(gridPoint))
                {
                    if (!cachedGameManager.TouchMoveEnabled())
                    {
                        CancelMove();
                    }
                    return;
                }

                if (cachedGameManager.PieceAtGrid(gridPoint) == null)
                {
                    cachedGameManager.Move(movingPiece, gridPoint);
                }
                else
                {
                    cachedGameManager.CapturePieceAt(gridPoint);
                    cachedGameManager.Move(movingPiece, gridPoint);
                }
                // Reference Point 3: capture enemy piece here later
                ExitState();
            }
        }
        else
        {
            tileHighlight.SetActive(false);
        }
    }

    private void CancelMove()
    {
        this.enabled = false;
        ClearHighlights();
        cachedGameManager.DeselectPiece(movingPiece);
        movingPiece = null;
        cachedTileSelector.EnterState();
    }

    private void ReselectPiece(GameObject piece)
    {
        ClearHighlights();
        cachedGameManager.DeselectPiece(movingPiece);
        movingPiece = piece;
        cachedGameManager.SelectPiece(movingPiece);
        moveLocations = cachedGameManager.MovesForPiece(movingPiece);
        moveLocationLookup = new HashSet<Vector2Int>(moveLocations);
        locationHighlights = new List<GameObject>(moveLocations.Count);

        foreach (Vector2Int loc in moveLocations)
        {
            GameObject highlight = cachedGameManager.PieceAtGrid(loc)
                ? Instantiate(attackLocationPrefab, Geometry.PointFromGrid(loc), Quaternion.identity, gameObject.transform)
                : Instantiate(moveLocationPrefab, Geometry.PointFromGrid(loc), Quaternion.identity, gameObject.transform);
            locationHighlights.Add(highlight);
        }
    }

    public void EnterState(GameObject piece)
    {
        movingPiece = piece;
        this.enabled = true;

        moveLocations = cachedGameManager.MovesForPiece(movingPiece);
        moveLocationLookup = new HashSet<Vector2Int>(moveLocations);
        locationHighlights = new List<GameObject>(moveLocations.Count);

        if (moveLocations.Count == 0)
        {
            CancelMove();
        }

        foreach (Vector2Int loc in moveLocations)
        {
            GameObject highlight;
            if (cachedGameManager.PieceAtGrid(loc))
            {
                highlight = Instantiate(attackLocationPrefab, Geometry.PointFromGrid(loc), Quaternion.identity, gameObject.transform);
            }
            else
            {
                highlight = Instantiate(moveLocationPrefab, Geometry.PointFromGrid(loc), Quaternion.identity, gameObject.transform);
            }
            locationHighlights.Add(highlight);
        }
    }

    private void ExitState()
    {
        this.enabled = false;
        tileHighlight.SetActive(false);
        cachedGameManager.DeselectPiece(movingPiece);
        movingPiece = null;
        if (!cachedGameManager.IsGameOver)
        {
            cachedGameManager.NextPlayer();
            cachedTileSelector.EnterState();
        }
        foreach (GameObject highlight in locationHighlights)
        {
            Destroy(highlight);
        }

        moveLocationLookup?.Clear();
    }

    private void ClearHighlights()
    {
        if (locationHighlights != null)
        {
            foreach (GameObject highlight in locationHighlights)
            {
                Destroy(highlight);
            }

            locationHighlights.Clear();
        }

        moveLocationLookup?.Clear();
    }
}
