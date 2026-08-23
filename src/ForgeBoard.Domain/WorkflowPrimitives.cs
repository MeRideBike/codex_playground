namespace ForgeBoard.Domain;

public enum MissionStatus
{
    Draft,
    Running,
    AwaitingApproval,
    Completed,
    Rejected,
    Failed
}

public enum RunStepStatus
{
    Pending,
    Running,
    Succeeded,
    AwaitingApproval,
    Rejected,
    Failed
}

public enum WorkflowNodeKind { Agent, HumanApproval, DeterministicOperation, ConditionalBranch, Parallel }
public enum ApprovalOutcome { Approved, Rejected, RevisionRequested }

public sealed record WorkflowNodeDefinition(string Key, WorkflowNodeKind Kind, string? AgentRole = null);
public sealed record WorkflowTransition(string From, string To);

public sealed class WorkflowDefinition
{
    public WorkflowDefinition(string name, IReadOnlyCollection<WorkflowNodeDefinition> nodes, IReadOnlyCollection<WorkflowTransition> transitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(transitions);
        if (nodes.Count == 0) throw new ArgumentException("A workflow requires at least one node.", nameof(nodes));
        if (nodes.Select(node => node.Key).Distinct(StringComparer.Ordinal).Count() != nodes.Count)
            throw new ArgumentException("Workflow node keys must be unique.", nameof(nodes));
        Name = name;
        Nodes = nodes;
        Transitions = transitions;
    }

    public string Name { get; }
    public IReadOnlyCollection<WorkflowNodeDefinition> Nodes { get; }
    public IReadOnlyCollection<WorkflowTransition> Transitions { get; }

    public static WorkflowDefinition InitialEngineeringMission() => new(
        "Initial engineering mission",
        [
            new("planner", WorkflowNodeKind.Agent, "Planner"),
            new("researcher", WorkflowNodeKind.Agent, "Researcher"),
            new("approval", WorkflowNodeKind.HumanApproval),
            new("implementer", WorkflowNodeKind.Agent, "Implementer"),
            new("test-engineer", WorkflowNodeKind.Agent, "Test Engineer"),
            new("reviewer", WorkflowNodeKind.Agent, "Reviewer")
        ],
        [
            new("planner", "researcher"), new("researcher", "approval"), new("approval", "implementer"),
            new("implementer", "test-engineer"), new("test-engineer", "reviewer")
        ]);
}
