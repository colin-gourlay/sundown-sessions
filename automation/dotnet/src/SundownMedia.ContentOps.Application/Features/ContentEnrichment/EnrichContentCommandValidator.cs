// <copyright file="EnrichContentCommandValidator.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Application.Features.ContentEnrichment
{
    using FluentValidation;

    public sealed class EnrichContentCommandValidator : AbstractValidator<EnrichContentCommand>
    {
        public EnrichContentCommandValidator()
        {
            this.RuleFor(command => command.SiteRoot).NotEmpty();
            this.RuleFor(command => command.ReportPath).NotEmpty();
            this.RuleFor(command => command.CorrelationId).NotEmpty();
        }
    }
}
