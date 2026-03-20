// <copyright file="IWorkflowRepository.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Application.Abstractions
{
    using SundownMedia.ContentOps.Domain.Workflows;

    public interface IWorkflowRepository
    {
        Task AddAsync(Workflow workflow, CancellationToken cancellationToken);

        Task<Workflow?> GetByIdAsync(Guid workflowId, CancellationToken cancellationToken);

        Task SaveChangesAsync(CancellationToken cancellationToken);
    }
}
