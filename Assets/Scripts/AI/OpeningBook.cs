using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class OpeningBook
{
    private const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    private readonly Dictionary<string, List<Move>> movesByPosition = new Dictionary<string, List<Move>>(128);
    private readonly List<Move> legalMoves = new List<Move>(64);
    private readonly List<Move> candidateMoves = new List<Move>(64);

    public OpeningBook()
    {
        AddLine("e2e4", "e7e5", "g1f3", "b8c6", "f1b5", "a7a6", "b5a4", "g8f6");
        AddLine("e2e4", "c7c5", "g1f3", "d7d6", "d2d4", "c5d4", "f3d4", "g8f6", "b1c3", "a7a6");
        AddLine("e2e4", "e7e6", "d2d4", "d7d5", "b1c3", "g8f6", "e4e5");
        AddLine("e2e4", "c7c6", "d2d4", "d7d5", "b1c3", "d5e4", "c3e4");
        AddLine("d2d4", "d7d5", "c2c4", "e7e6", "b1c3", "g8f6", "c1g5");
        AddLine("d2d4", "g8f6", "c2c4", "g7g6", "b1c3", "f8g7", "e2e4", "d7d6");
        AddLine("c2c4", "e7e5", "b1c3", "g8f6", "g1f3", "b8c6", "g2g3");
        AddLine("g1f3", "d7d5", "d2d4", "g8f6", "c2c4", "e7e6", "g2g3");
    }

    public bool TryGetBookMove(BoardState state, out Move move)
    {
        move = default;

        string key = BuildPositionKey(state);
        if (!movesByPosition.TryGetValue(key, out List<Move> bookMoves) || bookMoves.Count == 0)
        {
            return false;
        }

        MoveGenerator.GetLegalMoves(state, state.currentTurn, legalMoves, candidateMoves);
        if (legalMoves.Count == 0)
        {
            return false;
        }

        int startIndex = Random.Range(0, bookMoves.Count);
        for (int i = 0; i < bookMoves.Count; i++)
        {
            Move bookMove = bookMoves[(startIndex + i) % bookMoves.Count];
            if (TryFindMatchingLegalMove(bookMove, legalMoves, out move))
            {
                return true;
            }
        }

        return false;
    }

    private void AddLine(params string[] moves)
    {
        if (!FEN.TryLoadFen(StartFen, out BoardState state))
        {
            return;
        }

        foreach (string moveText in moves)
        {
            MoveGenerator.GetLegalMoves(state, state.currentTurn, legalMoves, candidateMoves);
            if (!TryParseBookMove(moveText, legalMoves, out Move move))
            {
                return;
            }

            AddMove(BuildPositionKey(state), move);
            state.MakeMove(move);
            state.switchTurn();
        }
    }

    private void AddMove(string key, Move move)
    {
        if (!movesByPosition.TryGetValue(key, out List<Move> moves))
        {
            moves = new List<Move>(4);
            movesByPosition[key] = moves;
        }

        foreach (Move existingMove in moves)
        {
            if (SameMove(existingMove, move))
            {
                return;
            }
        }

        moves.Add(move);
    }

    private static bool TryParseBookMove(string moveText, List<Move> legalMoves, out Move move)
    {
        move = default;

        if (string.IsNullOrWhiteSpace(moveText) || moveText.Length < 4)
        {
            return false;
        }

        if (!TryParseSquare(moveText.Substring(0, 2), out int from) ||
            !TryParseSquare(moveText.Substring(2, 2), out int to))
        {
            return false;
        }

        PieceType promotionType = PieceType.None;
        if (moveText.Length >= 5 && !TryParsePromotion(moveText[4], out promotionType))
        {
            return false;
        }

        foreach (Move legalMove in legalMoves)
        {
            if (legalMove.from == from &&
                legalMove.to == to &&
                (promotionType == PieceType.None || legalMove.promotionType == promotionType))
            {
                move = legalMove;
                return true;
            }
        }

        return false;
    }

    private static bool TryFindMatchingLegalMove(Move bookMove, List<Move> legalMoves, out Move move)
    {
        foreach (Move legalMove in legalMoves)
        {
            if (SameMove(bookMove, legalMove))
            {
                move = legalMove;
                return true;
            }
        }

        move = default;
        return false;
    }

    private static bool SameMove(Move left, Move right)
    {
        return left.from == right.from
            && left.to == right.to
            && left.promotionType == right.promotionType;
    }

    private static bool TryParseSquare(string squareText, out int square)
    {
        square = -1;
        if (squareText.Length != 2)
        {
            return false;
        }

        int col = squareText[0] - 'a';
        int row = squareText[1] - '1';
        if (!BoardState.InBounds(col, row))
        {
            return false;
        }

        square = BoardState.SquareIndex(col, row);
        return true;
    }

    private static bool TryParsePromotion(char promotionText, out PieceType promotionType)
    {
        switch (char.ToLowerInvariant(promotionText))
        {
            case 'q': promotionType = PieceType.Queen; return true;
            case 'r': promotionType = PieceType.Rook; return true;
            case 'b': promotionType = PieceType.Bishop; return true;
            case 'n': promotionType = PieceType.Knight; return true;
            default: promotionType = PieceType.None; return false;
        }
    }

    private static string BuildPositionKey(BoardState state)
    {
        var builder = new StringBuilder(96);
        AppendPiecePlacement(state, builder);
        builder.Append(' ');
        builder.Append(state.currentTurn == PieceColor.White ? 'w' : 'b');
        builder.Append(' ');
        AppendCastlingRights(state.castlingRights, builder);
        builder.Append(' ');
        AppendSquareOrNone(state.enPassantTarget, builder);
        return builder.ToString();
    }

    private static void AppendPiecePlacement(BoardState state, StringBuilder builder)
    {
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

                builder.Append(PieceToFenChar(piece));
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
    }

    private static void AppendCastlingRights(CastlingRights rights, StringBuilder builder)
    {
        if (rights == CastlingRights.None)
        {
            builder.Append('-');
            return;
        }

        if ((rights & CastlingRights.WhiteKingside) != 0) builder.Append('K');
        if ((rights & CastlingRights.WhiteQueenside) != 0) builder.Append('Q');
        if ((rights & CastlingRights.BlackKingside) != 0) builder.Append('k');
        if ((rights & CastlingRights.BlackQueenside) != 0) builder.Append('q');
    }

    private static void AppendSquareOrNone(int square, StringBuilder builder)
    {
        if (square < 0)
        {
            builder.Append('-');
            return;
        }

        builder.Append((char)('a' + square % 8));
        builder.Append((char)('1' + square / 8));
    }

    private static char PieceToFenChar(int piece)
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

        return PieceBits.GetColor(piece) == PieceColor.White ? char.ToUpperInvariant(symbol) : symbol;
    }
}
