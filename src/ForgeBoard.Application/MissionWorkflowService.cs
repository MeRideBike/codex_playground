using ForgeBoard.Domain;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ForgeBoard.Application;

public sealed class MissionWorkflowService(
    IMissionRepository repository,
    IAgentProvider agentProvider,
    ISampleWorkspace sampleWorkspace,
    IEngineeringMissionCatalog missionCatalog,
    IConstrainedSampleExecutor constrainedSampleExecutor,
    IClock clock,
    ILogger<MissionWorkflowService> logger)
{
    private static readonly WorkflowDefinition InitialWorkflow = WorkflowDefinition.InitialEngineeringMission();
    private static readonly Action<ILogger, Guid, string, string, Exception?> StepCompleted = LoggerMessage.Define<Guid, string, string>(
        LogLevel.Information, new EventId(1001, nameof(StepCompleted)), "Mission {MissionId} step {StepKey} completed with provider {Provider}");
    private static readonly Action<ILogger, Exception?> StepFailed = LoggerMessage.Define(
        LogLevel.Error, new EventId(1002, nameof(StepFailed)), "Mission workflow step failed; see structured audit record for the mission and step identifiers.");

    public Task<Mission> CreateAsync(string title, string description, string? sampleId, string? templateId, CancellationToken cancellationToken) =>
        CreateAsync(title, description, sampleId, templateId, null, null, cancellationToken);

    public async Task<Mission> CreateAsync(string title, string description, string? sampleId, string? templateId, int? maxTokenUsage, decimal? maxEstimatedCost, CancellationToken cancellationToken)
    {
        var template = string.IsNullOrWhiteSpace(templateId) ? null : missionCatalog.Find(templateId)
            ?? throw new ArgumentException($"Mission template '{templateId}' is not available.", nameof(templateId));
        if (template is not null && !string.IsNullOrWhiteSpace(sampleId) && !string.Equals(template.SampleId, sampleId, StringComparison.Ordinal))
            throw new ArgumentException("The selected mission template does not support the selected sample.", nameof(sampleId));
        sampleId ??= template?.SampleId;
        template ??= !string.IsNullOrWhiteSpace(sampleId) ? missionCatalog.GetDefaultForSample(sampleId) : null;
        if (!string.IsNullOrWhiteSpace(sampleId) && await sampleWorkspace.GetSnapshotAsync(sampleId, cancellationToken) is null)
            throw new ArgumentException($"Sample '{sampleId}' is not available.", nameof(sampleId));
        var operationPolicy = JsonSerializer.Serialize(template?.AllowedCapabilities ?? Array.Empty<string>());
        var mission = new Mission(title, description, InitialWorkflow.Name, clock.UtcNow, sampleId, template?.Id, maxTokenUsage, maxEstimatedCost, operationPolicy);
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
        var approval = new ApprovalRecord(approvalStep.Id, outcome, comment, clock.UtcNow);
        mission.RecordApproval(approval);
        await repository.AddApprovalAsync(approval, cancellationToken);

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
            var researcherRevision = new RunStep($"researcher-revision-{revisionNumber}", $"Researcher Revision {revisionNumber}", WorkflowNodeKind.Agent);
            var approvalRevision = new RunStep($"approval-revision-{revisionNumber}", $"Human Approval {revisionNumber + 1}", WorkflowNodeKind.HumanApproval);
            mission.AddStep(researcherRevision);
            mission.AddStep(approvalRevision);
            await repository.AddStepAsync(researcherRevision, cancellationToken);
            await repository.AddStepAsync(approvalRevision, cancellationToken);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return await ExecuteAvailableStepsAsync(mission, cancellationToken);
    }

    public async Task<Mission> GetAsync(Guid missionId, CancellationToken cancellationToken) =>
        await repository.GetAsync(missionId, cancellationToken) ?? throw new KeyNotFoundException($"Mission '{missionId}' was not found.");

    public Task<IReadOnlyList<Mission>> ListAsync(CancellationToken cancellationToken) => repository.ListAsync(cancellationToken);

    public async Task<IReadOnlyList<SampleFileChange>> GetChangesAsync(Guid missionId, CancellationToken cancellationToken)
    {
        var mission = await GetAsync(missionId, cancellationToken);
        if (string.IsNullOrWhiteSpace(mission.SampleId)) return [];
        var sample = await sampleWorkspace.GetSnapshotAsync(mission.SampleId, cancellationToken)
            ?? throw new InvalidOperationException($"Sample '{mission.SampleId}' is no longer available.");
        return await constrainedSampleExecutor.GetChangesAsync(mission.Id, sample, cancellationToken);
    }

    public async Task<Mission> CreateIsolatedCommitAsync(Guid missionId, string summary, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(summary) || summary.Length > 300)
            throw new ArgumentException("An isolated commit summary of 1 to 300 characters is required.", nameof(summary));
        var mission = await GetAsync(missionId, cancellationToken);
        if (mission.Status != MissionStatus.Completed)
            throw new InvalidOperationException("Only a completed mission can create an isolated review commit.");
        if (mission.Steps.Any(step => step.NodeKey == "isolated-commit" && step.Status == RunStepStatus.Succeeded))
            throw new InvalidOperationException("This mission already has an isolated review commit.");
        if (mission.SampleId is null || mission.TemplateId is null)
            throw new InvalidOperationException("Only a completed, sample-backed template mission can create an isolated review commit.");
        var sample = await sampleWorkspace.GetSnapshotAsync(mission.SampleId, cancellationToken)
            ?? throw new InvalidOperationException($"Sample '{mission.SampleId}' is no longer available.");
        var template = missionCatalog.Find(mission.TemplateId)
            ?? throw new InvalidOperationException($"Mission template '{mission.TemplateId}' is no longer available.");
        var step = new RunStep("isolated-commit", "Isolated Review Commit", WorkflowNodeKind.DeterministicOperation);
        mission.AddStep(step);
        await repository.AddStepAsync(step, cancellationToken);
        step.Start(JsonSerializer.Serialize(new { action = "isolated-git-commit", summary = summary.Trim(), mission.OperationPolicy }), clock.UtcNow);
        await repository.SaveChangesAsync(cancellationToken);
        try
        {
            var output = await constrainedSampleExecutor.CommitReviewedChangeAsync(mission.Id, sample, template, GetAllowedCapabilities(mission.OperationPolicy), summary, cancellationToken);
            step.Succeed(output, clock.UtcNow);
            StepCompleted(logger, mission.Id, step.NodeKey, output.Provider, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            step.Fail(exception.Message, clock.UtcNow);
            StepFailed(logger, exception);
        }
        await repository.SaveChangesAsync(cancellationToken);
        return mission;
    }

    public async Task<Mission> RetryFailedStepAsync(Guid missionId, CancellationToken cancellationToken)
    {
        var mission = await repository.GetAsync(missionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Mission '{missionId}' was not found.");
        var step = mission.Steps.LastOrDefault(candidate => candidate.Status == RunStepStatus.Failed)
            ?? throw new InvalidOperationException("This mission has no failed step to retry.");
        mission.RestartAfterFailure();
        step.Retry();
        await repository.SaveChangesAsync(cancellationToken);
        return await ExecuteAvailableStepsAsync(mission, cancellationToken);
    }

    private async Task<Mission> ExecuteAvailableStepsAsync(Mission mission, CancellationToken cancellationToken)
    {
        var sample = mission.SampleId is null ? null : await sampleWorkspace.GetSnapshotAsync(mission.SampleId, cancellationToken)
            ?? throw new InvalidOperationException($"Sample '{mission.SampleId}' is no longer available.");
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
            var stepInput = JsonSerializer.Serialize(new { mission.Title, mission.Description, Sample = sample?.ToAuditReference(), mission.OperationPolicy, Budget = new { mission.MaxTokenUsage, mission.MaxEstimatedCost }, Artifacts = artifacts });
            if (!CanStartNextStep(mission, out var budgetError))
            {
                step.Start(stepInput, clock.UtcNow);
                step.Fail(budgetError!, clock.UtcNow);
                mission.Fail(clock.UtcNow);
                await repository.SaveChangesAsync(cancellationToken);
                break;
            }
            step.Start(stepInput, clock.UtcNow);
            await repository.SaveChangesAsync(cancellationToken);
            try
            {
                var template = mission.TemplateId is null ? null : missionCatalog.Find(mission.TemplateId);
                var output = sample is not null && template is not null && step.NodeKey is "implementer" or "test-engineer"
                    ? await ExecuteConstrainedOperationAsync(step.NodeKey, mission.Id, sample, template, GetAllowedCapabilities(mission.OperationPolicy), cancellationToken)
                    : await agentProvider.ExecuteAsync(new AgentExecutionRequest(step.DisplayName, mission.Title, mission.Description, artifacts, sample, template), cancellationToken);
                step.Succeed(output, clock.UtcNow);
                if (!WouldExceedBudget(mission, out budgetError))
                {
                    StepCompleted(logger, mission.Id, step.NodeKey, output.Provider, null);
                    await repository.SaveChangesAsync(cancellationToken);
                    continue;
                }
                step.FailAfterOutput(budgetError!, clock.UtcNow);
                mission.Fail(clock.UtcNow);
                StepCompleted(logger, mission.Id, step.NodeKey, output.Provider, null);
                await repository.SaveChangesAsync(cancellationToken);
                break;
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

    private Task<AgentAuditOutput> ExecuteConstrainedOperationAsync(string stepKey, Guid missionId, SampleSnapshot sample, EngineeringMissionTemplate template, IReadOnlyList<string> allowedCapabilities, CancellationToken cancellationToken) =>
        stepKey switch
        {
            "implementer" => constrainedSampleExecutor.ApplyCorrectionAsync(missionId, sample, template, allowedCapabilities, cancellationToken),
            "test-engineer" => constrainedSampleExecutor.VerifyCorrectionAsync(missionId, sample, template, allowedCapabilities, cancellationToken),
            _ => throw new InvalidOperationException($"No constrained operation is configured for '{stepKey}'.")
        };

    private static string[] GetAllowedCapabilities(string operationPolicy)
    {
        try { return JsonSerializer.Deserialize<string[]>(operationPolicy) ?? []; }
        catch (JsonException) { throw new InvalidOperationException("The persisted mission operation policy is invalid."); }
    }

    private static bool CanStartNextStep(Mission mission, out string? error)
    {
        var usedTokens = mission.Steps.Sum(step => step.TokenUsage ?? 0);
        var usedCost = mission.Steps.Sum(step => step.EstimatedCost ?? 0m);
        if (mission.MaxTokenUsage is { } maxTokens && usedTokens >= maxTokens)
        {
            error = $"Mission token budget is exhausted ({usedTokens}/{maxTokens}).";
            return false;
        }
        if (mission.MaxEstimatedCost is { } maxCost && usedCost >= maxCost)
        {
            error = $"Mission estimated-cost budget is exhausted ({usedCost}/{maxCost}).";
            return false;
        }
        error = null;
        return true;
    }

    private static bool WouldExceedBudget(Mission mission, out string? error)
    {
        var usedTokens = mission.Steps.Sum(step => step.TokenUsage ?? 0);
        var usedCost = mission.Steps.Sum(step => step.EstimatedCost ?? 0m);
        if (mission.MaxTokenUsage is { } maxTokens && usedTokens > maxTokens)
        {
            error = $"Mission token budget was exceeded ({usedTokens}/{maxTokens}); no later workflow step was started.";
            return true;
        }
        if (mission.MaxEstimatedCost is { } maxCost && usedCost > maxCost)
        {
            error = $"Mission estimated-cost budget was exceeded ({usedCost}/{maxCost}); no later workflow step was started.";
            return true;
        }
        error = null;
        return false;
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
