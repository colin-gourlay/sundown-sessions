namespace SundownMedia.ContentOps.Domain.Workflows;

public sealed class Workflow
{
    public Workflow(Guid id, string sourcePath, string workingRoot, string masterRoot)
    {
        Id = id;
        SourcePath = sourcePath;
        WorkingRoot = workingRoot;
        MasterRoot = masterRoot;
        State = WorkflowState.Draft;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; }

    public string SourcePath { get; }

    public string WorkingRoot { get; }

    public string MasterRoot { get; }

    public WorkflowState State { get; private set; }

    public DateTime CreatedAtUtc { get; }

    public void StartIntake() => State = WorkflowState.IntakeRunning;

    public void MarkIntakeSucceeded() => State = WorkflowState.IntakeSucceeded;

    public void MarkIntakeFailed() => State = WorkflowState.IntakeFailed;
}
