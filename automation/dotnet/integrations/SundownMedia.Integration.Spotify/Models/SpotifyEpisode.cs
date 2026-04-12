// <copyright file="SpotifyEpisode.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.Integration.Spotify.Models;

public sealed record SpotifyEpisode(string Id, string Name, string? ImageUrl);
