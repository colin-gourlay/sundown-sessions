// <copyright file="IClock.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Application.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}
