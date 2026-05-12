using FluentAssertions;
using NSubstitute;
using SundownMedia.ContentOps.Application.Abstractions;
using SundownMedia.ContentOps.Application.Features.AlbumReview.Intake;
using SundownMedia.ContentOps.Domain.Workflows;

namespace SundownMedia.ContentOps.Application.Tests;

public sealed class IntakeAlbumCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsError_WhenSourcePathDoesNotExist()
    {
        var fileCopyService = Substitute.For<IFileCopyService>();
        var repository = Substitute.For<IWorkflowRepository>();
        var handler = new IntakeAlbumCommandHandler(fileCopyService, repository);

        var command = new IntakeAlbumCommand("/does/not/exist", "/tmp/work", "/tmp/master", Guid.NewGuid().ToString("D"));

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        await repository.DidNotReceive().AddAsync(Arg.Any<Workflow>(), Arg.Any<CancellationToken>());
    }
}
