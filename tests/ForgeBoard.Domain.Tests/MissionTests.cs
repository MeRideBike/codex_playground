using ForgeBoard.Domain;

namespace ForgeBoard.Domain.Tests;

public sealed class MissionTests
{
    [Fact]
    public void MissionCannotCompleteBeforeItHasStarted()
    {
        var mission = new Mission("Fix test", "Diagnose the sample", "Initial", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => mission.Complete(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ApprovalStepRecordsItsTerminalState()
    {
        var step = new RunStep("approval", "Human Approval", WorkflowNodeKind.HumanApproval);
        step.WaitForApproval(DateTimeOffset.UtcNow);
        step.Approve(DateTimeOffset.UtcNow);

        Assert.Equal(RunStepStatus.Succeeded, step.Status);
        Assert.NotNull(step.CompletedAt);
    }
}
