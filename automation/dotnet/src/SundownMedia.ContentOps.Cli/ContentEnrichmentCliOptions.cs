// <copyright file="ContentEnrichmentCliOptions.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Cli;

public sealed record ContentEnrichmentCliOptions(
    string SiteRoot,
    IReadOnlyList<string> ChangedPaths,
    string ReportPath,
    string? CorrelationId)
    : CliOptions(CorrelationId);
