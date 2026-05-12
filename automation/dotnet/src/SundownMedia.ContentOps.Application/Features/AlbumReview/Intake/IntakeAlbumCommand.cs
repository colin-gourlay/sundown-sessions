// <copyright file="IntakeAlbumCommand.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Application.Features.AlbumReview.Intake
{
    using ErrorOr;
    using Mediator;

    public sealed record IntakeAlbumCommand(string SourcePath, string WorkingRoot, string MasterRoot, string CorrelationId) : IRequest<ErrorOr<IntakeAlbumResult>>;
}
