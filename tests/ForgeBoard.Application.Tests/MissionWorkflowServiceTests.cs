using ForgeBoard.Application;
using ForgeBoard.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace ForgeBoard.Application.Tests;

public sealed class MissionWorkflowServiceTests
{
    [Fact]
    public async Task CreationRunsUntilTheHumanApprovalGate()
    {
        var repository = new InMemoryMissionRepository();
        var service = CreateService(repository);

        var mission = await service.CreateAsync("Fix sample", "Diagnose a failing sample test.", null, null, CancellationToken.None);

        Assert.Equal(MissionStatus.AwaitingApproval, mission.Status);
        Assert.Equal(RunStepStatus.Succeeded, mission.Steps.Single(step => step.NodeKey == "planner").Status);
        Assert.Equal(RunStepStatus.Succeeded, mission.Steps.Single(step => step.NodeKey == "researcher").Status);
        Assert.Equal(RunStepStatus.AwaitingApproval, mission.Steps.Single(step => step.NodeKey == "approval").Status);
    }

    [Fact]
    public async Task ApprovalCompletesTheRemainingWorkflow()
    {
        var repository = new InMemoryMissionRepository();
        var service = CreateService(repository);
        var mission = await service.CreateAsync("Fix sample", "Diagnose a failing sample test.", null, null, CancellationToken.None);

        mission = await service.DecideApprovalAsync(mission.Id, ApprovalOutcome.Approved, "Proceed with the plan.", CancellationToken.None);

        Assert.Equal(MissionStatus.Completed, mission.Status);
        Assert.All(mission.Steps, step => Assert.Equal(RunStepStatus.Succeeded, step.Status));
    }

    [Fact]
    public async Task RejectionTerminatesTheMissionAndRecordsTheDecision()
    {
        var repository = new InMemoryMissionRepository();
        var service = CreateService(repository);
        var mission = await service.CreateAsync("Fix sample", "Diagnose a failing sample test.", null, null, CancellationToken.None);

        mission = await service.DecideApprovalAsync(mission.Id, ApprovalOutcome.Rejected, "Do not continue.", CancellationToken.None);

        Assert.Equal(MissionStatus.Rejected, mission.Status);
        Assert.Equal(ApprovalOutcome.Rejected, Assert.Single(mission.Approvals).Outcome);
    }

    [Fact]
    public async Task RevisionRequestRunsAResearchRevisionAndPausesForASecondApproval()
    {
        var repository = new InMemoryMissionRepository();
        var service = CreateService(repository);
        var mission = await service.CreateAsync("Fix sample", "Diagnose a failing sample test.", null, null, CancellationToken.None);

        mission = await service.DecideApprovalAsync(mission.Id, ApprovalOutcome.RevisionRequested, "Find stronger evidence.", CancellationToken.None);

        Assert.Equal(MissionStatus.AwaitingApproval, mission.Status);
        Assert.Equal(RunStepStatus.Succeeded, mission.Steps.Single(step => step.NodeKey == "researcher-revision-1").Status);
        Assert.Equal(RunStepStatus.AwaitingApproval, mission.Steps.Single(step => step.NodeKey == "approval-revision-1").Status);
    }

    [Fact]
    public async Task FailedStepCanBeExplicitlyRetriedWithoutLosingItsErrorAudit()
    {
        var repository = new InMemoryMissionRepository();
        var service = CreateService(repository, new FailsOnceAgentProvider());

        var mission = await service.CreateAsync("Retry sample", "Exercise a recoverable workflow failure.", null, null, CancellationToken.None);

        Assert.Equal(MissionStatus.Failed, mission.Status);
        var planner = mission.Steps.Single(step => step.NodeKey == "planner");
        Assert.Equal(RunStepStatus.Failed, planner.Status);
        Assert.Equal("Transient provider failure.", planner.Error);

        mission = await service.RetryFailedStepAsync(mission.Id, CancellationToken.None);

        planner = mission.Steps.Single(step => step.NodeKey == "planner");
        Assert.Equal(MissionStatus.AwaitingApproval, mission.Status);
        Assert.Equal(RunStepStatus.Succeeded, planner.Status);
        Assert.Equal(1, planner.RetryAttempts);
        Assert.Equal("Transient provider failure.", planner.Error);
    }

    [Fact]
    public async Task TokenBudgetStopsTheWorkflowAndRetainsTheObservedUsage()
    {
        var repository = new InMemoryMissionRepository();
        var service = CreateService(repository, new TokenConsumingAgentProvider());

        var mission = await service.CreateAsync("Budget sample", "Enforce an observed token ceiling.", null, null, 1, null, CancellationToken.None);

        var planner = mission.Steps.Single(step => step.NodeKey == "planner");
        Assert.Equal(MissionStatus.Failed, mission.Status);
        Assert.Equal(RunStepStatus.Failed, planner.Status);
        Assert.Equal(2, planner.TokenUsage);
        Assert.Contains("token budget was exceeded", planner.Error);
        Assert.Equal(RunStepStatus.Pending, mission.Steps.Single(step => step.NodeKey == "researcher").Status);
    }

    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => new(2026, 8, 23, 0, 0, 0, TimeSpan.Zero); }
    private static MissionWorkflowService CreateService(InMemoryMissionRepository repository, IAgentProvider? provider = null) => new(repository, provider ?? new TestAgentProvider(), new TestSampleWorkspace(), new BundledEngineeringMissionCatalog(), new TestConstrainedSampleExecutor(), new TestClock(), NullLogger<MissionWorkflowService>.Instance);
    private sealed class TestAgentProvider : IAgentProvider
    {
        public string Name => "test";
        public Task<AgentAuditOutput> ExecuteAsync(AgentExecutionRequest request, CancellationToken cancellationToken) => Task.FromResult(new AgentAuditOutput("{}", "test rationale", "test", "v1", "[]", "[]", 0, 0m));
    }
    private sealed class FailsOnceAgentProvider : IAgentProvider
    {
        private bool _hasFailed;
        public string Name => "fails-once";
        public Task<AgentAuditOutput> ExecuteAsync(AgentExecutionRequest request, CancellationToken cancellationToken)
        {
            if (!_hasFailed) { _hasFailed = true; throw new InvalidOperationException("Transient provider failure."); }
            return Task.FromResult(new AgentAuditOutput("{}", "test rationale", "fails-once", "v1", "[]", "[]", 0, 0m));
        }
    }
    private sealed class TokenConsumingAgentProvider : IAgentProvider
    {
        public string Name => "token-consuming";
        public Task<AgentAuditOutput> ExecuteAsync(AgentExecutionRequest request, CancellationToken cancellationToken) => Task.FromResult(new AgentAuditOutput("{}", "test rationale", "token-consuming", "v1", "[]", "[]", 2, 0m));
    }
    private sealed class TestSampleWorkspace : ISampleWorkspace
    {
        public IReadOnlyList<SampleDefinition> List() => [];
        public Task<SampleSnapshot?> GetSnapshotAsync(string sampleId, CancellationToken cancellationToken) => Task.FromResult<SampleSnapshot?>(null);
    }
    private sealed class TestConstrainedSampleExecutor : IConstrainedSampleExecutor
    {
        public Task<AgentAuditOutput> ApplyCorrectionAsync(Guid missionId, SampleSnapshot sample, EngineeringMissionTemplate missionTemplate, IReadOnlyList<string> allowedCapabilities, CancellationToken cancellationToken) => Task.FromResult(new AgentAuditOutput("{}", "test operation", "test", "v1", "[]", "[]", 0, 0m));
        public Task<AgentAuditOutput> VerifyCorrectionAsync(Guid missionId, SampleSnapshot sample, EngineeringMissionTemplate missionTemplate, IReadOnlyList<string> allowedCapabilities, CancellationToken cancellationToken) => Task.FromResult(new AgentAuditOutput("{}", "test operation", "test", "v1", "[]", "[]", 0, 0m));
        public Task<IReadOnlyList<SampleFileChange>> GetChangesAsync(Guid missionId, SampleSnapshot sample, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<SampleFileChange>>([]);
        public Task<AgentAuditOutput> CommitReviewedChangeAsync(Guid missionId, SampleSnapshot sample, EngineeringMissionTemplate missionTemplate, IReadOnlyList<string> allowedCapabilities, string summary, CancellationToken cancellationToken) => Task.FromResult(new AgentAuditOutput("{}", "test commit", "test", "v1", "[]", "[]", 0, 0m));
    }
    private sealed class InMemoryMissionRepository : IMissionRepository
    {
        private readonly Dictionary<Guid, Mission> _missions = [];
        public Task AddAsync(Mission mission, CancellationToken cancellationToken) { _missions.Add(mission.Id, mission); return Task.CompletedTask; }
        public Task AddApprovalAsync(ApprovalRecord approval, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddStepAsync(RunStep runStep, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Mission?> GetAsync(Guid missionId, CancellationToken cancellationToken) => Task.FromResult(_missions.GetValueOrDefault(missionId));
        public Task<IReadOnlyList<Mission>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Mission>>(_missions.Values.ToArray());
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
