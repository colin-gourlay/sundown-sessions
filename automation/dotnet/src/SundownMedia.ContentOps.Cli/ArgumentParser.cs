// <copyright file="ArgumentParser.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Cli;

public static class ArgumentParser
{
    public static bool TryParse(string[] args, out CliOptions? options)
    {
        options = null;

        if (args.Length < 7)
        {
            return false;
        }

        if (!string.Equals(args[0], "intake", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(args[1], "start", StringComparison.OrdinalIgnoreCase))
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

        options = new CliOptions(source, workingRoot, masterRoot, correlationId);
        return true;
    }
}
