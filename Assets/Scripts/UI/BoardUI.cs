using System.Collections.Generic;
using UnityEngine;

public class BoardUI : MonoBehaviour
{
    [SerializeField] public GameObject SquarePrefab;
    [SerializeField] private Color darkColor;
    [SerializeField] private Color lightColor;
    [SerializeField] private Color frameColor = new Color(0.18f, 0.13f, 0.09f, 0.96f);
    [SerializeField] private Color lightCoordinateColor = new Color(0.71f, 0.53f, 0.39f, 1f);
    [SerializeField] private Color darkCoordinateColor = new Color(0.94f, 0.85f, 0.71f, 1f);

    private GameObject[,] Board = new GameObject[8, 8];
    private readonly List<GameObject> coordinateLabels = new List<GameObject>(16);
    private GameObject boardFrame;
    private Font coordinateFont;

    void Awake()
    {
        Geometry.SetCellSize(GetSquareSize());
        coordinateFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

        BuildCoordinates();
    }

    private void ClearVisualBoard()
    {
        if (boardFrame != null)
        {
            Destroy(boardFrame);
            boardFrame = null;
        }

        foreach (GameObject label in coordinateLabels)
        {
            if (label != null)
            {
                Destroy(label);
            }
        }

        coordinateLabels.Clear();

        for (int file = 0; file < 8; file++)
        {
            for (int rank = 0; rank < 8; rank++)
            {
                if (Board[file, rank] != null)
                {
                    Destroy(Board[file, rank]);
                    Board[file, rank] = null;
                }
            }
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

    private void BuildCoordinates()
    {
        for (int file = 0; file < 8; file++)
        {
            Vector3 filePosition = Geometry.PointFromGrid(new Vector2Int(file, 0)) + new Vector3(Geometry.CellSize * 0.32f, -Geometry.CellSize * 0.36f, 0f);
            bool isLightSquare = file % 2 != 0;
            coordinateLabels.Add(CreateCoordinateLabel(((char)('a' + file)).ToString(), filePosition, isLightSquare ? lightCoordinateColor : darkCoordinateColor, TextAnchor.MiddleCenter));
        }

        for (int rank = 0; rank < 8; rank++)
        {
            Vector3 rankPosition = Geometry.PointFromGrid(new Vector2Int(0, rank)) + new Vector3(-Geometry.CellSize * 0.34f, Geometry.CellSize * 0.34f, 0f);
            bool isLightSquare = rank % 2 == 0;
            coordinateLabels.Add(CreateCoordinateLabel((rank + 1).ToString(), rankPosition, isLightSquare ? lightCoordinateColor : darkCoordinateColor, TextAnchor.MiddleCenter));
        }
    }

    private GameObject CreateCoordinateLabel(string text, Vector3 position, Color color, TextAnchor anchor)
    {
        GameObject labelObject = new GameObject($"Coord_{text}");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.position = position;

        TextMesh textMesh = labelObject.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.anchor = anchor;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontSize = 64;
        textMesh.characterSize = 0.06f;
        textMesh.color = color;

        if (coordinateFont != null)
        {
            textMesh.font = coordinateFont;
            MeshRenderer meshRenderer = labelObject.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = coordinateFont.material;
            meshRenderer.sortingOrder = 5;
        }

        return labelObject;
    }
}
