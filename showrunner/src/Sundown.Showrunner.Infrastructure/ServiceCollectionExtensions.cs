using Microsoft.Extensions.DependencyInjection;
using Sundown.Showrunner.Application.Commands;
using Sundown.Showrunner.Application.Queries;
using Sundown.Showrunner.Domain.Repositories;
using Sundown.Showrunner.Infrastructure.Persistence;
using Sundown.Showrunner.Infrastructure.Repositories;

namespace Sundown.Showrunner.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddShowrunnerInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddSingleton(new ShowrunnerDatabase(connectionString));

        services.AddScoped<IShowRepository, SqliteShowRepository>();
        services.AddScoped<IRecordingRepository, SqliteRecordingRepository>();
        services.AddScoped<IRepeatExceptionRepository, SqliteRepeatExceptionRepository>();

        services.AddScoped<ShowGetQuery>();
        services.AddScoped<RecordingSearchQuery>();
        services.AddScoped<RecordingHistoryQuery>();
        services.AddScoped<ShowPrepareCommand>();
        services.AddScoped<RecordingResolveCommand>();
        services.AddScoped<RepeatExceptionCreateCommand>();

        return services;
    }
}
