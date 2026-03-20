using Microsoft.Extensions.DependencyInjection;
using SundownMedia.ContentOps.Application.Abstractions;
using SundownMedia.ContentOps.Contracts.Correlation;
using SundownMedia.ContentOps.Infrastructure.Correlation;
using SundownMedia.ContentOps.Infrastructure.Persistence;
using SundownMedia.ContentOps.Infrastructure.System;

namespace SundownMedia.ContentOps.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContentOpsInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ICorrelationContext, AsyncLocalCorrelationContext>();
        services.AddSingleton<IWorkflowRepository, InMemoryWorkflowRepository>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IFileCopyService, FileCopyService>();
        return services;
    }
}
