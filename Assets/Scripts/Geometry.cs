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

using UnityEngine;

public class Geometry
{
    private const float DefaultCellSize = 3.2f;
    private const int BoardDimension = 8;

    public static float CellSize { get; private set; } = DefaultCellSize;
    public static float BoardHalfSpan => CellSize * (BoardDimension - 1) * 0.5f;
    public static float BoardExtent => CellSize * BoardDimension * 0.5f;

    public static void SetCellSize(float cellSize)
    {
        CellSize = cellSize > 0f ? cellSize : DefaultCellSize;
    }

    static public Vector3 PointFromGrid(Vector2Int gridPoint)
    {
        float x = -BoardHalfSpan + CellSize * gridPoint.x;
        float y = -BoardHalfSpan + CellSize * gridPoint.y;
        return new Vector3(x, y, 0);
    }

    static public Vector2Int GridPoint(int col, int row)
    {
        return new Vector2Int(col, row);
    }

    static public Vector2Int GridFromPoint(Vector3 point)
    {
        int col = Mathf.FloorToInt((BoardExtent + point.x) / CellSize);
        int row = Mathf.FloorToInt((BoardExtent + point.y) / CellSize);
        return new Vector2Int(col, row);
    }

    static public bool TryGridFromPoint(Vector3 point, out Vector2Int gridPoint)
    {
        if (point.x < -BoardExtent || point.x >= BoardExtent || point.y < -BoardExtent || point.y >= BoardExtent)
        {
            gridPoint = default;
            return false;
        }

        gridPoint = GridFromPoint(point);
        return gridPoint.x >= 0 && gridPoint.x < BoardDimension && gridPoint.y >= 0 && gridPoint.y < BoardDimension;
    }
}
