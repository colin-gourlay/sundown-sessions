// <copyright file="CreateShowNotesFrontmatterCommand.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Application.Features.ShowNotes.CreateFrontmatter
{
    using ErrorOr;
    using Mediator;

    public sealed record CreateShowNotesFrontmatterCommand(
        int ShowNumber,
        string FeaturedGuest,
        DateTimeOffset BroadcastDate,
        IReadOnlyList<string> Keywords,
        string OutputPath,
        string CorrelationId,
        string? SpotifyEpisodeId = null) : IRequest<ErrorOr<CreateShowNotesFrontmatterResult>>;
}
