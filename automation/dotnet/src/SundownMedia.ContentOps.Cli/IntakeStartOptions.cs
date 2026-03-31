// <copyright file="IntakeStartOptions.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Cli;

public sealed record IntakeStartOptions(string SourcePath, string WorkingRoot, string MasterRoot, string? CorrelationId) : CliOptions;
