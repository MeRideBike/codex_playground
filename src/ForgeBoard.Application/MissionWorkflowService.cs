using ForgeBoard.Domain;
using Microsoft.Extensions.Logging;

namespace ForgeBoard.Application;

public sealed class MissionWorkflowService(
    IMissionRepository repository,
    IAgentProvider agentProvider,
    IClock clock,
    ILogger<MissionWorkflowService> logger)
{
    private static readonly WorkflowDefinition InitialWorkflow = WorkflowDefinition.InitialEngineeringMission();
    private static readonly Action<ILogger, Guid, string, string, Exception?> StepCompleted = LoggerMessage.Define<Guid, string, string>(
        LogLevel.Information, new EventId(1001, nameof(StepCompleted)), "Mission {MissionId} step {StepKey} completed with provider {Provider}");
    private static readonly Action<ILogger, Exception?> StepFailed = LoggerMessage.Define(
        LogLevel.Error, new EventId(1002, nameof(StepFailed)), "Mission workflow step failed; see structured audit record for the mission and step identifiers.");

    public async Task<Mission> CreateAsync(string title, string description, CancellationToken cancellationToken)
    {
        var mission = new Mission(title, description, InitialWorkflow.Name, clock.UtcNow);
        foreach (var node in InitialWorkflow.Nodes)
            mission.AddStep(new RunStep(node.Key, node.AgentRole ?? "Human Approval", node.Kind));

        await repository.AddAsync(mission, cancellationToken);
        mission.Start(clock.UtcNow);
        await repository.SaveChangesAsync(cancellationToken);
        return await ExecuteAvailableStepsAsync(mission, cancellationToken);
    }

    public async Task<Mission> DecideApprovalAsync(Guid missionId, ApprovalOutcome outcome, string comment, CancellationToken cancellationToken)
    {
        var mission = await repository.GetAsync(missionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Mission '{missionId}' was not found.");
        var approvalStep = mission.Steps.LastOrDefault(step => step.Status == RunStepStatus.AwaitingApproval)
            ?? throw new InvalidOperationException("This mission has no approval awaiting a decision.");
        mission.RecordApproval(new ApprovalRecord(approvalStep.Id, outcome, comment, clock.UtcNow));

        if (outcome == ApprovalOutcome.Rejected)
        {
            approvalStep.Reject(clock.UtcNow);
            mission.Reject(clock.UtcNow);
            await repository.SaveChangesAsync(cancellationToken);
            return mission;
        }

        approvalStep.Approve(clock.UtcNow);
        mission.Resume();
        if (outcome == ApprovalOutcome.RevisionRequested)
        {
            var revisionNumber = mission.Approvals.Count(approval => approval.Outcome == ApprovalOutcome.RevisionRequested);
            mission.AddStep(new RunStep($"researcher-revision-{revisionNumber}", $"Researcher Revision {revisionNumber}", WorkflowNodeKind.Agent));
            mission.AddStep(new RunStep($"approval-revision-{revisionNumber}", $"Human Approval {revisionNumber + 1}", WorkflowNodeKind.HumanApproval));
        }

        await repository.SaveChangesAsync(cancellationToken);
        return await ExecuteAvailableStepsAsync(mission, cancellationToken);
    }

    public async Task<Mission> GetAsync(Guid missionId, CancellationToken cancellationToken) =>
        await repository.GetAsync(missionId, cancellationToken) ?? throw new KeyNotFoundException($"Mission '{missionId}' was not found.");

    public Task<IReadOnlyList<Mission>> ListAsync(CancellationToken cancellationToken) => repository.ListAsync(cancellationToken);

    private async Task<Mission> ExecuteAvailableStepsAsync(Mission mission, CancellationToken cancellationToken)
    {
        while (mission.Status == MissionStatus.Running)
        {
            var step = FindNextStep(mission);
            if (step is null)
            {
                mission.Complete(clock.UtcNow);
                await repository.SaveChangesAsync(cancellationToken);
                break;
            }

            if (step.Kind == WorkflowNodeKind.HumanApproval)
            {
                step.WaitForApproval(clock.UtcNow);
                mission.AwaitApproval();
                await repository.SaveChangesAsync(cancellationToken);
                break;
            }

            var artifacts = mission.Steps.Where(candidate => candidate.Status == RunStepStatus.Succeeded && candidate.StructuredOutput is not null)
                .Select(candidate => new ArtifactReference(candidate.NodeKey, candidate.StructuredOutput!)).ToArray();
            step.Start(System.Text.Json.JsonSerializer.Serialize(new { mission.Title, mission.Description, Artifacts = artifacts }), clock.UtcNow);
            await repository.SaveChangesAsync(cancellationToken);
            try
            {
                var output = await agentProvider.ExecuteAsync(new AgentExecutionRequest(step.DisplayName, mission.Title, mission.Description, artifacts), cancellationToken);
                step.Succeed(output, clock.UtcNow);
                StepCompleted(logger, mission.Id, step.NodeKey, output.Provider, null);
                await repository.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                step.Fail(exception.Message, clock.UtcNow);
                mission.Fail(clock.UtcNow);
                StepFailed(logger, exception);
                await repository.SaveChangesAsync(CancellationToken.None);
                break;
            }
        }
        return mission;
    }

    private static RunStep? FindNextStep(Mission mission)
    {
        var revisionStep = mission.Steps.LastOrDefault(step => step.NodeKey.StartsWith("researcher-revision-", StringComparison.Ordinal) && step.Status == RunStepStatus.Pending);
        if (revisionStep is not null) return revisionStep;
        var revisionApproval = mission.Steps.LastOrDefault(step => step.NodeKey.StartsWith("approval-revision-", StringComparison.Ordinal) && step.Status == RunStepStatus.Pending);
        if (revisionApproval is not null && mission.Steps.Any(step => step.NodeKey.StartsWith("researcher-revision-", StringComparison.Ordinal) && step.Status == RunStepStatus.Succeeded)) return revisionApproval;
        return InitialWorkflow.Nodes.Select(node => mission.Steps.Single(step => step.NodeKey == node.Key)).FirstOrDefault(step => step.Status == RunStepStatus.Pending);
    }
}
