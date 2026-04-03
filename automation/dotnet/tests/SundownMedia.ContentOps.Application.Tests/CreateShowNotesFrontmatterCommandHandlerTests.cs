using FluentAssertions;
using NSubstitute;
using SundownMedia.ContentOps.Application.Abstractions;
using SundownMedia.ContentOps.Application.Features.ShowNotes.CreateFrontmatter;

namespace SundownMedia.ContentOps.Application.Tests;

public sealed class CreateShowNotesFrontmatterCommandHandlerTests
{
    private static readonly DateTimeOffset BroadcastDate = new DateTimeOffset(2024, 6, 5, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_ReturnsError_WhenOutputDirectoryDoesNotExist()
    {
        var writer = Substitute.For<IShowNotesWriter>();
        var handler = new CreateShowNotesFrontmatterCommandHandler(writer);

        var command = new CreateShowNotesFrontmatterCommand(
            1,
            "The Big Now",
            BroadcastDate,
            ["The Big Now", "IST IST"],
            "/does/not/exist/index.md",
            Guid.NewGuid().ToString("D"));

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        await writer.DidNotReceive().WriteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WritesFile_WhenOutputDirectoryExists()
    {
        var writer = Substitute.For<IShowNotesWriter>();
        var handler = new CreateShowNotesFrontmatterCommandHandler(writer);
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var outputPath = Path.Combine(tempDir, "index.md");

        try
        {
            Directory.CreateDirectory(tempDir);

            var correlationId = Guid.NewGuid().ToString("D");
            var command = new CreateShowNotesFrontmatterCommand(
                1,
                "The Big Now",
                BroadcastDate,
                ["The Big Now", "IST IST"],
                outputPath,
                correlationId);

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsError.Should().BeFalse();
            result.Value.OutputPath.Should().Be(outputPath);
            result.Value.CorrelationId.Should().Be(correlationId);

            await writer.Received(1).WriteAsync(
                Arg.Is(outputPath),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void Build_ContainsFrontmatterFields()
    {
        var command = new CreateShowNotesFrontmatterCommand(
            1,
            "The Big Now",
            BroadcastDate,
            ["The Big Now", "IST IST", "Nick Cave & The Bad Seeds"],
            "/tmp/index.md",
            Guid.NewGuid().ToString("D"));

        var content = ShowNotesFrontmatterBuilder.Build(command);

        content.Should().Contain("title: 'Show #1: Broadcast 5th June 2024'");
        content.Should().Contain("slug: 'featuring-the-big-now'");
        content.Should().Contain("description: 'featuring The Big Now'");
        content.Should().Contain("featured_image: '1-show-logo.jpeg'");
        content.Should().Contain("date: 2024-06-05T22:00:00+00:00");
        content.Should().Contain("toc: false");
        content.Should().Contain("draft: true");
        content.Should().Contain("- 'The Big Now'");
        content.Should().Contain("- 'IST IST'");
        content.Should().Contain("- 'Nick Cave & The Bad Seeds'");
        content.Should().Contain("{{< fold \"Listen On Demand\" >}}");
        content.Should().Contain("{{< include_content \"/shows/1/listen-again\" >}}");
        content.Should().Contain("{{< fold \"Playlist\" >}}");
        content.Should().Contain("{{< include_content \"/shows/1/playlist\" >}}");
        content.Should().Contain("{{< fold \"Featured band: The Big Now\" >}}");
        content.Should().Contain("{{< include_content \"/shows/1/featured-guest\" >}}");
        content.Should().Contain("{{< fold \"Show discussion points\" >}}");
        content.Should().Contain("{{< include_content \"/shows/1/discussion-points\" >}}");
        content.Should().Contain("{{< fold \"Track info\" >}}");
        content.Should().Contain("{{< include_content \"/shows/1/track-info\" >}}");
        content.Split(["{{< /fold >}}"], StringSplitOptions.None).Length.Should().Be(6);
    }

    [Theory]
    [InlineData(1, 6, "1st")]
    [InlineData(2, 6, "2nd")]
    [InlineData(3, 6, "3rd")]
    [InlineData(4, 6, "4th")]
    [InlineData(5, 6, "5th")]
    [InlineData(11, 6, "11th")]
    [InlineData(12, 6, "12th")]
    [InlineData(13, 6, "13th")]
    [InlineData(21, 6, "21st")]
    [InlineData(22, 6, "22nd")]
    [InlineData(23, 6, "23rd")]
    [InlineData(31, 1, "31st")]
    public void FormatBroadcastDate_ProducesCorrectOrdinal(int day, int month, string expectedOrdinal)
    {
        var date = new DateTimeOffset(2024, month, day, 22, 0, 0, TimeSpan.Zero);

        var formatted = ShowNotesFrontmatterBuilder.FormatBroadcastDate(date);

        formatted.Should().StartWith(expectedOrdinal);
    }

    [Fact]
    public void ToSlug_ConvertsToLowercaseWithHyphens()
    {
        var slug = ShowNotesFrontmatterBuilder.ToSlug("The Big Now");

        slug.Should().Be("the-big-now");
    }

    [Fact]
    public void ToSlug_RemovesSpecialCharacters()
    {
        var slug = ShowNotesFrontmatterBuilder.ToSlug("Nick Cave & The Bad Seeds");

        slug.Should().Be("nick-cave-the-bad-seeds");
    }

    [Fact]
    public void EscapeYamlSingleQuoted_EscapesApostrophes()
    {
        var escaped = ShowNotesFrontmatterBuilder.EscapeYamlSingleQuoted("O'Hara");

        escaped.Should().Be("O''Hara");
    }
}
