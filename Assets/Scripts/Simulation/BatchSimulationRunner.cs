public static class BatchSimulationRunner
{
    public static SimResult[] RunBatch(
        EngineSettings whiteSettings,
        EngineSettings blackSettings,
        int gameCount,
        int maxPlies)
    {
        int safeGameCount = System.Math.Max(1, gameCount);
        SimResult[] results = new SimResult[safeGameCount];

        for (int i = 0; i < safeGameCount; i++)
        {
            results[i] = PureGameSimulator.RunSingleGame(
                whiteSettings,
                blackSettings,
                i + 1,
                maxPlies);
        }

        return results;
    }
}
