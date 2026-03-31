// <copyright file="UpdateShowKeywordsCommand.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Application.Features.ShowNotes.UpdateKeywords;

using ErrorOr;
using Mediator;

public sealed record UpdateShowKeywordsCommand(string ShowDirectoryPath) : IRequest<ErrorOr<UpdateShowKeywordsResult>>;
