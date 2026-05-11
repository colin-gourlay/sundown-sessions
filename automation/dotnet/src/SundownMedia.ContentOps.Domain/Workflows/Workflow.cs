// <copyright file="Workflow.cs" company="SundownMedia">
// Copyright (c) SundownMedia. All rights reserved.
// </copyright>

namespace SundownMedia.ContentOps.Domain.Workflows;

public sealed class Workflow
{
    public Workflow(Guid id, string sourcePath, string workingRoot, string masterRoot)
    {
        this.Id = id;
        this.SourcePath = sourcePath;
        this.WorkingRoot = workingRoot;
        this.MasterRoot = masterRoot;
        this.State = WorkflowState.Draft;
        this.CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; }

    public string SourcePath { get; }

    public string WorkingRoot { get; }

    public string MasterRoot { get; }

    public WorkflowState State { get; private set; }

    public DateTime CreatedAtUtc { get; }

    public void StartIntake() => this.State = WorkflowState.IntakeRunning;

    public void MarkIntakeSucceeded() => this.State = WorkflowState.IntakeSucceeded;

    public void MarkIntakeFailed() => this.State = WorkflowState.IntakeFailed;
}
