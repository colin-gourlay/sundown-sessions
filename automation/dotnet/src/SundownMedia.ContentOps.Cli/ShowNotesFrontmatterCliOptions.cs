// <copyright file="ShowNotesFrontmatterCliOptions.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Cli;

public sealed record ShowNotesFrontmatterCliOptions(
    int ShowNumber,
    string FeaturedGuest,
    DateTimeOffset BroadcastDate,
    IReadOnlyList<string> Keywords,
    string OutputPath,
    string? CorrelationId,
    string? SpotifyEpisodeId = null)
    : CliOptions(CorrelationId);
