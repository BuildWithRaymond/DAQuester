namespace DAQuester.Core;

public record PatrolWaypoint(int Map, Tile Position);

// Exact server replies and verified collection settings for the selected server profile.
public record Calibration
{
    public string ServerProfile
    {
        get;
        init;
    } = "datester-7.41";
    public bool Validated
    {
        get;
        init;
    }
    public string BranchName
    {
        get;
        init;
    } = "Branch";
    public int BranchWeight
    {
        get;
        init;
    } = 1;
    public int RewardWeightReserve
    {
        get;
        init;
    } = 5;
    public Tile[] Patrol
    {
        get;
        init;
    } = [];
    public PatrolWaypoint[] AdditionalPatrol
    {
        get;
        init;
    } = [];
    public string PatrolBasis
    {
        get;
        init;
    } = "Configured route; collection is proved by server inventory";
    public Tile[] ObservedDropPositions
    {
        get;
        init;
    } = [];
    public string[] Accepted
    {
        get;
        init;
    } = [];
    public string[] Completed
    {
        get;
        init;
    } = [];
    public string[] Refused
    {
        get;
        init;
    } = [];
    public string[] Cooldown
    {
        get;
        init;
    } = [];
    public string[] EvidenceFiles
    {
        get;
        init;
    } = [];
    public string[] ValidatedCharacters
    {
        get;
        init;
    } = [];
    public bool Ready => Validated && Patrol.Length > 0 && Accepted.Length > 0 && Completed.Length > 0 && EvidenceFiles.Length > 0;

    public bool Matches(string text, string[] patterns) => patterns.Any(p => p.Length > 0 && text.Equals(p, StringComparison.Ordinal));
}
