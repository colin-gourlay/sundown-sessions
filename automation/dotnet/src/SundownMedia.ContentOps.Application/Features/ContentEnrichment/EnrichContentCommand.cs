// <copyright file="EnrichContentCommand.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Application.Features.ContentEnrichment
{
    using ErrorOr;
    using Mediator;

    public sealed record EnrichContentCommand(
        string SiteRoot,
        IReadOnlyList<string> ChangedPaths,
        string ReportPath,
        string CorrelationId) : IRequest<ErrorOr<EnrichContentResult>>;
}
