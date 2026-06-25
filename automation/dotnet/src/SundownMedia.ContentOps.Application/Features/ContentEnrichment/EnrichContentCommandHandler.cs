// <copyright file="EnrichContentCommandHandler.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Application.Features.ContentEnrichment
{
    using ErrorOr;
    using Mediator;

    public sealed class EnrichContentCommandHandler : IRequestHandler<EnrichContentCommand, ErrorOr<EnrichContentResult>>
    {
        public ValueTask<ErrorOr<EnrichContentResult>> Handle(
            EnrichContentCommand command,
            CancellationToken cancellationToken)
        {
            if (!Directory.Exists(command.SiteRoot))
            {
                return ValueTask.FromResult<ErrorOr<EnrichContentResult>>(
                    Error.Validation("ContentEnrichment.SiteRoot", "Site root does not exist."));
            }

            var result = ContentEnrichmentGenerator.Enrich(command);
            return ValueTask.FromResult<ErrorOr<EnrichContentResult>>(result);
        }
    }
}
