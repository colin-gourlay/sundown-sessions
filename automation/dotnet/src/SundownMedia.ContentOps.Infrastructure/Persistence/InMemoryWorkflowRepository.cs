// <copyright file="InMemoryWorkflowRepository.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Infrastructure.Persistence
{
    using SundownMedia.ContentOps.Application.Abstractions;
    using SundownMedia.ContentOps.Domain.Workflows;

    public sealed class InMemoryWorkflowRepository : IWorkflowRepository
    {
        private readonly Dictionary<Guid, Workflow> store = new();

        public Task AddAsync(Workflow workflow, CancellationToken cancellationToken)
        {
            this.store[workflow.Id] = workflow;
            return Task.CompletedTask;
        }

        public Task<Workflow?> GetByIdAsync(Guid workflowId, CancellationToken cancellationToken)
        {
            this.store.TryGetValue(workflowId, out var workflow);
            return Task.FromResult(workflow);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
