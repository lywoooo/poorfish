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

    public static int CreatePiece(int type, bool isBlack)
    {
        if (type == None)
        {
            return None;
        }

        return isBlack ? (type | Black) : type;
    }

    public static int getType(int piece)
    {
        return piece & 7;
    }

    public static bool isBlack(int piece)
    {
        return !isEmpty(piece) && (piece & Black) != 0;
    }

    public static bool isEmpty(int piece)
    {
        return piece == None;
    }

    public static int getValue(int piece)
    {
        switch (getType(piece))
        {
            case Pawn: return 100;
            case Knight: return 250;
            case Bishop: return 300;
            case Rook: return 500;
            case Queen: return 900;
            case King: return 20000;
            default: return 0;
        }
    }
}
