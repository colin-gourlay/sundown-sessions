using SundownMedia.Integration.Spotify.Models;

namespace SundownMedia.Integration.Spotify.Abstractions;

public sealed class SpotifyClient : ISpotifyClient
{
    public Task<SpotifyResult<SpotifyTrack>> FindTrackAsync(string isrc, string artist, string title, CancellationToken cancellationToken)
    {
        var result = new SpotifyResult<SpotifyTrack>(false, null, "NotImplemented", "Spotify client is a reusable integration contract stub.");
        return Task.FromResult(result);
    }
}
