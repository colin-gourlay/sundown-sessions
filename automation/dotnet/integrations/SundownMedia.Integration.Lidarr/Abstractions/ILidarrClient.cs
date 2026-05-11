// <copyright file="ILidarrClient.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.Integration.Lidarr.Abstractions
{
    using SundownMedia.Integration.Lidarr.Models;

    public interface ILidarrClient
    {
        Task<LidarrResult<bool>> RefreshLibraryAsync(LidarrRelease release, CancellationToken cancellationToken);
    }
}
