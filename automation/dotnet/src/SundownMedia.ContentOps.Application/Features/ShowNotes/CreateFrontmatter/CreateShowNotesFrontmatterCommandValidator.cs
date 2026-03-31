// <copyright file="CreateShowNotesFrontmatterCommandValidator.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Application.Features.ShowNotes.CreateFrontmatter
{
    using FluentValidation;

    public sealed class CreateShowNotesFrontmatterCommandValidator : AbstractValidator<CreateShowNotesFrontmatterCommand>
    {
        public CreateShowNotesFrontmatterCommandValidator()
        {
            this.RuleFor(command => command.ShowNumber).GreaterThan(0);
            this.RuleFor(command => command.FeaturedGuest).NotEmpty();
            this.RuleFor(command => command.Keywords).NotEmpty();
            this.RuleFor(command => command.OutputPath).NotEmpty();
            this.RuleFor(command => command.CorrelationId).NotEmpty();
        }
    }
}
