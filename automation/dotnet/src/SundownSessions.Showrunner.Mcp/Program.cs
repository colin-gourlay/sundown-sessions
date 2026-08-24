using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SundownSessions.Showrunner;
using SundownSessions.Showrunner.Mcp;
using SundownSessions.Showrunner.Persistence;

var preparationOptions = ShowrunnerMcpConfiguration.Load();
var databasePath = ShowrunnerDbContextFactory.ResolveDatabasePath();
var connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddDbContext<ShowrunnerDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<ShowrunnerService>();
builder.Services.AddScoped<ShowPreparationService>();
builder.Services.AddScoped<ShowReconciliationService>();
builder.Services.AddSingleton<IMixxxPlaybackEvidenceReader>(_ => new SqliteMixxxPlaybackEvidenceReader());
builder.Services.AddSingleton(preparationOptions);
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<ShowrunnerTools>();

using var host = builder.Build();
await using (var scope = host.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ShowrunnerDbContext>();
    await dbContext.Database.MigrateAsync();
}

await host.RunAsync();
