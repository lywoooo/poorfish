using System.Collections.Generic;
using UnityEngine;

public class BoardUI : MonoBehaviour
{
    [SerializeField] public GameObject SquarePrefab;
    [SerializeField] private Color darkColor;
    [SerializeField] private Color lightColor;
    [SerializeField] private Color frameColor;
    private GameObject[,] Board = new GameObject[8, 8];
    private GameObject boardFrame;

    void OnValidate()
    {
        if (SquarePrefab == null)
        {
            Debug.LogError("BoardUI is missing its SquarePrefab reference.", this);
            return;
        }

        SpriteRenderer spriteRenderer = SquarePrefab.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            Debug.LogError("BoardUI SquarePrefab needs a SpriteRenderer with a sprite.", SquarePrefab);
        }

        Geometry.SetCellSize(GetSquareSize());
    }

    void Awake()
    {
        Geometry.SetCellSize(GetSquareSize());
    }

    void Start()
    {
        BuildVisualBoard();
    }

    public void BuildVisualBoard()
    {
        ClearVisualBoard();
        BuildBoardBacking();

        for (int file = 0; file < 8; file++)
        {
            for (int rank = 0; rank < 8; rank++)
            {
                bool isLight = (file + rank) % 2 != 0;
                Color squareColor = isLight ? lightColor : darkColor;
                Vector3 position = Geometry.PointFromGrid(new Vector2Int(file, rank));
                GameObject square = Instantiate(SquarePrefab, position, Quaternion.identity, transform);
                SpriteRenderer sr = square.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = squareColor;
                }

                square.name = $"Square_{file}_{rank}";
                Board[file, rank] = square;
            }
        }
    }

    private void ClearVisualBoard()
    {
        if (boardFrame != null)
        {
            DestroyObject(boardFrame);
            boardFrame = null;
        }

        for (int file = 0; file < 8; file++)
        {
            for (int rank = 0; rank < 8; rank++)
            {
                if (Board[file, rank] != null)
                {
                    DestroyObject(Board[file, rank]);
                    Board[file, rank] = null;
                }
            }
        }
    }

    private void DestroyObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private float GetSquareSize()
    {
        if (SquarePrefab == null)
        {
            return Geometry.CellSize;
        }

        SpriteRenderer spriteRenderer = SquarePrefab.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            return spriteRenderer.sprite.bounds.size.x * SquarePrefab.transform.localScale.x;
        }

        BoxCollider2D collider2D = SquarePrefab.GetComponent<BoxCollider2D>();
        if (collider2D != null)
        {
            return collider2D.size.x * SquarePrefab.transform.localScale.x;
        }

        BoxCollider collider = SquarePrefab.GetComponent<BoxCollider>();
        if (collider != null)
        {
            return collider.size.x * SquarePrefab.transform.localScale.x;
        }

        return Geometry.CellSize;
    }

    private void BuildBoardBacking()
    {
        if (SquarePrefab == null)
        {
            return;
        }

        boardFrame = Instantiate(SquarePrefab, Vector3.zero, Quaternion.identity, transform);
        ConfigureBacking(boardFrame, frameColor, Geometry.CellSize * 8.18f, -1);
    }

    private void ConfigureBacking(GameObject backing, Color color, float size, int sortingOrder)
    {
        if (backing == null)
        {
            return;
        }

        backing.name = "BoardBacking";
        float scaleMultiplier = size / Geometry.CellSize;
        Vector3 baseScale = backing.transform.localScale;
        backing.transform.localScale = new Vector3(
            baseScale.x * scaleMultiplier,
            baseScale.y * scaleMultiplier,
            baseScale.z);

        SpriteRenderer spriteRenderer = backing.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
            spriteRenderer.sortingOrder = sortingOrder;
        }

        Collider2D collider2D = backing.GetComponent<Collider2D>();
        if (collider2D != null)
        {
            collider2D.enabled = false;
        }
    }

}
