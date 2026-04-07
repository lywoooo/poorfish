using UnityEngine;

public class BoardUI : MonoBehaviour
{
    [SerializeField] private GameObject SquarePrefab;
    [SerializeField] private Color darkColor;
    [SerializeField] private Color lightColor;

    private GameObject[,] Board = new GameObject[8, 8];

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

        BoxCollider collider = SquarePrefab.GetComponent<BoxCollider>();
        if (collider != null)
        {
            return collider.size.x * SquarePrefab.transform.localScale.x;
        }

        SpriteRenderer spriteRenderer = SquarePrefab.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            return spriteRenderer.sprite.bounds.size.x * SquarePrefab.transform.localScale.x;
        }

        return Geometry.CellSize;
    }
}
