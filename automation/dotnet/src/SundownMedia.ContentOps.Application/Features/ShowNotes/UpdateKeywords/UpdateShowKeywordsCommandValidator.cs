// <copyright file="UpdateShowKeywordsCommandValidator.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Application.Features.ShowNotes.UpdateKeywords;

using FluentValidation;

public sealed class UpdateShowKeywordsCommandValidator : AbstractValidator<UpdateShowKeywordsCommand>
{
    public UpdateShowKeywordsCommandValidator()
    {
        this.RuleFor(command => command.ShowDirectoryPath).NotEmpty();
    }
}
