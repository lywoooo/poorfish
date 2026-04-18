using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class CsvRecorder : MonoBehaviour
{
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

    private struct RecordedMove
    {
        public int moveNumber;
        public string playerColor;
        public string pieceType;
        public string from;
        public string to;
        public string promotion;
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
        recordedMoves.Add(new RecordedMove
        {
            moveNumber = moveNumber,
            playerColor = gameManager.CurrentTurnColor.ToString(),
            pieceType = pieceType.ToString(),
            from = ToSquareName(fromGridPoint),
            to = ToSquareName(toGridPoint),
            promotion = pieceType == PieceType.Queen || pieceType == PieceType.Rook || pieceType == PieceType.Bishop || pieceType == PieceType.Knight
                ? InferPromotionLabel(fromGridPoint, toGridPoint, pieceType)
                : string.Empty
        });
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
        string csvPath = GetBatchFilePath(fileName);
        bool fileExists = File.Exists(csvPath);

        using (var writer = new StreamWriter(csvPath, true))
        {
            if (!fileExists)
            {
                writer.WriteLine("batch_id,game_number,match_id,timestamp_utc,white_profile,black_profile,move_number,player_color,piece_type,from_square,to_square,promotion,result,result_type");
            }

            string timestamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            string resultTypeLabel = resultType.ToString();

            if (recordedMoves.Count == 0)
            {
                writer.WriteLine(string.Join(",",
                    EscapeCsv(batchId),
                    currentGameNumber.ToString(CultureInfo.InvariantCulture),
                    EscapeCsv(matchId),
                    EscapeCsv(timestamp),
                    EscapeCsv(whiteProfileName),
                    EscapeCsv(blackProfileName),
                    "0",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    EscapeCsv(result),
                    EscapeCsv(resultTypeLabel)));
            }
            else
            {
                foreach (RecordedMove recordedMove in recordedMoves)
                {
                    writer.WriteLine(string.Join(",",
                        EscapeCsv(batchId),
                        currentGameNumber.ToString(CultureInfo.InvariantCulture),
                        EscapeCsv(matchId),
                        EscapeCsv(timestamp),
                        EscapeCsv(whiteProfileName),
                        EscapeCsv(blackProfileName),
                        recordedMove.moveNumber.ToString(CultureInfo.InvariantCulture),
                        EscapeCsv(recordedMove.playerColor),
                        EscapeCsv(recordedMove.pieceType),
                        EscapeCsv(recordedMove.from),
                        EscapeCsv(recordedMove.to),
                        EscapeCsv(recordedMove.promotion),
                        EscapeCsv(result),
                        EscapeCsv(resultTypeLabel)));
                }
            }
        }

        Debug.Log("AI vs AI CSV saved to " + csvPath, this);
    }

    private void WriteSummaryCsv(int completedGames)
    {
        string summaryPath = GetBatchFilePath(Path.GetFileNameWithoutExtension(fileName) + "_summary.csv");
        bool fileExists = File.Exists(summaryPath);

        using (var writer = new StreamWriter(summaryPath, true))
        {
            if (!fileExists)
            {
                writer.WriteLine("batch_id,timestamp_utc,white_profile,black_profile,target_completed_games,completed_games,actual_games_played,white_wins,black_wins,draws");
            }

            writer.WriteLine(string.Join(",",
                EscapeCsv(batchId),
                EscapeCsv(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)),
                EscapeCsv(whiteProfileName),
                EscapeCsv(blackProfileName),
                targetCompletedGames.ToString(CultureInfo.InvariantCulture),
                completedGames.ToString(CultureInfo.InvariantCulture),
                currentGameNumber.ToString(CultureInfo.InvariantCulture),
                whiteWins.ToString(CultureInfo.InvariantCulture),
                blackWins.ToString(CultureInfo.InvariantCulture),
                draws.ToString(CultureInfo.InvariantCulture)));
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
