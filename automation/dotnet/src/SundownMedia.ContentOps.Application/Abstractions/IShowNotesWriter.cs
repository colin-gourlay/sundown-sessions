// <copyright file="IShowNotesWriter.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Application.Abstractions;

public interface IShowNotesWriter
{
    Task WriteAsync(string outputPath, string content, CancellationToken cancellationToken);
}
