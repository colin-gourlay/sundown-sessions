namespace SundownSessions.Showrunner.Persistence;

internal sealed class RecordingEntity
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Artist { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public List<RecordingExternalIdentifierEntity> ExternalIdentifiers { get; } = [];
}

internal sealed class RecordingExternalIdentifierEntity
{
    public Guid Id { get; set; }

    public Guid RecordingId { get; set; }

    public string Source { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public RecordingEntity Recording { get; set; } = null!;
}

internal sealed class BacklogItemEntity
{
    public Guid Id { get; set; }

    public Guid? RecordingId { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed class ShowEntity
{
    public Guid Id { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public DateOnly ShowDate { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public List<PlannedRecordingEntity> PlannedRecordings { get; } = [];

    public ReconciliationEntity? Reconciliation { get; set; }

    public List<BroadcastRecordingEntity> BroadcastRecordings { get; } = [];
}

internal sealed class PlannedRecordingEntity
{
    public Guid Id { get; set; }

    public Guid ShowId { get; set; }

    public Guid RecordingId { get; set; }

    public int Position { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public ShowEntity Show { get; set; } = null!;
}

internal sealed class ReconciliationEntity
{
    public Guid Id { get; set; }

    public Guid ShowId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? ConfirmedAtUtc { get; set; }

    public ShowEntity Show { get; set; } = null!;

    public List<ReconciliationItemEntity> Items { get; } = [];
}

internal sealed class ReconciliationItemEntity
{
    public Guid Id { get; set; }

    public Guid ReconciliationId { get; set; }

    public Guid PlannedRecordingId { get; set; }

    public ReconciliationItemOutcome Outcome { get; set; }

    public ReconciliationEntity Reconciliation { get; set; } = null!;
}

internal sealed class BroadcastRecordingEntity
{
    public Guid Id { get; set; }

    public Guid ShowId { get; set; }

    public Guid RecordingId { get; set; }

    public Guid PlannedRecordingId { get; set; }

    public DateTimeOffset BroadcastAtUtc { get; set; }

    public ShowEntity Show { get; set; } = null!;
}

internal sealed class RepeatExceptionEntity
{
    public Guid Id { get; set; }

    public Guid ShowId { get; set; }

    public Guid RecordingId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
