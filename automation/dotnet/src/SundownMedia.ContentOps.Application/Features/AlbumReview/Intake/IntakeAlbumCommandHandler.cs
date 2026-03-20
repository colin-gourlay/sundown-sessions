using ErrorOr;
using Mediator;
using SundownMedia.ContentOps.Application.Abstractions;
using SundownMedia.ContentOps.Domain.Workflows;

namespace SundownMedia.ContentOps.Application.Features.AlbumReview.Intake;

public sealed class IntakeAlbumCommandHandler : IRequestHandler<IntakeAlbumCommand, ErrorOr<IntakeAlbumResult>>
{
    private readonly IFileCopyService _fileCopyService;
    private readonly IWorkflowRepository _workflowRepository;

    public IntakeAlbumCommandHandler(IFileCopyService fileCopyService, IWorkflowRepository workflowRepository)
    {
        _fileCopyService = fileCopyService;
        _workflowRepository = workflowRepository;
    }

    public async ValueTask<ErrorOr<IntakeAlbumResult>> Handle(IntakeAlbumCommand command, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(command.SourcePath))
        {
            return Error.Validation("Intake.SourcePath", "Source path does not exist.");
        }

        var workflowId = Guid.NewGuid();
        var workflow = new Workflow(workflowId, command.SourcePath, command.WorkingRoot, command.MasterRoot);
        workflow.StartIntake();

        var destination = Path.Combine(command.WorkingRoot, workflowId.ToString("N"));

        try
        {
            await _fileCopyService.CopyAlbumAsync(command.SourcePath, destination, cancellationToken);
            workflow.MarkIntakeSucceeded();
        }
        catch (Exception ex)
        {
            workflow.MarkIntakeFailed();
            return Error.Failure("Intake.CopyFailed", ex.Message);
        }

        await _workflowRepository.AddAsync(workflow, cancellationToken);
        await _workflowRepository.SaveChangesAsync(cancellationToken);

        return new IntakeAlbumResult(workflowId, command.CorrelationId);
    }
}
