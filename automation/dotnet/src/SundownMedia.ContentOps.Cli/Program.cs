// <copyright file="Program.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SundownMedia.ContentOps.Application.DependencyInjection;
using SundownMedia.ContentOps.Application.Features.AlbumReview.Intake;
using SundownMedia.ContentOps.Application.Features.ShowNotes.UpdateKeywords;
using SundownMedia.ContentOps.Cli;
using SundownMedia.ContentOps.Contracts.Correlation;
using SundownMedia.ContentOps.Infrastructure.DependencyInjection;

var hostBuilder = Host.CreateApplicationBuilder(args);
hostBuilder.Services.AddContentOpsApplication();
hostBuilder.Services.AddContentOpsInfrastructure();

using var host = hostBuilder.Build();

if (!ArgumentParser.TryParse(args, out var options) || options is null)
{
    HelpPrinter.Print();
    return;
}

var sender = host.Services.GetRequiredService<ISender>();

if (options is IntakeStartOptions intakeOptions)
{
    var correlationContext = host.Services.GetRequiredService<ICorrelationContext>();
    var correlationId = intakeOptions.CorrelationId ?? Guid.NewGuid().ToString("D");
    correlationContext.SetCorrelation(correlationId);

    var command = new IntakeAlbumCommand(intakeOptions.SourcePath, intakeOptions.WorkingRoot, intakeOptions.MasterRoot, correlationId);
    var result = await sender.Send(command, CancellationToken.None);

    if (result.IsError)
    {
        Console.Error.WriteLine(result.FirstError.Description);
        Environment.ExitCode = 1;
        return;
    }

    Console.WriteLine($"Workflow created: {result.Value.WorkflowId}");
    Console.WriteLine($"CorrelationId: {result.Value.CorrelationId}");
}
else if (options is UpdateShowKeywordsOptions updateKeywordsOptions)
{
    var command = new UpdateShowKeywordsCommand(updateKeywordsOptions.ShowDirectoryPath);
    var result = await sender.Send(command, CancellationToken.None);

    if (result.IsError)
    {
        Console.Error.WriteLine(result.FirstError.Description);
        Environment.ExitCode = 1;
        return;
    }

    Console.WriteLine($"Keywords updated ({result.Value.Keywords.Count}):");
    foreach (var keyword in result.Value.Keywords)
    {
        Console.WriteLine($"  - {keyword}");
    }
}
