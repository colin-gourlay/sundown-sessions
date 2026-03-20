namespace SundownMedia.Integration.Lidarr.Options;

public sealed class LidarrOptions
{
    public const string SectionName = "Lidarr";

    public string BaseUrl { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;
}
