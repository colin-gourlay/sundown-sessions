// <copyright file="ISpotifyClient.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.Integration.Spotify.Abstractions
{
    using SundownMedia.Integration.Spotify.Models;

    public interface ISpotifyClient
    {
        Task<SpotifyResult<SpotifyTrack>> FindTrackAsync(string isrc, string artist, string title, CancellationToken cancellationToken);
    }
}
