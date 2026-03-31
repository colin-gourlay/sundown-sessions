// <copyright file="UpdateShowKeywordsResult.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Application.Features.ShowNotes.UpdateKeywords;

public sealed record UpdateShowKeywordsResult(IReadOnlyList<string> Keywords);
