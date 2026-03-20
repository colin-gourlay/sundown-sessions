using SundownMedia.ContentOps.Domain.Workflows;

namespace SundownMedia.ContentOps.Application.Abstractions;

public interface IWorkflowRepository
{
    Task AddAsync(Workflow workflow, CancellationToken cancellationToken);

    Task<Workflow?> GetByIdAsync(Guid workflowId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
