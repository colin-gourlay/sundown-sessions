// <copyright file="ServiceCollectionExtensions.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Infrastructure.DependencyInjection
{
    using Microsoft.Extensions.DependencyInjection;
    using SundownMedia.ContentOps.Application.Abstractions;
    using SundownMedia.ContentOps.Contracts.Correlation;
    using SundownMedia.ContentOps.Infrastructure.Correlation;
    using SundownMedia.ContentOps.Infrastructure.Persistence;
    using SundownMedia.ContentOps.Infrastructure.System;
    using SundownMedia.Integration.Spotify.Abstractions;

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddContentOpsInfrastructure(this IServiceCollection services)
        {
            services.AddSingleton<ICorrelationContext, AsyncLocalCorrelationContext>();
            services.AddSingleton<IWorkflowRepository, InMemoryWorkflowRepository>();
            services.AddSingleton<IClock, SystemClock>();
            services.AddSingleton<IFileCopyService, FileCopyService>();
            services.AddSingleton<IShowNotesWriter, ShowNotesWriter>();
            services.AddSingleton<ISpotifyClient, SpotifyClient>();
            services.AddHttpClient<IShowLogoService, ShowLogoService>();
            return services;
        }
    }
}
