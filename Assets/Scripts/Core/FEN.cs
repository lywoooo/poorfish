public static class FEN
{
    /* FEN notation: 
    piece placement: capital = white, lowercase = black, num = space between pieces
    e.g. 1r6/5pp1/R1R4p/1r1pP3/2pkQPP1/7P/1P6/2K5 - - - -> 

    active color: w or b
    castling rights: Q and K for white queen and king castling rights etc. 
    possible en passant targets: square of a potential en passant target, even if en passant is not possible in position
    half move clock and full move counter for 50 move draw rule: 100 50
    */
    
    public static bool LoadFen(string fen, out BoardState state)
    {
        
    }

    private static bool ParsePieces(string placement, BoardState state)
    {
        
    }
    private static bool ParseIndividualPiece(char symbol, out PieceType type, out PieceColor color)
    {
        
    }
    private static bool ParseActiveColor(string token, out PieceColor color)
    {
        
    }
    private static bool ParseCastling(string token, out CastlingRights rights)
    {
        
    }
    private static bool ParseSquare(string token, out int square)
    {
        
    }

}