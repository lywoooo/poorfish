using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoardUI : MonoBehaviour
{
    [SerializeField] private GameObject SquarePrefab;
    [SerializeField] private Color darkColor;
    [SerializeField] private Color lightColor;

    private GameObject[,] Board = new GameObject[8, 8];

    void Start()
    {
        BuildVisualBoard();
    }

    public void BuildVisualBoard()
    {
        for (int file = 0; file < 8; file++)
        {
            for (int rank = 0; rank < 8; rank++)
            {
                bool isLight = (file + rank) % 2 != 0;
                Color squareColor = isLight ? lightColor : darkColor;

                Vector2 position = new Vector2(-1f + file, -1f + rank);

                GameObject square = Instantiate(SquarePrefab, position, Quaternion.identity, transform);
                SpriteRenderer sr = square.GetComponent<SpriteRenderer>();
                sr.color = squareColor;

                square.name = $"Square_{file}_{rank}";
                Board[file, rank] = square; 
            }
        }
    }
}
