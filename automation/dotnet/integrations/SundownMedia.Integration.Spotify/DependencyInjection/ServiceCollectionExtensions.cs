using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SundownMedia.Integration.Spotify.Abstractions;
using SundownMedia.Integration.Spotify.Options;

namespace SundownMedia.Integration.Spotify.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSpotifyIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SpotifyOptions>(configuration.GetSection(SpotifyOptions.SectionName));
        services.AddSingleton<ISpotifyClient, SpotifyClient>();
        return services;
    }
}
