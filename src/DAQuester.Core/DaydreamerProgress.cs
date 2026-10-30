namespace DAQuester.Core;

public sealed record DaydreamerAcknowledgement(DateTimeOffset At, ServerNotice? Status = null, Dialog? Dialog = null);

public sealed record DaydreamerSubmission(Dialog Page, long BaselineRevision, DateTimeOffset SentAt);

public interface IDaydreamerSession
{
    Task<DaydreamerSubmission> SubmitDaydreamerAsync(Dialog page, CancellationToken cancellation);
}

public sealed record DaydreamerProgress
{
    public int? Streak { get; init; }
    public int? PreviousCount { get; init; }
    public ServerNotice? LastCountReceipt { get; init; }
    public DaydreamerAcknowledgement? LastAcknowledgement { get; init; }
    public DaydreamerAcknowledgement? LastRefusal { get; init; }
    public DateTimeOffset? LastSuccessAt { get; init; }
    public DateTimeOffset? NextCheckInAt { get; init; }
    public string TimeZoneId { get; init; } = "America/Chicago";
    public ServerNotice? FirstThirtyReceipt { get; init; }
    public Dialog? Source { get; init; }
    public DaydreamerSubmission? Submission { get; init; }
    public Dialog? CleanupPage { get; init; }
    public bool CleanupPending { get; init; }
}
