public static class Evaluator
{
    // source chessprogramming wiki SEF for values
    private const int ValPawn   = 100;
    private const int ValKnight = 320;
    private const int ValBishop = 330;
    private const int ValRook   = 500;
    private const int ValQueen  = 900;
    private const int ValKing   = 20000;

    private const int EndgameThreshold = 1500;

    public static bool IsEndgame(BoardState state)
    {
        int totalMaterial = 0;

        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                var piece = state.whatIsAt(col, row);
                if (piece == null || piece.Value.type == PieceType.King) continue;

                totalMaterial += GetMaterialValue(piece.Value.type);
            }
        }

        return totalMaterial < EndgameThreshold;
    }

    public static int Evaluate(BoardState state)
    {
        bool endgame = IsEndgame(state);
        int score = 0;

        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                BoardState.BoardPiece? piece = state.whatIsAt(col, row);
                if (piece == null) continue;

                var p = piece.Value;

                PieceType type = p.type;
                PieceColor color = p.color;

                int material = GetMaterialValue(type);
                int pst = PieceSquareTables.GetPST(type, color, col, row, endgame);

                if (color == PieceColor.White)
                    score += material + pst;
                else
                    score -= material + pst;
            }
        }

        return score;
    }

    public static int GetMaterialValue(PieceType type)
    {
        return type switch
        {
            PieceType.Pawn   => ValPawn,
            PieceType.Knight => ValKnight,
            PieceType.Bishop => ValBishop,
            PieceType.Rook   => ValRook,
            PieceType.Queen  => ValQueen,
            PieceType.King   => ValKing,
            _                => 0
        };
    }
}
