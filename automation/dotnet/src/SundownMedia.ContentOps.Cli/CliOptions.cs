// <copyright file="CliOptions.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Cli;

public sealed record CliOptions(string SourcePath, string WorkingRoot, string MasterRoot, string? CorrelationId);
