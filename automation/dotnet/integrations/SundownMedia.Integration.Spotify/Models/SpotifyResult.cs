// <copyright file="SpotifyResult.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.Integration.Spotify.Models;

public sealed record SpotifyResult<T>(bool IsSuccess, T? Value, string? ErrorCode = null, string? ErrorMessage = null);
