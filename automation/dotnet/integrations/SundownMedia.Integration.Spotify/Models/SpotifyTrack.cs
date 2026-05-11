// <copyright file="SpotifyTrack.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.Integration.Spotify.Models;

public sealed record SpotifyTrack(string Id, string Artist, string Title, string? Isrc, string? Label);
