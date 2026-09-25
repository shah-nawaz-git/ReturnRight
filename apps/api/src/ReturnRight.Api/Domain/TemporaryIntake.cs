namespace ReturnRight.Api.Domain;

public class TemporaryIntake
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string StorageKey { get; set; }
    public required string OriginalFileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public IntakeStatus Status { get; set; }
    public bool UsedOcr { get; set; }
    public string? CandidatesJson { get; set; }
    public string? ItemsJson { get; set; }
    public string? FailureMessage { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public AppUser User { get; set; } = null!;
}
