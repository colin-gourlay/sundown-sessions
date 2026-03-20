namespace SundownMedia.ContentOps.Contracts.Workflows;

public sealed record IntakeAlbumRequest(string SourcePath, string WorkingRoot, string MasterRoot, string? CorrelationId = null);
