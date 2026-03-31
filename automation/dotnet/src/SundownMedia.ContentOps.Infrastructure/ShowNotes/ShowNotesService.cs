// <copyright file="ShowNotesService.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Infrastructure.ShowNotes;

using global::System.Text;
using global::System.Text.RegularExpressions;
using SundownMedia.ContentOps.Application.Abstractions;

public sealed partial class ShowNotesService : IShowNotesService
{
    private const string TrackInfoFileName = "track-info.md";
    private const string IndexFileName = "index.md";

    private static readonly Regex KeywordsBlockPattern = KeywordsBlockRegex();

    public async Task<string> ReadTrackInfoAsync(string showDirectoryPath, CancellationToken cancellationToken)
    {
        var trackInfoPath = Path.Combine(showDirectoryPath, TrackInfoFileName);
        return await File.ReadAllTextAsync(trackInfoPath, cancellationToken);
    }

    public async Task UpdateKeywordsAsync(string showDirectoryPath, IReadOnlyList<string> keywords, CancellationToken cancellationToken)
    {
        var indexPath = Path.Combine(showDirectoryPath, IndexFileName);
        var content = await File.ReadAllTextAsync(indexPath, cancellationToken);

        var updatedContent = ReplaceKeywordsBlock(content, keywords);

        await File.WriteAllTextAsync(indexPath, updatedContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
    }

    private static string ReplaceKeywordsBlock(string content, IReadOnlyList<string> keywords)
    {
        var keywordsBlock = BuildKeywordsBlock(keywords);
        return KeywordsBlockPattern.Replace(content, keywordsBlock);
    }

    private static string BuildKeywordsBlock(IReadOnlyList<string> keywords)
    {
        var sb = new StringBuilder();
        sb.AppendLine("keywords:");
        foreach (var keyword in keywords)
        {
            sb.Append(" - '");
            sb.Append(keyword.Replace("'", "''", StringComparison.Ordinal));
            sb.AppendLine("'");
        }

        return sb.ToString().TrimEnd();
    }

    [GeneratedRegex(
        @"keywords:(?:\r?\n - '(?:[^']|'')*')*",
        RegexOptions.Compiled)]
    private static partial Regex KeywordsBlockRegex();
}
