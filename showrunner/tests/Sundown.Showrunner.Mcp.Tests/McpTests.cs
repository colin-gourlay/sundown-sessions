using ModelContextProtocol.Server;
using Sundown.Showrunner.Application.Commands;
using Sundown.Showrunner.Application.Exceptions;
using Sundown.Showrunner.Application.Queries;
using Sundown.Showrunner.Mcp.Tests.Fakes;
using Sundown.Showrunner.Domain.Entities;
using Sundown.Showrunner.Mcp.Tools;
using Xunit;

namespace Sundown.Showrunner.Mcp.Tests;

public class ToolDiscoveryTests
{
    [Fact]
    public void ShowTools_HasMcpServerToolTypeAttribute()
    {
        var attr = typeof(ShowTools).GetCustomAttributes(typeof(McpServerToolTypeAttribute), false);
        Assert.Single(attr);
    }

    [Fact]
    public void RecordingTools_HasMcpServerToolTypeAttribute()
    {
        var attr = typeof(RecordingTools).GetCustomAttributes(typeof(McpServerToolTypeAttribute), false);
        Assert.Single(attr);
    }

    [Fact]
    public void ShowPreparationTools_HasMcpServerToolTypeAttribute()
    {
        var attr = typeof(ShowPreparationTools).GetCustomAttributes(typeof(McpServerToolTypeAttribute), false);
        Assert.Single(attr);
    }

    [Fact]
    public void ShowTools_ShowGetByIdMethod_HasMcpServerToolAttribute()
    {
        var method = typeof(ShowTools).GetMethod(nameof(ShowTools.ShowGetByIdAsync));
        Assert.NotNull(method);

        var attr = method.GetCustomAttributes(typeof(McpServerToolAttribute), false).Cast<McpServerToolAttribute>().FirstOrDefault();
        Assert.NotNull(attr);
        Assert.Equal("show_get", attr.Name);
    }

    [Fact]
    public void RecordingTools_RecordingSearchMethod_HasMcpServerToolAttribute()
    {
        var method = typeof(RecordingTools).GetMethod(nameof(RecordingTools.RecordingSearchAsync));
        Assert.NotNull(method);

        var attr = method.GetCustomAttributes(typeof(McpServerToolAttribute), false).Cast<McpServerToolAttribute>().FirstOrDefault();
        Assert.NotNull(attr);
        Assert.Equal("recording_search", attr.Name);
    }

    [Fact]
    public void RecordingTools_RecordingHistoryMethod_HasMcpServerToolAttribute()
    {
        var method = typeof(RecordingTools).GetMethod(nameof(RecordingTools.RecordingHistoryAsync));
        Assert.NotNull(method);

        var attr = method.GetCustomAttributes(typeof(McpServerToolAttribute), false).Cast<McpServerToolAttribute>().FirstOrDefault();
        Assert.NotNull(attr);
        Assert.Equal("recording_history", attr.Name);
    }

    [Fact]
    public void ShowPreparationTools_ShowPrepareMethod_HasMcpServerToolAttribute()
    {
        var method = typeof(ShowPreparationTools).GetMethod(nameof(ShowPreparationTools.ShowPrepareAsync));
        Assert.NotNull(method);

        var attr = method.GetCustomAttributes(typeof(McpServerToolAttribute), false).Cast<McpServerToolAttribute>().FirstOrDefault();
        Assert.NotNull(attr);
        Assert.Equal("show_prepare", attr.Name);
    }

    [Fact]
    public void ShowPreparationTools_RecordingResolveMethod_HasMcpServerToolAttribute()
    {
        var method = typeof(ShowPreparationTools).GetMethod(nameof(ShowPreparationTools.RecordingResolveAsync));
        Assert.NotNull(method);

        var attr = method.GetCustomAttributes(typeof(McpServerToolAttribute), false).Cast<McpServerToolAttribute>().FirstOrDefault();
        Assert.NotNull(attr);
        Assert.Equal("recording_resolve", attr.Name);
    }

    [Fact]
    public void ShowPreparationTools_RepeatExceptionCreateMethod_HasMcpServerToolAttribute()
    {
        var method = typeof(ShowPreparationTools).GetMethod(nameof(ShowPreparationTools.RepeatExceptionCreateAsync));
        Assert.NotNull(method);

        var attr = method.GetCustomAttributes(typeof(McpServerToolAttribute), false).Cast<McpServerToolAttribute>().FirstOrDefault();
        Assert.NotNull(attr);
        Assert.Equal("repeat_exception_create", attr.Name);
    }
}

public class ShowToolsTests
{
    private static ShowTools MakeShowTools(FakeShowRepository showRepo)
        => new(new ShowGetQuery(showRepo));

    [Fact]
    public async Task ShowGetByIdAsync_KnownShow_ReturnsResult()
    {
        var showRepo = new FakeShowRepository();
        showRepo.Seed(new Show { Id = 1, BroadcastDate = new DateOnly(2026, 6, 10), Title = "Episode 1", Status = ShowStatus.Planned });

        var tools = MakeShowTools(showRepo);
        var result = await tools.ShowGetByIdAsync(1);

        Assert.Equal("Episode 1", result.Title);
    }

    [Fact]
    public async Task ShowGetByDateAsync_InvalidDateFormat_ThrowsDomainRuleException()
    {
        var tools = MakeShowTools(new FakeShowRepository());

        await Assert.ThrowsAsync<DomainRuleException>(() => tools.ShowGetByDateAsync("not-a-date"));
    }

    [Fact]
    public async Task ShowGetByDateAsync_UnknownDate_ThrowsShowNotFoundException()
    {
        var tools = MakeShowTools(new FakeShowRepository());

        await Assert.ThrowsAsync<ShowNotFoundException>(() => tools.ShowGetByDateAsync("2026-01-01"));
    }
}

public class RecordingToolsTests
{
    private static RecordingTools MakeRecordingTools(FakeRecordingRepository recordingRepo)
        => new(new RecordingSearchQuery(recordingRepo), new RecordingHistoryQuery(recordingRepo));

    [Fact]
    public async Task RecordingSearchAsync_EmptyQuery_ThrowsDomainRuleException()
    {
        var tools = MakeRecordingTools(new FakeRecordingRepository());

        await Assert.ThrowsAsync<DomainRuleException>(() => tools.RecordingSearchAsync(""));
    }

    [Fact]
    public async Task RecordingSearchAsync_MultipleMatches_IsAmbiguousTrue()
    {
        var recordingRepo = new FakeRecordingRepository();
        recordingRepo.Seed(new Recording { Id = 1, ArtistName = "Massive Attack", TrackTitle = "Teardrop" });
        recordingRepo.Seed(new Recording { Id = 2, ArtistName = "Massive Attack", TrackTitle = "Unfinished Sympathy" });

        var tools = MakeRecordingTools(recordingRepo);
        var result = await tools.RecordingSearchAsync("massive");

        Assert.True(result.IsAmbiguous);
    }

    [Fact]
    public async Task RecordingHistoryAsync_NoHistory_ReturnsEmpty()
    {
        var recordingRepo = new FakeRecordingRepository();
        recordingRepo.Seed(new Recording { Id = 1, ArtistName = "Orbital", TrackTitle = "Belfast" });

        var tools = MakeRecordingTools(recordingRepo);
        var result = await tools.RecordingHistoryAsync(1);

        Assert.Empty(result);
    }
}

public class ShowPreparationToolsTests
{
    private static ShowPreparationTools MakeTools(
        FakeShowRepository? showRepo = null,
        FakeRecordingRepository? recordingRepo = null,
        FakeRepeatExceptionRepository? repeatRepo = null)
    {
        showRepo ??= new FakeShowRepository();
        recordingRepo ??= new FakeRecordingRepository();
        repeatRepo ??= new FakeRepeatExceptionRepository();

        return new ShowPreparationTools(
            new ShowPrepareCommand(showRepo, recordingRepo, repeatRepo),
            new RecordingResolveCommand(showRepo, recordingRepo),
            new RepeatExceptionCreateCommand(showRepo, recordingRepo, repeatRepo));
    }

    [Fact]
    public async Task ShowPrepareAsync_UnknownShow_ThrowsShowNotFoundException()
    {
        var tools = MakeTools();

        await Assert.ThrowsAsync<ShowNotFoundException>(() => tools.ShowPrepareAsync(99));
    }

    [Fact]
    public async Task RepeatExceptionCreateAsync_EmptyReason_ThrowsDomainRuleException()
    {
        var tools = MakeTools();

        await Assert.ThrowsAsync<DomainRuleException>(
            () => tools.RepeatExceptionCreateAsync(1, 1, "   "));
    }

    [Fact]
    public async Task RecordingResolveAsync_ValidRequest_UpdatesSlot()
    {
        var showRepo = new FakeShowRepository();
        showRepo.Seed(new Show
        {
            Id = 1,
            BroadcastDate = new DateOnly(2026, 6, 1),
            Title = "Show",
            Status = ShowStatus.Planned,
            Slots = [new ShowSlot { Position = 1 }],
        });

        var recordingRepo = new FakeRecordingRepository();
        recordingRepo.Seed(new Recording { Id = 1, ArtistName = "Nils Frahm", TrackTitle = "Says" });

        var tools = MakeTools(showRepo, recordingRepo);
        var result = await tools.RecordingResolveAsync(1, 1, 1);

        var slot = Assert.Single(result.Slots);
        Assert.Equal(1, slot.RecordingId);
        Assert.Equal("Nils Frahm", slot.ArtistName);
    }
}

public class NoDomainMcpDependencyTests
{
    [Fact]
    public void Domain_Assembly_HasNoMcpDependencies()
    {
        var domainAssembly = typeof(Sundown.Showrunner.Domain.Entities.Show).Assembly;
        var referencedNames = domainAssembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty);

        Assert.DoesNotContain(referencedNames, name => name.StartsWith("ModelContextProtocol", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Application_Assembly_HasNoMcpDependencies()
    {
        var applicationAssembly = typeof(Sundown.Showrunner.Application.Queries.ShowGetQuery).Assembly;
        var referencedNames = applicationAssembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty);

        Assert.DoesNotContain(referencedNames, name => name.StartsWith("ModelContextProtocol", StringComparison.OrdinalIgnoreCase));
    }
}
