// <copyright file="SpotifyClient.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.Integration.Spotify.Abstractions
{
    using SundownMedia.Integration.Spotify.Models;

    public sealed class SpotifyClient : ISpotifyClient
    {
        public Task<SpotifyResult<SpotifyTrack>> FindTrackAsync(string isrc, string artist, string title, CancellationToken cancellationToken)
        {
            var result = new SpotifyResult<SpotifyTrack>(false, null, "NotImplemented", "Spotify client is a reusable integration contract stub.");
            return Task.FromResult(result);
        }
    }
}
