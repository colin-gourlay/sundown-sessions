// <copyright file="ServiceCollectionExtensions.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.Integration.Spotify.DependencyInjection
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using SundownMedia.Integration.Spotify.Abstractions;
    using SundownMedia.Integration.Spotify.Options;

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSpotifyIntegration(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<SpotifyOptions>(configuration.GetSection(SpotifyOptions.SectionName));
            services.AddSingleton<ISpotifyClient, SpotifyClient>();
            return services;
        }
    }
}
