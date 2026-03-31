// <copyright file="IShowNotesService.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Application.Abstractions;

public interface IShowNotesService
{
    Task<string> ReadTrackInfoAsync(string showDirectoryPath, CancellationToken cancellationToken);

    Task UpdateKeywordsAsync(string showDirectoryPath, IReadOnlyList<string> keywords, CancellationToken cancellationToken);
}
