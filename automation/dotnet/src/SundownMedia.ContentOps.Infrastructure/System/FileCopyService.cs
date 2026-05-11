// <copyright file="FileCopyService.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Infrastructure.System
{
    using SundownMedia.ContentOps.Application.Abstractions;

    public sealed class FileCopyService : IFileCopyService
    {
        public async Task CopyAlbumAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(destinationPath);

            foreach (var sourceFilePath in Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = Path.GetRelativePath(sourcePath, sourceFilePath);
                var destinationFilePath = Path.Combine(destinationPath, relativePath);
                var destinationDirectory = Path.GetDirectoryName(destinationFilePath);

                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                await using var sourceStream = File.OpenRead(sourceFilePath);
                await using var destinationStream = File.Create(destinationFilePath);
                await sourceStream.CopyToAsync(destinationStream, cancellationToken);
            }
        }
    }
}
