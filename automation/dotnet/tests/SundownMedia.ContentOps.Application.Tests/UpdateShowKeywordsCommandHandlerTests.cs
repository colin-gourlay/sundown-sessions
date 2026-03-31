using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SundownMedia.ContentOps.Application.Abstractions;
using SundownMedia.ContentOps.Application.Features.ShowNotes.UpdateKeywords;

namespace SundownMedia.ContentOps.Application.Tests;

public sealed class UpdateShowKeywordsCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsError_WhenShowDirectoryDoesNotExist()
    {
        var showNotesService = Substitute.For<IShowNotesService>();
        var handler = new UpdateShowKeywordsCommandHandler(showNotesService);

        var command = new UpdateShowKeywordsCommand("/does/not/exist");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        await showNotesService.DidNotReceive().ReadTrackInfoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsError_WhenTrackInfoFileIsNotFound()
    {
        var showNotesService = Substitute.For<IShowNotesService>();
        showNotesService
            .ReadTrackInfoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new FileNotFoundException());

        var handler = new UpdateShowKeywordsCommandHandler(showNotesService);
        var showDir = Directory.CreateTempSubdirectory().FullName;

        try
        {
            var command = new UpdateShowKeywordsCommand(showDir);

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.FirstError.Code.Should().Be("UpdateShowKeywords.TrackInfo");
        }
        finally
        {
            Directory.Delete(showDir, recursive: true);
        }
    }

    [Fact]
    public async Task Handle_ReturnsError_WhenIndexFileIsNotFound()
    {
        const string TrackInfoContent = """
            | 1 | {{<title "Take Me Out--Franz Ferdinand">}} | Album | 3:57 | |
            """;

        var showNotesService = Substitute.For<IShowNotesService>();
        showNotesService
            .ReadTrackInfoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(TrackInfoContent);
        showNotesService
            .UpdateKeywordsAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new FileNotFoundException());

        var handler = new UpdateShowKeywordsCommandHandler(showNotesService);
        var showDir = Directory.CreateTempSubdirectory().FullName;

        try
        {
            var command = new UpdateShowKeywordsCommand(showDir);

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.FirstError.Code.Should().Be("UpdateShowKeywords.ShowNotes");
        }
        finally
        {
            Directory.Delete(showDir, recursive: true);
        }
    }

    [Fact]
    public async Task Handle_ReturnsKeywords_WhenSuccessful()
    {
        const string TrackInfoContent = """
            | 1 | {{<title "Take Me Out--Franz Ferdinand">}} | Album | 3:57 | |
            | 2 | {{<track-info-featured-guest "Fast Cars, Soul Music--The Big Now">}} | Demo | 4:15 | |
            """;

        var showNotesService = Substitute.For<IShowNotesService>();
        showNotesService
            .ReadTrackInfoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(TrackInfoContent);

        var handler = new UpdateShowKeywordsCommandHandler(showNotesService);
        var showDir = Directory.CreateTempSubdirectory().FullName;

        try
        {
            var command = new UpdateShowKeywordsCommand(showDir);
            string[] expectedKeywords = ["The Big Now", "Franz Ferdinand"];

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsError.Should().BeFalse();
            result.Value.Keywords.Should().Equal(expectedKeywords);

            await showNotesService.Received(1).UpdateKeywordsAsync(
                showDir,
                Arg.Is<IReadOnlyList<string>>(k => k.SequenceEqual(expectedKeywords)),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            Directory.Delete(showDir, recursive: true);
        }
    }
}
