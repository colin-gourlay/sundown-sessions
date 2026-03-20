using SundownMedia.Integration.Spotify.Models;

namespace SundownMedia.Integration.Spotify.Abstractions;

public interface ISpotifyClient
{
    Task<SpotifyResult<SpotifyTrack>> FindTrackAsync(string isrc, string artist, string title, CancellationToken cancellationToken);
}
