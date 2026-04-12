// <copyright file="ShowLogoService.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Infrastructure.System
{
    using ErrorOr;
    using SundownMedia.ContentOps.Application.Abstractions;
    using SundownMedia.Integration.Spotify.Abstractions;

    public sealed class ShowLogoService : IShowLogoService
    {
        private readonly ISpotifyClient spotifyClient;
        private readonly HttpClient httpClient;

        public ShowLogoService(ISpotifyClient spotifyClient, HttpClient httpClient)
        {
            this.spotifyClient = spotifyClient;
            this.httpClient = httpClient;
        }

        public async Task<ErrorOr<Success>> DownloadLogoAsync(string episodeId, string destinationPath, CancellationToken cancellationToken)
        {
            var episodeResult = await this.spotifyClient.GetEpisodeAsync(episodeId, cancellationToken);

            if (!episodeResult.IsSuccess || episodeResult.Value is null)
            {
                return Error.Failure(
                    "ShowLogo.SpotifyError",
                    episodeResult.ErrorMessage ?? "Failed to retrieve episode from Spotify.");
            }

            if (string.IsNullOrWhiteSpace(episodeResult.Value.ImageUrl))
            {
                return Error.NotFound("ShowLogo.ImageNotFound", "No image URL found for the Spotify episode.");
            }

            byte[] imageBytes;

            try
            {
                imageBytes = await this.httpClient.GetByteArrayAsync(episodeResult.Value.ImageUrl, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                return Error.Failure("ShowLogo.DownloadFailed", ex.Message);
            }

            await File.WriteAllBytesAsync(destinationPath, imageBytes, cancellationToken);

            return Result.Success;
        }
    }
}
