using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sundown.Showrunner.Infrastructure;
using Sundown.Showrunner.Mcp.Tools;

var connectionString = Environment.GetEnvironmentVariable("SHOWRUNNER_DB")
    ?? "Data Source=showrunner.db";

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddShowrunnerInfrastructure(connectionString);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<ShowTools>()
    .WithTools<RecordingTools>()
    .WithTools<ShowPreparationTools>();

var app = builder.Build();
await app.RunAsync();
