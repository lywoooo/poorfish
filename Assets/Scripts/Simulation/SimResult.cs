public readonly struct SimResult
{
    public readonly int gameNumber;
    public readonly string whiteProfile;
    public readonly string blackProfile;
    public readonly GameResultType result;
    public readonly int plies;
    public readonly string terminationReason;

    public SimResult(
        int gameNumber,
        string whiteProfile,
        string blackProfile,
        GameResultType result,
        int plies,
        string terminationReason)
    {
        this.gameNumber = gameNumber;
        this.whiteProfile = whiteProfile;
        this.blackProfile = blackProfile;
        this.result = result;
        this.plies = plies;
        this.terminationReason = terminationReason;
    }
}
