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
        state = new BoardState();

        if (string.IsNullOrWhiteSpace(fen))
        {
            return false;
        }

        string[] parts = fen.Trim().Split(' ');
        if (parts.Length < 4)
        {
            return false;
        }

        if (!ParsePieces(parts[0], state))
        {
            return false;
        }

        if (!ParseActiveColor(parts[1], out state.currentTurn))
        {
            return false;
        }

        if (!ParseCastling(parts[2], out state.castlingRights))
        {
            return false;
        }

        if (!ParseSquare(parts[3], out state.enPassantTarget))
        {
            return false;
        }

        state.hasLastMove = false;
        state.lastMove = default;

        return true;
    }

    private static bool ParsePieces(string placement, BoardState state)
    {
        string[] ranks = placement.Split('/');
        if (ranks.Length != 8)
        {
            return false;
        }

        for (int fenRank = 0; fenRank < 8; fenRank++)
        {
            string rankText = ranks[fenRank];
            int row = 7 - fenRank;
            int col = 0;

            foreach (char symbol in rankText)
            {
                if (char.IsDigit(symbol))
                {
                    int emptyCount = symbol - '0';

                    if (emptyCount < 1 || emptyCount > 8)
                    {
                        return false;
                    }

                    col += emptyCount;
                    continue;
                }

                if (!ParseIndividualPiece(symbol, out PieceType type, out PieceColor color))
                {
                    return false;
                }

                if (col >= 8)
                {
                    return false;
                }

                int square = BoardState.SquareIndex(col, row);
                state.board[square] = PieceBits.CreatePiece(type, color);
                col++;
            }

            if (col != 8)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ParseIndividualPiece(char symbol, out PieceType type, out PieceColor color)
    {
        color = char.IsLower(symbol) ? PieceColor.Black : PieceColor.White;

        switch(char.ToLowerInvariant(symbol))
        {
            case 'p' : type = PieceType.Pawn; return true;
            case 'n' : type = PieceType.Knight; return true;
            case 'r' : type = PieceType.Rook; return true;
            case 'b' : type = PieceType.Bishop; return true;
            case 'q' : type = PieceType.Queen; return true;
            case 'k' : type = PieceType.King; return true;
            default : type = PieceType.None; return false;
        }
    }
    private static bool ParseActiveColor(string token, out PieceColor color)
    {
        switch (token)
        {
            case "w" : color = PieceColor.White; return true;
            case "b" : color = PieceColor.Black; return true;
            default : color = default; return false;
        }
    }
    private static bool ParseCastling(string token, out CastlingRights rights)
    {
        rights = CastlingRights.None;

        if (token == "-")
        {
            return true;
        } 

        foreach (char symbol in token)
        {
            switch (symbol)
            {
                case 'K' : rights |= CastlingRights.WhiteKingside; break;
                case 'Q' : rights |= CastlingRights.WhiteQueenside; break;
                case 'k' : rights |= CastlingRights.BlackKingside; break;
                case 'q' : rights |= CastlingRights.BlackQueenside; break;
                default : return false;
            }
        }

        return true;
    }

    private static bool ParseSquare(string token, out int square)
    {
        square = -1; 
        if (token == "-")
        {
            return true;
        }

        if (token.Length != 2)
        {
            return false;
        }

        char file = token[0];
        char rank = token[1];

        int col = file - 'a';
        int row = rank - '1';
        
        if (!BoardState.InBounds(col, row))
        {
            return false;
        }

        square = BoardState.SquareIndex(col, row);
        return true;
    }

}