// <copyright file="IntakeCliOptions.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Cli;

public sealed record IntakeCliOptions(string SourcePath, string WorkingRoot, string MasterRoot, string? CorrelationId)
    : CliOptions(CorrelationId);
