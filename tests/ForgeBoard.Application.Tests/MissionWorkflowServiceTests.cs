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
        var service = new MissionWorkflowService(repository, new TestAgentProvider(), new TestClock(), NullLogger<MissionWorkflowService>.Instance);

        var mission = await service.CreateAsync("Fix sample", "Diagnose a failing sample test.", CancellationToken.None);

        Assert.Equal(MissionStatus.AwaitingApproval, mission.Status);
        Assert.Equal(RunStepStatus.Succeeded, mission.Steps.Single(step => step.NodeKey == "planner").Status);
        Assert.Equal(RunStepStatus.Succeeded, mission.Steps.Single(step => step.NodeKey == "researcher").Status);
        Assert.Equal(RunStepStatus.AwaitingApproval, mission.Steps.Single(step => step.NodeKey == "approval").Status);
    }

    [Fact]
    public async Task ApprovalCompletesTheRemainingWorkflow()
    {
        var repository = new InMemoryMissionRepository();
        var service = new MissionWorkflowService(repository, new TestAgentProvider(), new TestClock(), NullLogger<MissionWorkflowService>.Instance);
        var mission = await service.CreateAsync("Fix sample", "Diagnose a failing sample test.", CancellationToken.None);

        mission = await service.DecideApprovalAsync(mission.Id, ApprovalOutcome.Approved, "Proceed with the plan.", CancellationToken.None);

        Assert.Equal(MissionStatus.Completed, mission.Status);
        Assert.All(mission.Steps, step => Assert.Equal(RunStepStatus.Succeeded, step.Status));
    }

    [Fact]
    public async Task RejectionTerminatesTheMissionAndRecordsTheDecision()
    {
        var repository = new InMemoryMissionRepository();
        var service = new MissionWorkflowService(repository, new TestAgentProvider(), new TestClock(), NullLogger<MissionWorkflowService>.Instance);
        var mission = await service.CreateAsync("Fix sample", "Diagnose a failing sample test.", CancellationToken.None);

        mission = await service.DecideApprovalAsync(mission.Id, ApprovalOutcome.Rejected, "Do not continue.", CancellationToken.None);

        Assert.Equal(MissionStatus.Rejected, mission.Status);
        Assert.Equal(ApprovalOutcome.Rejected, Assert.Single(mission.Approvals).Outcome);
    }

    [Fact]
    public async Task RevisionRequestRunsAResearchRevisionAndPausesForASecondApproval()
    {
        var repository = new InMemoryMissionRepository();
        var service = new MissionWorkflowService(repository, new TestAgentProvider(), new TestClock(), NullLogger<MissionWorkflowService>.Instance);
        var mission = await service.CreateAsync("Fix sample", "Diagnose a failing sample test.", CancellationToken.None);

        mission = await service.DecideApprovalAsync(mission.Id, ApprovalOutcome.RevisionRequested, "Find stronger evidence.", CancellationToken.None);

        Assert.Equal(MissionStatus.AwaitingApproval, mission.Status);
        Assert.Equal(RunStepStatus.Succeeded, mission.Steps.Single(step => step.NodeKey == "researcher-revision-1").Status);
        Assert.Equal(RunStepStatus.AwaitingApproval, mission.Steps.Single(step => step.NodeKey == "approval-revision-1").Status);
    }

    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => new(2026, 8, 23, 0, 0, 0, TimeSpan.Zero); }
    private sealed class TestAgentProvider : IAgentProvider
    {
        public string Name => "test";
        public Task<AgentAuditOutput> ExecuteAsync(AgentExecutionRequest request, CancellationToken cancellationToken) => Task.FromResult(new AgentAuditOutput("{}", "test rationale", "test", "v1", "[]", "[]", 0, 0m));
    }
    private sealed class InMemoryMissionRepository : IMissionRepository
    {
        private readonly Dictionary<Guid, Mission> _missions = [];
        public Task AddAsync(Mission mission, CancellationToken cancellationToken) { _missions.Add(mission.Id, mission); return Task.CompletedTask; }
        public Task<Mission?> GetAsync(Guid missionId, CancellationToken cancellationToken) => Task.FromResult(_missions.GetValueOrDefault(missionId));
        public Task<IReadOnlyList<Mission>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Mission>>(_missions.Values.ToArray());
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
