# ForgeBoard showcase walkthrough

ForgeBoard is intended to be evaluated as an engineering system, not as a chat demo. This three-minute walkthrough highlights the parts worth inspecting in a code review or interview.

## 1. Start a bounded mission

Choose **Diagnose failing test** in the dashboard. The Planner and Researcher record structured artifacts from a hashed, allowlisted sample snapshot. ForgeBoard pauses before any source modification and shows the immutable operation policy for the mission.

## 2. Make an accountable decision

Approve, reject, or request a revision. A revision produces an additional researcher pass and a second approval gate. Every decision is durable data, not a transient chat message.

## 3. Inspect a real verified change

After approval, the Implementer applies one reviewed correction in an application-owned workspace. The Test Engineer performs two checks:

- a deterministic source assertion; and
- an executable, dependency-free .NET verification harness.

The audit record includes the exact approved process operation, exit code, duration, stdout, stderr, and the before/after source content.

## 4. Record a reviewable change

For completed mutable runs, select **Record isolated Git commit**. ForgeBoard creates one commit in its disposable workspace and records its SHA and diff summary. It never reads, modifies, stages, commits, or pushes the ForgeBoard checkout—or any user repository.

## What to inspect in the code

| Concern | Where to look |
| --- | --- |
| Domain state transitions | `src/ForgeBoard.Domain/Mission.cs` |
| Workflow orchestration and approval behavior | `src/ForgeBoard.Application/MissionWorkflowService.cs` |
| Capability boundaries | `src/ForgeBoard.Infrastructure/ConstrainedSampleExecutor.cs` |
| Fixed runtime verification | `src/ForgeBoard.Infrastructure/ApprovedDotnetVerificationRunner.cs` |
| Fixed isolated Git operations | `src/ForgeBoard.Infrastructure/IsolatedGitWorkspace.cs` |
| Full HTTP workflow coverage | `tests/ForgeBoard.IntegrationTests/MissionApiTests.cs` |

## Deliberate limits

ForgeBoard currently accepts only bundled samples. It does not claim to safely run arbitrary code, access an arbitrary local path, clone a remote repository, or generate a pull request. Those are future capabilities because they need stronger sandboxing, permission policy, identity, and review design.
