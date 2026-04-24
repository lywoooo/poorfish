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

public enum PieceColor {White, Black}

public class Piece : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sr; 

    public int PieceCode { get; private set; }
    public PieceType Type
    {
        get => PieceBits.GetType(PieceCode);
        set => PieceCode = PieceBits.CreatePiece(value, color);
    }
    public PieceType type => Type;
    public PieceColor color => PieceBits.GetColor(PieceCode);

    public void setPiece(int PieceCode, Sprite sprite)
    {
        this.PieceCode = PieceCode;

        if (sr == null)
        {
            sr = GetComponent<SpriteRenderer>();
        }

        if (sr != null)
        {
            sr.sprite = sprite; 
            sr.enabled = PieceCode != PieceBits.None; 
        }

        gameObject.name = PieceCode == PieceBits.None ? "EmptyPiece" : $"{color}_{type}";
    }
}
