// <copyright file="IFileCopyService.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Application.Abstractions;

public interface IFileCopyService
{
    Task CopyAlbumAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken);
}
