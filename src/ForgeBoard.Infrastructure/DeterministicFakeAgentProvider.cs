using ForgeBoard.Application;
using ForgeBoard.Domain;

namespace ForgeBoard.Infrastructure;

public sealed class DeterministicFakeAgentProvider : IAgentProvider
{
    public string Name => "deterministic-fake";

    public Task<AgentAuditOutput> ExecuteAsync(AgentExecutionRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        object result = request.Role switch
        {
            "Planner" => new { objective = request.MissionDescription, plan = new[] { "Inspect supplied evidence", "Identify a minimal correction", "Verify the result" } },
            "Researcher" => ResearchResult(),
            "Implementer" => new { change = "Proposed minimal code correction", changedFiles = Array.Empty<string>(), applied = false },
            "Test Engineer" => new { verification = "No host command execution is permitted in Milestone 1.", result = "not-run" },
            "Reviewer" => new { conclusion = "Review complete; inspect artifacts and verification status before merging.", approved = false },
            _ when request.Role.StartsWith("Researcher Revision", StringComparison.Ordinal) => ResearchResult(),
            _ => new { message = "No deterministic script exists for this role." }
        };
        var output = System.Text.Json.JsonSerializer.Serialize(result);
        var audit = new AgentAuditOutput(output, $"Deterministic {request.Role} result generated from mission inputs and prior structured artifacts.", Name, "forgeboard-fake-v1", "[]", "[]", 0, 0m);
        return Task.FromResult(audit);
    }

    private static object ResearchResult() => new { finding = "The supplied sample contains a deterministic defect.", evidence = "Sample test fixture", recommendation = "Apply the smallest corrective change." };
}
