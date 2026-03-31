// <copyright file="HelpPrinter.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Cli;

public static class HelpPrinter
{
    public static void Print()
    {
        Console.WriteLine("SundownMedia ContentOps CLI");
        Console.WriteLine("Usage:");
        Console.WriteLine("  contentops intake start --source <path> --working-root <path> --master-root <path> [--correlation-id <guid>]");
        Console.WriteLine("  contentops show create-frontmatter --show-number <n> --featured-guest <name> --broadcast-date <ISO 8601> --keywords <artist1,artist2,...> --output-path <path> [--correlation-id <guid>]");
    }
}
