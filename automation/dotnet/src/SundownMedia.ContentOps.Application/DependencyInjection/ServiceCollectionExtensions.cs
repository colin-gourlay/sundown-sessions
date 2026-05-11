// <copyright file="ServiceCollectionExtensions.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Application.DependencyInjection
{
    using FluentValidation;
    using Microsoft.Extensions.DependencyInjection;

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddContentOpsApplication(this IServiceCollection services)
        {
            services.AddMediator();
            services.AddValidatorsFromAssembly(typeof(ServiceCollectionExtensions).Assembly);
            return services;
        }
    }
}
