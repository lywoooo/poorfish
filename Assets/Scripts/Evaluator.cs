using TMPro;

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
                if (piece == null || piece.Value.getPieceType() == PieceType.King) continue;

                totalMaterial += GetMaterialValue(piece.Value.getPieceType());
            }
        }

        return totalMaterial < EndgameThreshold;
    }

    public static int Evaluate(BoardState state)
    {
        const int checkmate = 100000;

        // check checkmate
        if (MoveGenerator.isCheckmate(state, PieceColor.White)) return checkmate;
        if (MoveGenerator.isCheckmate(state, PieceColor.Black)) return -checkmate;

        // check stalemate
        if (MoveGenerator.isStalemate(state, PieceColor.White) || MoveGenerator.isStalemate(state, PieceColor.White)) return 0;

        bool endgame = IsEndgame(state);
        int score = 0;

        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                BoardState.BoardPiece? piece = state.whatIsAt(col, row);
                if (piece == null) continue;

                var p = piece.Value;

                PieceType type = p.getPieceType();
                PieceColor color = p.GetPieceColor();

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

    private static int GetMaterialValue(PieceType type)
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
