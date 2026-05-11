// <copyright file="ShowNotesWriter.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Infrastructure.System
{
    using SundownMedia.ContentOps.Application.Abstractions;

    public sealed class ShowNotesWriter : IShowNotesWriter
    {
        private static readonly global::System.Text.Encoding Utf8NoBom = new global::System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public async Task WriteAsync(string outputPath, string content, CancellationToken cancellationToken)
        {
            await File.WriteAllTextAsync(outputPath, content, Utf8NoBom, cancellationToken);
        }
    }
}
