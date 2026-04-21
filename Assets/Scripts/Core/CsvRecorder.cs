using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class CsvRecorder : MonoBehaviour
{
    private static readonly string[] GameCsvColumns =
    {
        "batch_id", "game_id", "game_number", "timestamp_utc", "engine_build", "white_engine", "black_engine",
        "starting_fen", "total_plies", "total_moves", "result", "termination_reason", "result_type"
    };

    private static readonly string[] PlyCsvColumns =
    {
        "game_id", "ply", "move_number", "side_to_move", "move_uci", "piece_type", "from_square",
        "to_square", "promotion", "evaluation", "depth", "nodes_searched", "leaf_evaluations",
        "transposition_hits", "alpha_beta_cutoffs", "time_ms", "search_algorithm", "evaluation_version",
        "used_opening_book", "fen_after"
    };

    private static readonly string[] SummaryCsvColumns =
    {
        "batch_id", "timestamp_utc", "white_profile", "black_profile", "target_completed_games",
        "completed_games", "actual_games_played", "white_wins", "black_wins", "draws"
    };

    private readonly List<RecordedMove> recordedMoves = new List<RecordedMove>(256);
    private GameManager gameManager;
    private bool recordingEnabled;
    private string fileName = "ai_vs_ai_matches.csv";
    private string batchId;
    private string batchDirectoryPath;
    private string matchId;
    private string whiteProfileName = "White";
    private string blackProfileName = "Black";
    private int targetCompletedGames = 1;
    private int currentGameNumber;
    private int whiteWins;
    private int blackWins;
    private int draws;
    private int moveNumber;
    private string startingFen;
    private PendingEngineMove pendingEngineMove;

    private struct RecordedMove
    {
        public int ply;
        public int moveNumber;
        public string sideToMove;
        public string moveUci;
        public string fenAfter;
        public string pieceType;
        public string from;
        public string to;
        public string promotion;
        public string searchAlgorithm;
        public string evaluationVersion;
        public int depth;
        public int evaluation;
        public int nodesSearched;
        public int leafEvaluations;
        public int transpositionHits;
        public int alphaBetaCutoffs;
        public float timeMs;
        public bool usedOpeningBook;
    }

    private struct PendingEngineMove
    {
        public bool hasValue;
        public Move move;
        public string sideToMove;
        public SearchResult searchResult;
        public string evaluatorName;
        public bool usedOpeningBook;
    }

    private void OnEnable()
    {
        GameManager.MoveApplied += HandleMoveApplied;
        GameManager.GameEnded += HandleGameEnded;
    }

    private void OnDisable()
    {
        GameManager.MoveApplied -= HandleMoveApplied;
        GameManager.GameEnded -= HandleGameEnded;
    }

    public void Configure(GameManager manager, bool shouldRecord, string configuredFileName, AIController[] aiControllers, int plannedGames)
    {
        gameManager = manager;
        recordingEnabled = shouldRecord;
        fileName = string.IsNullOrWhiteSpace(configuredFileName) ? "ai_vs_ai_matches.csv" : configuredFileName.Trim();
        targetCompletedGames = Mathf.Max(1, plannedGames);
        batchId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        batchDirectoryPath = Path.Combine(GetProjectRootPath(), "SelfPlayLogs", batchId);
        currentGameNumber = 0;
        whiteWins = 0;
        blackWins = 0;
        draws = 0;
        ResolveProfileNames(aiControllers);
        BeginNextGame();
    }

    public void BeginNextGame()
    {
        currentGameNumber++;
        matchId = batchId + "_g" + currentGameNumber.ToString(CultureInfo.InvariantCulture);
        moveNumber = 0;
        recordedMoves.Clear();
        pendingEngineMove = default;
        startingFen = CurrentFenOrEmpty(1);
    }

    public void PrepareEngineMove(
        BoardState stateBefore,
        PieceColor sideToMove,
        Move move,
        SearchResult searchResult,
        EngineSettings settings,
        string evaluatorName,
        bool usedOpeningBook)
    {
        if (!recordingEnabled)
        {
            return;
        }

        if (string.IsNullOrEmpty(startingFen))
        {
            startingFen = FEN.ToFen(stateBefore, 1);
        }

        pendingEngineMove = new PendingEngineMove
        {
            hasValue = true,
            move = move,
            sideToMove = sideToMove.ToString(),
            searchResult = searchResult,
            evaluatorName = evaluatorName,
            usedOpeningBook = usedOpeningBook
        };
    }

    public void FinalizeBatch(int completedGames)
    {
        if (!recordingEnabled)
        {
            return;
        }

        WriteSummaryCsv(completedGames);
        recordingEnabled = false;
    }

    private void ResolveProfileNames(AIController[] aiControllers)
    {
        whiteProfileName = "White";
        blackProfileName = "Black";

        if (aiControllers == null)
        {
            return;
        }

        foreach (AIController aiController in aiControllers)
        {
            if (aiController == null)
            {
                continue;
            }

            string profileName = aiController.engineProfile != null && !string.IsNullOrWhiteSpace(aiController.engineProfile.profileName)
                ? aiController.engineProfile.profileName
                : aiController.fallbackSettings.profileName;

            if (string.IsNullOrWhiteSpace(profileName))
            {
                profileName = aiController.aiStartColorBlack ? "Black" : "White";
            }

            if (aiController.aiStartColorBlack)
            {
                blackProfileName = profileName;
            }
            else
            {
                whiteProfileName = profileName;
            }
        }
    }

    private void HandleMoveApplied(Vector2Int fromGridPoint, Vector2Int toGridPoint)
    {
        if (!recordingEnabled || gameManager == null || gameManager.IsGameOver)
        {
            return;
        }

        GameObject movedPiece = gameManager.PieceAtGrid(toGridPoint);
        PieceType pieceType = movedPiece != null ? gameManager.GetPieceType(movedPiece) : PieceType.None;

        moveNumber++;
        int ply = moveNumber;
        bool hasPending = pendingEngineMove.hasValue
            && pendingEngineMove.move.from == BoardState.SquareIndex(fromGridPoint)
            && pendingEngineMove.move.to == BoardState.SquareIndex(toGridPoint);
        string sideToMove = hasPending ? pendingEngineMove.sideToMove : gameManager.CurrentTurnColor.ToString();
        string fenAfter = CurrentFenAfterMove(sideToMove, FullMoveNumberForPly(ply + 1));
        SearchResult searchResult = hasPending ? pendingEngineMove.searchResult : default;
        string moveUci = hasPending
            ? MoveToUci(pendingEngineMove.move)
            : ToSquareName(fromGridPoint) + ToSquareName(toGridPoint);

        recordedMoves.Add(new RecordedMove
        {
            ply = ply,
            moveNumber = FullMoveNumberForPly(ply),
            sideToMove = sideToMove,
            moveUci = moveUci,
            fenAfter = fenAfter,
            pieceType = pieceType.ToString(),
            from = ToSquareName(fromGridPoint),
            to = ToSquareName(toGridPoint),
            promotion = pieceType == PieceType.Queen || pieceType == PieceType.Rook || pieceType == PieceType.Bishop || pieceType == PieceType.Knight
                ? InferPromotionLabel(fromGridPoint, toGridPoint, pieceType)
                : string.Empty,
            searchAlgorithm = hasPending ? (pendingEngineMove.usedOpeningBook ? "OpeningBook" : "MinimaxAB") : string.Empty,
            evaluationVersion = hasPending ? pendingEngineMove.evaluatorName : string.Empty,
            depth = hasPending ? searchResult.stats.completedDepth : 0,
            evaluation = hasPending ? searchResult.bestScore : 0,
            nodesSearched = hasPending ? searchResult.stats.nodesVisited : 0,
            leafEvaluations = hasPending ? searchResult.stats.leafEvaluations : 0,
            transpositionHits = hasPending ? searchResult.stats.transpositionHits : 0,
            alphaBetaCutoffs = hasPending ? searchResult.stats.alphaBetaCutoffs : 0,
            timeMs = hasPending ? searchResult.stats.elapsedMilliseconds : 0f,
            usedOpeningBook = hasPending && pendingEngineMove.usedOpeningBook
        });

        if (pendingEngineMove.hasValue)
        {
            pendingEngineMove = default;
        }
    }

    private void HandleGameEnded(string result, GameResultType resultType)
    {
        if (!recordingEnabled)
        {
            return;
        }

        WriteMatchToCsv(result, resultType);
        TallyResult(resultType);
    }

    private void WriteMatchToCsv(string result, GameResultType resultType)
    {
        string gameCsvPath = GetBatchFilePath(fileName);
        string plyCsvPath = GetBatchFilePath(PlyFileName());
        bool gameFileExists = File.Exists(gameCsvPath);
        bool plyFileExists = File.Exists(plyCsvPath);
        string timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        using (var writer = new StreamWriter(gameCsvPath, true))
        {
            if (!gameFileExists)
            {
                writer.WriteLine(string.Join(",", GameCsvColumns));
            }

            writer.WriteLine(BuildGameCsvRow(timestamp, result, resultType));
        }

        using (var writer = new StreamWriter(plyCsvPath, true))
        {
            if (!plyFileExists)
            {
                writer.WriteLine(string.Join(",", PlyCsvColumns));
            }

            foreach (RecordedMove recordedMove in recordedMoves)
            {
                writer.WriteLine(BuildPlyCsvRow(recordedMove));
            }
        }

        Debug.Log("AI vs AI game CSV saved to " + gameCsvPath, this);
        Debug.Log("AI vs AI ply CSV saved to " + plyCsvPath, this);
    }

    private void WriteSummaryCsv(int completedGames)
    {
        string summaryPath = GetBatchFilePath(Path.GetFileNameWithoutExtension(fileName) + "_summary.csv");
        bool fileExists = File.Exists(summaryPath);

        using (var writer = new StreamWriter(summaryPath, true))
        {
            if (!fileExists)
            {
                writer.WriteLine(string.Join(",", SummaryCsvColumns));
            }

            writer.WriteLine(ToCsvLine(
                batchId,
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                whiteProfileName,
                blackProfileName,
                FormatInt(targetCompletedGames),
                FormatInt(completedGames),
                FormatInt(currentGameNumber),
                FormatInt(whiteWins),
                FormatInt(blackWins),
                FormatInt(draws)));
        }

        Debug.Log("AI vs AI summary CSV saved to " + summaryPath, this);
    }

    private void TallyResult(GameResultType resultType)
    {
        switch (resultType)
        {
            case GameResultType.WhiteWin:
                whiteWins++;
                return;
            case GameResultType.BlackWin:
                blackWins++;
                return;
            case GameResultType.DrawStalemate:
            case GameResultType.DrawInsufficientMaterial:
            case GameResultType.DrawFiftyMoveRule:
            case GameResultType.DrawThreefoldRepetition:
            case GameResultType.DrawOther:
            default:
                draws++;
                return;
        }
    }

    private string GetBatchFilePath(string targetFileName)
    {
        string directoryPath = string.IsNullOrWhiteSpace(batchDirectoryPath)
            ? Path.Combine(GetProjectRootPath(), "SelfPlayLogs", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture))
            : batchDirectoryPath;

        Directory.CreateDirectory(directoryPath);
        return Path.Combine(directoryPath, targetFileName);
    }

    private string BuildGameCsvRow(
        string timestamp,
        string result,
        GameResultType resultType)
    {
        return ToCsvLine(
            batchId,
            matchId,
            FormatInt(currentGameNumber),
            timestamp,
            Application.version,
            whiteProfileName,
            blackProfileName,
            startingFen,
            FormatInt(recordedMoves.Count),
            FormatInt(recordedMoves.Count == 0 ? 0 : FullMoveNumberForPly(recordedMoves.Count)),
            ResultNotation(resultType),
            result,
            resultType.ToString());
    }

    private string BuildPlyCsvRow(RecordedMove recordedMove)
    {
        return ToCsvLine(
            matchId,
            FormatInt(recordedMove.ply),
            FormatInt(recordedMove.moveNumber),
            recordedMove.sideToMove,
            recordedMove.moveUci,
            recordedMove.pieceType,
            recordedMove.from,
            recordedMove.to,
            recordedMove.promotion,
            FormatInt(recordedMove.evaluation),
            FormatInt(recordedMove.depth),
            FormatInt(recordedMove.nodesSearched),
            FormatInt(recordedMove.leafEvaluations),
            FormatInt(recordedMove.transpositionHits),
            FormatInt(recordedMove.alphaBetaCutoffs),
            recordedMove.timeMs.ToString("F3", CultureInfo.InvariantCulture),
            recordedMove.searchAlgorithm,
            recordedMove.evaluationVersion,
            recordedMove.usedOpeningBook.ToString(),
            recordedMove.fenAfter);
    }

    private static string ToCsvLine(params string[] values)
    {
        string[] escapedValues = new string[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            escapedValues[i] = EscapeCsv(values[i]);
        }

        return string.Join(",", escapedValues);
    }

    private static string FormatInt(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private string PlyFileName()
    {
        string extension = Path.GetExtension(fileName);
        string baseName = Path.GetFileNameWithoutExtension(fileName);

        if (string.IsNullOrEmpty(extension))
        {
            extension = ".csv";
        }

        return baseName + "_plies" + extension;
    }

    private static string GetProjectRootPath()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }

    private static string ToSquareName(Vector2Int gridPoint)
    {
        char file = (char)('a' + gridPoint.x);
        int rank = gridPoint.y + 1;
        return file + rank.ToString(CultureInfo.InvariantCulture);
    }

    private static string InferPromotionLabel(Vector2Int fromGridPoint, Vector2Int toGridPoint, PieceType pieceType)
    {
        bool reachedBackRank = (fromGridPoint.y == 6 && toGridPoint.y == 7) || (fromGridPoint.y == 1 && toGridPoint.y == 0);
        return reachedBackRank ? pieceType.ToString() : string.Empty;
    }

    private static int FullMoveNumberForPly(int ply)
    {
        return Mathf.Max(1, (ply + 1) / 2);
    }

    private string CurrentFenOrEmpty(int fullMoveNumber)
    {
        if (gameManager == null)
        {
            return string.Empty;
        }

        try
        {
            return FEN.ToFen(BoardState.boardSnapshot(), fullMoveNumber);
        }
        catch (NullReferenceException)
        {
            return string.Empty;
        }
    }

    private string CurrentFenAfterMove(string sideToMove, int fullMoveNumber)
    {
        if (gameManager == null)
        {
            return string.Empty;
        }

        try
        {
            BoardState stateAfter = BoardState.boardSnapshot();
            stateAfter.currentTurn = sideToMove == PieceColor.White.ToString()
                ? PieceColor.Black
                : PieceColor.White;
            return FEN.ToFen(stateAfter, fullMoveNumber);
        }
        catch (NullReferenceException)
        {
            return string.Empty;
        }
    }

    private static string MoveToUci(Move move)
    {
        string uci = SquareNameFromIndex(move.from) + SquareNameFromIndex(move.to);
        if (move.isPromotion)
        {
            uci += PromotionSuffix(move.promotionType);
        }

        return uci;
    }

    private static string SquareNameFromIndex(int square)
    {
        if (square < 0 || square >= 64)
        {
            return string.Empty;
        }

        return ToSquareName(new Vector2Int(square % 8, square / 8));
    }

    private static string PromotionSuffix(PieceType promotionType)
    {
        switch (promotionType)
        {
            case PieceType.Queen: return "q";
            case PieceType.Rook: return "r";
            case PieceType.Bishop: return "b";
            case PieceType.Knight: return "n";
            default: return string.Empty;
        }
    }

    private static string ResultNotation(GameResultType resultType)
    {
        switch (resultType)
        {
            case GameResultType.WhiteWin:
                return "1-0";
            case GameResultType.BlackWin:
                return "0-1";
            case GameResultType.DrawStalemate:
            case GameResultType.DrawInsufficientMaterial:
            case GameResultType.DrawFiftyMoveRule:
            case GameResultType.DrawThreefoldRepetition:
            case GameResultType.DrawOther:
                return "1/2-1/2";
            default:
                return "*";
        }
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
