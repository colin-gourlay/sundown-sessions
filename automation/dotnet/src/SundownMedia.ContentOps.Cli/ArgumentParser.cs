// <copyright file="ArgumentParser.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Cli;

public static class ArgumentParser
{
    public static bool TryParse(string[] args, out CliOptions? options)
    {
        options = null;

        if (args.Length < 2)
        {
            return false;
        }

        if (string.Equals(args[0], "intake", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseIntakeStart(args, out options);
        }

        if (string.Equals(args[0], "show", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(args[1], "create-frontmatter", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseShowCreateFrontmatter(args, out options);
        }

        return false;
    }

    private static bool TryParseIntakeStart(string[] args, out CliOptions? options)
    {
        options = null;

        if (args.Length < 7)
        {
            return false;
        }

        string? source = null;
        string? workingRoot = null;
        string? masterRoot = null;
        string? correlationId = null;

        for (var i = 2; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--source", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                source = args[++i];
            }
            else if (string.Equals(args[i], "--working-root", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                workingRoot = args[++i];
            }
            else if (string.Equals(args[i], "--master-root", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                masterRoot = args[++i];
            }
            else if (string.Equals(args[i], "--correlation-id", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                correlationId = args[++i];
            }
        }

        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(workingRoot) || string.IsNullOrWhiteSpace(masterRoot))
        {
            return false;
        }

        options = new IntakeCliOptions(source, workingRoot, masterRoot, correlationId);
        return true;
    }

    private static bool TryParseShowCreateFrontmatter(string[] args, out CliOptions? options)
    {
        options = null;

        string? showNumberRaw = null;
        string? featuredGuest = null;
        string? broadcastDateRaw = null;
        string? keywordsRaw = null;
        string? outputPath = null;
        string? correlationId = null;
        string? spotifyEpisodeId = null;

        for (var i = 2; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--show-number", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                showNumberRaw = args[++i];
            }
            else if (string.Equals(args[i], "--featured-guest", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                featuredGuest = args[++i];
            }
            else if (string.Equals(args[i], "--broadcast-date", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                broadcastDateRaw = args[++i];
            }
            else if (string.Equals(args[i], "--keywords", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                keywordsRaw = args[++i];
            }
            else if (string.Equals(args[i], "--output-path", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                outputPath = args[++i];
            }
            else if (string.Equals(args[i], "--correlation-id", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                correlationId = args[++i];
            }
            else if (string.Equals(args[i], "--spotify-episode-id", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                spotifyEpisodeId = args[++i];
            }
        }

        if (string.IsNullOrWhiteSpace(showNumberRaw) ||
            string.IsNullOrWhiteSpace(featuredGuest) ||
            string.IsNullOrWhiteSpace(broadcastDateRaw) ||
            string.IsNullOrWhiteSpace(keywordsRaw) ||
            string.IsNullOrWhiteSpace(outputPath))
        {
            return false;
        }

        if (!int.TryParse(showNumberRaw, out var showNumber) || showNumber <= 0)
        {
            return false;
        }

        if (!DateTimeOffset.TryParse(broadcastDateRaw, out var broadcastDate))
        {
            return false;
        }

        var keywords = keywordsRaw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList()
            .AsReadOnly();

        options = new ShowNotesFrontmatterCliOptions(showNumber, featuredGuest, broadcastDate, keywords, outputPath, correlationId, spotifyEpisodeId);
        return true;
    }
}
