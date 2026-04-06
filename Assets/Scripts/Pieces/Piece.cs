using UnityEngine;

public enum PieceType
{
    None = 0,
    King = 1,
    Pawn = 2,
    Knight = 3,
    Bishop = 4,
    Rook = 5,
    Queen = 6
}

public class Piece : MonoBehaviour
{
    [SerializeField] private PieceType type = PieceType.None;

    public PieceType Type
    {
        get => type;
        set => type = value;
    }
}
