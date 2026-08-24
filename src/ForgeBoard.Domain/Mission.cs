namespace ForgeBoard.Domain;

public sealed class Mission
{
    private readonly List<RunStep> _steps = [];
    private readonly List<ApprovalRecord> _approvals = [];

    private Mission() { Title = null!; Description = null!; WorkflowName = null!; OperationPolicy = null!; }

    public Mission(string title, string description, string workflowName, DateTimeOffset createdAt, string? sampleId = null, string? templateId = null, int? maxTokenUsage = null, decimal? maxEstimatedCost = null, string? operationPolicy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowName);
        Id = Guid.NewGuid(); Title = title.Trim(); Description = description.Trim(); WorkflowName = workflowName;
        SampleId = string.IsNullOrWhiteSpace(sampleId) ? null : sampleId.Trim();
        TemplateId = string.IsNullOrWhiteSpace(templateId) ? null : templateId.Trim();
        if (maxTokenUsage is <= 0) throw new ArgumentOutOfRangeException(nameof(maxTokenUsage), "The token limit must be positive when specified.");
        if (maxEstimatedCost is <= 0) throw new ArgumentOutOfRangeException(nameof(maxEstimatedCost), "The cost limit must be positive when specified.");
        MaxTokenUsage = maxTokenUsage; MaxEstimatedCost = maxEstimatedCost;
        OperationPolicy = string.IsNullOrWhiteSpace(operationPolicy) ? "[]" : operationPolicy;
        CreatedAt = createdAt; Status = MissionStatus.Draft;
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public string WorkflowName { get; private set; }
    public string? SampleId { get; private set; }
    public string? TemplateId { get; private set; }
    public int? MaxTokenUsage { get; private set; }
    public decimal? MaxEstimatedCost { get; private set; }
    public string OperationPolicy { get; private set; }
    public MissionStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public IReadOnlyCollection<RunStep> Steps => _steps.AsReadOnly();
    public IReadOnlyCollection<ApprovalRecord> Approvals => _approvals.AsReadOnly();

    public void AddStep(RunStep step) { ArgumentNullException.ThrowIfNull(step); _steps.Add(step); }

    public void Start(DateTimeOffset now)
    {
        if (Status != MissionStatus.Draft) throw new InvalidOperationException("Only draft missions can be started.");
        Status = MissionStatus.Running; StartedAt = now;
    }

    public void AwaitApproval() => TransitionFrom(MissionStatus.Running, MissionStatus.AwaitingApproval);
    public void Resume() => TransitionFrom(MissionStatus.AwaitingApproval, MissionStatus.Running);
    public void Complete(DateTimeOffset now) { TransitionFrom(MissionStatus.Running, MissionStatus.Completed); CompletedAt = now; }
    public void Fail(DateTimeOffset now) { Status = MissionStatus.Failed; CompletedAt = now; }
    public void RestartAfterFailure()
    {
        if (Status != MissionStatus.Failed) throw new InvalidOperationException("Only failed missions can be retried.");
        Status = MissionStatus.Running; CompletedAt = null;
    }
    public void Reject(DateTimeOffset now) { TransitionFrom(MissionStatus.AwaitingApproval, MissionStatus.Rejected); CompletedAt = now; }

    public void RecordApproval(ApprovalRecord record) { ArgumentNullException.ThrowIfNull(record); _approvals.Add(record); }

    private void TransitionFrom(MissionStatus expected, MissionStatus next)
    {
        if (Status != expected) throw new InvalidOperationException($"Mission must be {expected} before it can become {next}.");
        Status = next;
    }
}

public sealed class RunStep
{
    private RunStep() { NodeKey = null!; DisplayName = null!; }
    public RunStep(string nodeKey, string displayName, WorkflowNodeKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeKey); ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        Id = Guid.NewGuid(); NodeKey = nodeKey; DisplayName = displayName; Kind = kind; Status = RunStepStatus.Pending;
    }
    public Guid Id { get; private set; }
    public Guid MissionId { get; private set; }
    public string NodeKey { get; private set; }
    public string DisplayName { get; private set; }
    public WorkflowNodeKind Kind { get; private set; }
    public RunStepStatus Status { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? Input { get; private set; }
    public string? StructuredOutput { get; private set; }
    public string? RationaleSummary { get; private set; }
    public string? Provider { get; private set; }
    public string? Model { get; private set; }
    public string? ToolsInvoked { get; private set; }
    public string? ToolResults { get; private set; }
    public string? Error { get; private set; }
    public int RetryAttempts { get; private set; }
    public int? TokenUsage { get; private set; }
    public decimal? EstimatedCost { get; private set; }

    public void Start(string input, DateTimeOffset now)
    {
        if (Status != RunStepStatus.Pending) throw new InvalidOperationException("Only pending steps can start.");
        Input = input; StartedAt = now; Status = RunStepStatus.Running;
    }
    public void Succeed(AgentAuditOutput output, DateTimeOffset now)
    {
        if (Status != RunStepStatus.Running) throw new InvalidOperationException("Only running steps can succeed.");
        StructuredOutput = output.StructuredOutput; RationaleSummary = output.RationaleSummary; Provider = output.Provider;
        Model = output.Model; ToolsInvoked = output.ToolsInvoked; ToolResults = output.ToolResults; TokenUsage = output.TokenUsage;
        EstimatedCost = output.EstimatedCost; CompletedAt = now; Status = RunStepStatus.Succeeded;
    }
    public void WaitForApproval(DateTimeOffset now)
    {
        if (Status != RunStepStatus.Pending) throw new InvalidOperationException("Only pending steps can await approval.");
        StartedAt = now; Status = RunStepStatus.AwaitingApproval;
    }
    public void Approve(DateTimeOffset now) { if (Status != RunStepStatus.AwaitingApproval) throw new InvalidOperationException("Step is not awaiting approval."); Status = RunStepStatus.Succeeded; CompletedAt = now; }
    public void Reject(DateTimeOffset now) { if (Status != RunStepStatus.AwaitingApproval) throw new InvalidOperationException("Step is not awaiting approval."); Status = RunStepStatus.Rejected; CompletedAt = now; }
    public void Fail(string error, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        Error = Error is null ? error : $"{Error}{Environment.NewLine}Retry {RetryAttempts}: {error}";
        CompletedAt = now; Status = RunStepStatus.Failed;
    }
    public void Retry()
    {
        if (Status != RunStepStatus.Failed) throw new InvalidOperationException("Only failed steps can be retried.");
        RetryAttempts++; StartedAt = null; CompletedAt = null; Status = RunStepStatus.Pending;
    }
    public void FailAfterOutput(string error, DateTimeOffset now)
    {
        if (Status != RunStepStatus.Succeeded) throw new InvalidOperationException("Only a succeeded step with recorded usage can exceed a budget.");
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        Error = error; CompletedAt = now; Status = RunStepStatus.Failed;
    }
}

public sealed record AgentAuditOutput(string StructuredOutput, string RationaleSummary, string Provider, string Model, string? ToolsInvoked, string? ToolResults, int? TokenUsage, decimal? EstimatedCost);

public sealed class ApprovalRecord
{
    private ApprovalRecord() { Comment = null!; }
    public ApprovalRecord(Guid stepId, ApprovalOutcome outcome, string comment, DateTimeOffset decidedAt)
    {
        if (stepId == Guid.Empty) throw new ArgumentException("A step identifier is required.", nameof(stepId));
        ArgumentException.ThrowIfNullOrWhiteSpace(comment);
        Id = Guid.NewGuid(); StepId = stepId; Outcome = outcome; Comment = comment.Trim(); DecidedAt = decidedAt;
    }
    public Guid Id { get; private set; }
    public Guid MissionId { get; private set; }
    public Guid StepId { get; private set; }
    public ApprovalOutcome Outcome { get; private set; }
    public string Comment { get; private set; }
    public DateTimeOffset DecidedAt { get; private set; }
}
