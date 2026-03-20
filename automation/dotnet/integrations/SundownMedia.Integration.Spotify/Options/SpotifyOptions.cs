namespace SundownMedia.Integration.Spotify.Options;

public sealed class SpotifyOptions
{
    public const string SectionName = "Spotify";

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string RedirectUri { get; init; } = string.Empty;
}
