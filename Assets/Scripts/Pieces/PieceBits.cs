public static class PieceBits
{
    public const int None = 0;
    public const int King = 1;
    public const int Pawn = 2;
    public const int Knight = 3;
    public const int Bishop = 4;
    public const int Rook = 5;
    public const int Queen = 6;

    public const int Black = 8;

    public static int CreatePiece(PieceType type, PieceColor color)
    {
        if (type == PieceType.None)
        {
            return None;
        }

        int piece = (int) type;
        return color == PieceColor.Black ? piece | Black : piece;
    }

    public static PieceType GetType(int piece)
    {
        return (PieceType)(piece & 7);
    }

    public static PieceColor GetColor(int piece)
    {
        return isBlack(piece) ? PieceColor.Black : PieceColor.White;
    }

    public static bool isBlack(int piece)
    {
        return !isEmpty(piece) && (piece & Black) != 0;
    }

    public static bool isEmpty(int piece)
    {
        return piece == None;
    }

    public static int GetValue(int piece)
    {
        switch (GetType(piece))
        {
            case PieceType.Pawn: return 100;
            case PieceType.Knight: return 320;
            case PieceType.Bishop: return 330;
            case PieceType.Rook: return 500;
            case PieceType.Queen: return 900;
            case PieceType.King: return 20000;
            default: return 0;
        }
    }
}
