// <copyright file="LidarrResult.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.Integration.Lidarr.Models;

public sealed record LidarrResult<T>(bool IsSuccess, T? Value, string? ErrorCode = null, string? ErrorMessage = null);
