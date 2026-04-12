// <copyright file="IShowLogoService.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Application.Abstractions;

using ErrorOr;

public interface IShowLogoService
{
    Task<ErrorOr<Success>> DownloadLogoAsync(string episodeId, string destinationPath, CancellationToken cancellationToken);
}
