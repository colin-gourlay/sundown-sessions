using SundownMedia.ContentOps.Application.Abstractions;
using SundownMedia.ContentOps.Domain.Workflows;

namespace SundownMedia.ContentOps.Infrastructure.Persistence;

public sealed class InMemoryWorkflowRepository : IWorkflowRepository
{
    private readonly Dictionary<Guid, Workflow> _store = new();

    public Task AddAsync(Workflow workflow, CancellationToken cancellationToken)
    {
        _store[workflow.Id] = workflow;
        return Task.CompletedTask;
    }

    public Task<Workflow?> GetByIdAsync(Guid workflowId, CancellationToken cancellationToken)
    {
        _store.TryGetValue(workflowId, out var workflow);
        return Task.FromResult(workflow);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
