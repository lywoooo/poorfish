using System.Collections.Generic;

public static class PureGameSimulator
{
    private const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    public static SimResult RunSingleGame(
        EngineSettings whiteSettings,
        EngineSettings blackSettings,
        int gameNumber,
        int maxPlies)
    {
        if (!FEN.TryLoadFen(StartFen, out BoardState state))
        {
            return Result(gameNumber, whiteSettings, blackSettings, GameResultType.DrawOther, 0, "Invalid start FEN.");
        }

        var whiteEngine = new MinimaxAB(new ConfigurableEvaluator(
            whiteSettings.evaluationWeights,
            whiteSettings.profileName + "_PureSimEvaluator"));
        var blackEngine = new MinimaxAB(new ConfigurableEvaluator(
            blackSettings.evaluationWeights,
            blackSettings.profileName + "_PureSimEvaluator"));

        var legalMoves = new List<Move>(128);
        var candidateMoves = new List<Move>(128);
        var repetitionCounts = new Dictionary<string, int>(256);
        RegisterPosition(state, repetitionCounts);

        for (int ply = 0; ply < maxPlies; ply++)
        {
            GameResultType terminalResult = EvaluateTerminalState(state, legalMoves, candidateMoves, repetitionCounts);
            if (terminalResult != GameResultType.None)
            {
                return Result(gameNumber, whiteSettings, blackSettings, terminalResult, ply, terminalResult.ToString());
            }

            PieceColor sideToMove = state.currentTurn;
            EngineSettings settings = sideToMove == PieceColor.White ? whiteSettings : blackSettings;
            MinimaxAB engine = sideToMove == PieceColor.White ? whiteEngine : blackEngine;
            SearchResult search = engine.FindBestMove(state, sideToMove, settings);

            if (!search.hasMove)
            {
                return Result(gameNumber, whiteSettings, blackSettings, GameResultType.DrawOther, ply, "Engine returned no move.");
            }

            state.MakeMove(search.bestMove);
            state.switchTurn();
            RegisterPosition(state, repetitionCounts);
        }

        return Result(gameNumber, whiteSettings, blackSettings, GameResultType.DrawOther, maxPlies, "Max plies reached.");
    }

    private static SimResult Result(
        int gameNumber,
        EngineSettings whiteSettings,
        EngineSettings blackSettings,
        GameResultType result,
        int plies,
        string terminationReason)
    {
        return new SimResult(
            gameNumber,
            whiteSettings.profileName,
            blackSettings.profileName,
            result,
            plies,
            terminationReason);
    }

    private static GameResultType EvaluateTerminalState(
        BoardState state,
        List<Move> legalMoves,
        List<Move> candidateMoves,
        Dictionary<string, int> repetitionCounts)
    {
        MoveGenerator.GetLegalMoves(state, state.currentTurn, legalMoves, candidateMoves);
        if (legalMoves.Count == 0)
        {
            if (MoveGenerator.isInCheck(state, state.currentTurn))
            {
                return state.currentTurn == PieceColor.White ? GameResultType.BlackWin : GameResultType.WhiteWin;
            }

            return GameResultType.DrawStalemate;
        }

        if (HasInsufficientMaterial(state))
        {
            return GameResultType.DrawInsufficientMaterial;
        }

        if (state.halfmoveClock >= 100)
        {
            return GameResultType.DrawFiftyMoveRule;
        }

        return repetitionCounts.TryGetValue(PositionKey(state), out int count) && count >= 3
            ? GameResultType.DrawThreefoldRepetition
            : GameResultType.None;
    }

    private static void RegisterPosition(BoardState state, Dictionary<string, int> repetitionCounts)
    {
        string key = PositionKey(state);
        repetitionCounts[key] = repetitionCounts.TryGetValue(key, out int count) ? count + 1 : 1;
    }

    private static string PositionKey(BoardState state)
    {
        var builder = new System.Text.StringBuilder(96);

        for (int square = 0; square < state.board.Length; square++)
        {
            if (square > 0)
            {
                builder.Append(',');
            }

            builder.Append(state.board[square]);
        }

        builder.Append('|');
        builder.Append(state.currentTurn == PieceColor.White ? 'w' : 'b');
        builder.Append('|');
        builder.Append((int)state.castlingRights);
        builder.Append('|');
        builder.Append(EffectiveEnPassantTarget(state));

        return builder.ToString();
    }

    private static int EffectiveEnPassantTarget(BoardState state)
    {
        if (state.enPassantTarget < 0)
        {
            return -1;
        }

        int targetCol = state.enPassantTarget % 8;
        int targetRow = state.enPassantTarget / 8;
        int pawnRow = state.currentTurn == PieceColor.White ? targetRow - 1 : targetRow + 1;

        return HasPawnThatCanCaptureEnPassant(state, targetCol - 1, pawnRow) ||
            HasPawnThatCanCaptureEnPassant(state, targetCol + 1, pawnRow)
                ? state.enPassantTarget
                : -1;
    }

    private static bool HasPawnThatCanCaptureEnPassant(BoardState state, int col, int row)
    {
        if (!BoardState.InBounds(col, row))
        {
            return false;
        }

        int piece = state.whatIsAt(col, row);
        return !PieceBits.isEmpty(piece) &&
            PieceBits.GetType(piece) == PieceType.Pawn &&
            PieceBits.GetColor(piece) == state.currentTurn;
    }

    private static bool HasInsufficientMaterial(BoardState state)
    {
        int whiteKnights = 0;
        int blackKnights = 0;
        var whiteBishopSquareColors = new List<bool>(2);
        var blackBishopSquareColors = new List<bool>(2);

        for (int col = 0; col < 8; col++)
        {
            for (int row = 0; row < 8; row++)
            {
                int piece = state.whatIsAt(col, row);
                if (PieceBits.isEmpty(piece))
                {
                    continue;
                }

                PieceType type = PieceBits.GetType(piece);
                PieceColor color = PieceBits.GetColor(piece);
                switch (type)
                {
                    case PieceType.King:
                        continue;
                    case PieceType.Bishop:
                        if (color == PieceColor.White)
                        {
                            whiteBishopSquareColors.Add(IsLightSquare(col, row));
                        }
                        else
                        {
                            blackBishopSquareColors.Add(IsLightSquare(col, row));
                        }
                        break;
                    case PieceType.Knight:
                        if (color == PieceColor.White)
                        {
                            whiteKnights++;
                        }
                        else
                        {
                            blackKnights++;
                        }
                        break;
                    default:
                        return false;
                }
            }
        }

        int whiteMinorCount = whiteKnights + whiteBishopSquareColors.Count;
        int blackMinorCount = blackKnights + blackBishopSquareColors.Count;

        if (whiteMinorCount == 0 && blackMinorCount == 0)
        {
            return true;
        }

        if (whiteMinorCount == 1 && blackMinorCount == 0)
        {
            return true;
        }

        if (whiteMinorCount == 0 && blackMinorCount == 1)
        {
            return true;
        }

        if (whiteKnights == 0 && blackKnights == 0 &&
            whiteBishopSquareColors.Count == 1 && blackBishopSquareColors.Count == 1)
        {
            return whiteBishopSquareColors[0] == blackBishopSquareColors[0];
        }

        return false;
    }

    private static bool IsLightSquare(int col, int row)
    {
        return (col + row) % 2 != 0;
    }
}
