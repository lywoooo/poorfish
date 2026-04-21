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
    
    public static bool TryLoadFen(string fen, out BoardState state)
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

        if (!TryParsePieces(parts[0], state))
        {
            return false;
        }

        if (!TryParseActiveColor(parts[1], out state.currentTurn))
        {
            return false;
        }

        if (!TryParseCastling(parts[2], out state.castlingRights))
        {
            return false;
        }

        if (!TryParseSquareOrNone(parts[3], out state.enPassantTarget))
        {
            return false;
        }

        if (parts.Length >= 5 && !TryParseNonNegativeInt(parts[4], out state.halfmoveClock))
        {
            return false;
        }

        state.hasLastMove = false;
        state.lastMove = default;

        return true;
    }

    public static string ToFen(BoardState state, int fullMoveNumber = 1)
    {
        return BuildPiecePlacement(state)
            + " "
            + (state.currentTurn == PieceColor.White ? "w" : "b")
            + " "
            + CastlingToFen(state.castlingRights)
            + " "
            + SquareToFen(state.enPassantTarget)
            + " "
            + state.halfmoveClock
            + " "
            + (fullMoveNumber < 1 ? 1 : fullMoveNumber);
    }

    private static string BuildPiecePlacement(BoardState state)
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder(64);

        for (int row = 7; row >= 0; row--)
        {
            int emptyCount = 0;
            for (int col = 0; col < 8; col++)
            {
                int piece = state.board[BoardState.SquareIndex(col, row)];
                if (PieceBits.isEmpty(piece))
                {
                    emptyCount++;
                    continue;
                }

                if (emptyCount > 0)
                {
                    builder.Append(emptyCount);
                    emptyCount = 0;
                }

                builder.Append(PieceToFen(piece));
            }

            if (emptyCount > 0)
            {
                builder.Append(emptyCount);
            }

            if (row > 0)
            {
                builder.Append('/');
            }
        }

        return builder.ToString();
    }

    private static char PieceToFen(int piece)
    {
        char symbol;
        switch (PieceBits.GetType(piece))
        {
            case PieceType.Pawn: symbol = 'p'; break;
            case PieceType.Knight: symbol = 'n'; break;
            case PieceType.Bishop: symbol = 'b'; break;
            case PieceType.Rook: symbol = 'r'; break;
            case PieceType.Queen: symbol = 'q'; break;
            case PieceType.King: symbol = 'k'; break;
            default: symbol = '1'; break;
        }

        return PieceBits.GetColor(piece) == PieceColor.White
            ? char.ToUpperInvariant(symbol)
            : symbol;
    }

    private static string CastlingToFen(CastlingRights rights)
    {
        if (rights == CastlingRights.None)
        {
            return "-";
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder(4);
        if ((rights & CastlingRights.WhiteKingside) != 0) builder.Append('K');
        if ((rights & CastlingRights.WhiteQueenside) != 0) builder.Append('Q');
        if ((rights & CastlingRights.BlackKingside) != 0) builder.Append('k');
        if ((rights & CastlingRights.BlackQueenside) != 0) builder.Append('q');
        return builder.Length == 0 ? "-" : builder.ToString();
    }

    private static string SquareToFen(int square)
    {
        if (square < 0)
        {
            return "-";
        }

        char file = (char)('a' + (square % 8));
        int rank = (square / 8) + 1;
        return file + rank.ToString();
    }

    private static bool TryParsePieces(string placement, BoardState state)
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

                if (!TryParsePiece(symbol, out PieceType type, out PieceColor color))
                {
                    return false;
                }

                if (col >= 8)
                {
                    return false;
                }

                int square = BoardState.SquareIndex(col, row);
                state.board[square] = PieceBits.CreatePiece(type, color);
                if (type == PieceType.King)
                {
                    state.SetKingSquare(color, square);
                }
                col++;
            }

            if (col != 8)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParsePiece(char symbol, out PieceType type, out PieceColor color)
    {
        type = PieceType.None;
        color = default;

        switch(char.ToLowerInvariant(symbol))
        {
            case 'p' : type = PieceType.Pawn; break;
            case 'n' : type = PieceType.Knight; break;
            case 'r' : type = PieceType.Rook; break;
            case 'b' : type = PieceType.Bishop; break;
            case 'q' : type = PieceType.Queen; break;
            case 'k' : type = PieceType.King; break;
            default : return false;
        }

        color = char.IsLower(symbol) ? PieceColor.Black : PieceColor.White;
        return true;
    }

    private static bool TryParseActiveColor(string token, out PieceColor color)
    {
        switch (token)
        {
            case "w" : color = PieceColor.White; return true;
            case "b" : color = PieceColor.Black; return true;
            default : color = default; return false;
        }
    }

    private static bool TryParseCastling(string token, out CastlingRights rights)
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

    private static bool TryParseSquareOrNone(string token, out int square)
    {
        if (token == "-")
        {
            square = -1;
            return true;
        }

        return TryParseSquare(token, out square);
    }

    private static bool TryParseSquare(string token, out int square)
    {
        square = -1;

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

    private static bool TryParseNonNegativeInt(string token, out int value)
    {
        return int.TryParse(token, out value) && value >= 0;
    }
}
