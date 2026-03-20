namespace SundownMedia.ContentOps.Cli;

public sealed record CliOptions(string SourcePath, string WorkingRoot, string MasterRoot, string? CorrelationId);
