// <copyright file="IntakeAlbumCommandValidator.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Application.Features.AlbumReview.Intake
{
    using FluentValidation;

    public sealed class IntakeAlbumCommandValidator : AbstractValidator<IntakeAlbumCommand>
    {
        public IntakeAlbumCommandValidator()
        {
            this.RuleFor(command => command.SourcePath).NotEmpty();
            this.RuleFor(command => command.WorkingRoot).NotEmpty();
            this.RuleFor(command => command.MasterRoot).NotEmpty();
            this.RuleFor(command => command.CorrelationId).NotEmpty();
        }
    }
}
