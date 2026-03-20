using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SundownMedia.Integration.Lidarr.Abstractions;
using SundownMedia.Integration.Lidarr.Options;

namespace SundownMedia.Integration.Lidarr.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLidarrIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LidarrOptions>(configuration.GetSection(LidarrOptions.SectionName));
        services.AddSingleton<ILidarrClient, LidarrClient>();
        return services;
    }
}
