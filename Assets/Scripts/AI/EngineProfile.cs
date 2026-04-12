using System;
using UnityEngine;

[CreateAssetMenu(fileName = "EngineProfile", menuName = "Poorfish/Engine Profile")]
public class EngineProfile : ScriptableObject
{
    [Header("Identity")]
    public string profileName = "Baseline";

    [Header("Search")]
    [Min(1)]
    public int searchDepth = 6;
    [Min(0f)]
    public float maxThinkTimeSeconds = 1.5f;
    public bool logSearchStats = true;

    [Header("Evaluation")]
    public EvaluationWeights evaluationWeights = EvaluationWeights.Default;

    public EngineSettings ToSettings()
    {
        return new EngineSettings
        {
            profileName = string.IsNullOrWhiteSpace(profileName) ? name : profileName,
            searchDepth = searchDepth,
            maxThinkTimeSeconds = maxThinkTimeSeconds,
            logSearchStats = logSearchStats,
            evaluationWeights = evaluationWeights
        };
    }
}

[Serializable]
public struct EngineSettings
{
    public string profileName;
    public int searchDepth;
    public float maxThinkTimeSeconds;
    public bool logSearchStats;
    public EvaluationWeights evaluationWeights;

    public static EngineSettings Default => new EngineSettings
    {
        profileName = "Baseline",
        searchDepth = 6,
        maxThinkTimeSeconds = 1.5f,
        logSearchStats = true,
        evaluationWeights = EvaluationWeights.Default
    };
}
