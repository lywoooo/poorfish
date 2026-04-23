using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PoorfishBatchRunner
{
    private const string StartedMessage = "Poorfish terminal batch started.";
    private const string FinishedMessage = "Poorfish terminal batch finished.";
    private const string DefaultWhiteProfilePath = "Assets/Prefabs/V9_OpeningBook.asset";
    private const string DefaultBlackProfilePath = "Assets/Prefabs/V8_Endgame.asset";

    public static void Run()
    {
        Debug.Log(StartedMessage);

        string[] args = Environment.GetCommandLineArgs();
        string projectPath = GetProjectPath(args);

        Debug.Log("Project path: " + projectPath);
        Debug.Log("Unity version: " + Application.unityVersion);
        Debug.Log("Command args: " + string.Join(" ", args));

        string whiteProfilePath = GetArgValue(args, "-white", DefaultWhiteProfilePath);
        string blackProfilePath = GetArgValue(args, "-black", DefaultBlackProfilePath);
        int gameCount = GetIntArgValue(args, "-games", 1000);
        int depth = GetIntArgValue(args, "-depth", 2);
        int maxPlies = GetIntArgValue(args, "-maxPlies", 160);
        string outputPath = GetArgValue(
            args,
            "-out",
            Path.Combine(projectPath, "SelfPlayLogs", "terminal_batch_results.csv"));

        EngineProfile whiteProfile = AssetDatabase.LoadAssetAtPath<EngineProfile>(whiteProfilePath);
        EngineProfile blackProfile = AssetDatabase.LoadAssetAtPath<EngineProfile>(blackProfilePath);
        if (whiteProfile == null || blackProfile == null)
        {
            Debug.LogError("Could not load engine profiles. white=" + whiteProfilePath + " black=" + blackProfilePath);
            EditorApplication.Exit(1);
            return;
        }

        EngineSettings whiteSettings = BatchSettings(whiteProfile, depth);
        EngineSettings blackSettings = BatchSettings(blackProfile, depth);

        Debug.Log(
            "Pure sim batch config: games=" + gameCount
            + " depth=" + depth
            + " maxPlies=" + maxPlies
            + " white=" + whiteProfilePath
            + " black=" + blackProfilePath
            + " out=" + outputPath);

        SimResult[] results = BatchSimulationRunner.RunBatch(
            whiteSettings,
            blackSettings,
            gameCount,
            maxPlies);

        WriteResultsCsv(outputPath, results);
        LogSummary(results, outputPath);
        Debug.Log(
            "Pure sim batch finished: games=" + results.Length
            + " out=" + outputPath);

        Debug.Log(FinishedMessage);
    }

    [MenuItem("Poorfish/Terminal Batch/Milestone Test")]
    public static void RunFromEditorMenu()
    {
        Run();
    }

    private static string GetProjectPath(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-projectPath")
            {
                return Path.GetFullPath(args[i + 1]);
            }
        }

        return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }

    private static string GetArgValue(string[] args, string name, string fallback)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }

        return fallback;
    }

    private static int GetIntArgValue(string[] args, string name, int fallback)
    {
        string rawValue = GetArgValue(args, name, null);
        return int.TryParse(rawValue, out int value) ? Mathf.Max(1, value) : fallback;
    }

    private static EngineSettings BatchSettings(EngineProfile profile, int depth)
    {
        EngineSettings settings = profile.ToSettings();
        settings.searchDepth = Mathf.Max(1, depth);
        settings.maxThinkTimeSeconds = 0f;
        settings.logSearchStats = false;
        settings.useOpeningBook = false;
        return settings;
    }

    private static void WriteResultsCsv(string outputPath, SimResult[] results)
    {
        string directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using (var writer = new StreamWriter(outputPath, false))
        {
            writer.WriteLine("game_number,white_profile,black_profile,result,plies,termination_reason");
            foreach (SimResult result in results)
            {
                writer.WriteLine(
                    result.gameNumber + ","
                    + EscapeCsv(result.whiteProfile) + ","
                    + EscapeCsv(result.blackProfile) + ","
                    + result.result + ","
                    + result.plies + ","
                    + EscapeCsv(result.terminationReason));
            }
        }
    }

    private static void LogSummary(SimResult[] results, string outputPath)
    {
        int whiteWins = 0;
        int blackWins = 0;
        int draws = 0;
        int totalPlies = 0;

        foreach (SimResult result in results)
        {
            totalPlies += result.plies;
            switch (result.result)
            {
                case GameResultType.WhiteWin:
                    whiteWins++;
                    break;
                case GameResultType.BlackWin:
                    blackWins++;
                    break;
                default:
                    draws++;
                    break;
            }
        }

        float averagePlies = results.Length == 0 ? 0f : (float)totalPlies / results.Length;
        Debug.Log(
            "Pure sim batch summary: whiteWins=" + whiteWins
            + " blackWins=" + blackWins
            + " draws=" + draws
            + " avgPlies=" + averagePlies.ToString("F1")
            + " out=" + outputPath);
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
