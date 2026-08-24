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
            "Planner" => new { objective = request.MissionDescription, template = request.Template?.Id, sample = request.Sample?.Id, plan = new[] { "Inspect allowlisted sample evidence", "Identify the reviewed task-specific action", "Verify the result" } },
            "Researcher" => ResearchResult(request.Sample, request.Template?.Id),
            "Implementer" => new { change = "Proposed minimal code correction", changedFiles = Array.Empty<string>(), applied = false },
            "Test Engineer" => new { verification = "No host command execution is permitted in Milestone 1.", result = "not-run" },
            "Reviewer" => new { conclusion = "Review complete; inspect artifacts and verification status before merging.", approved = false },
            _ when request.Role.StartsWith("Researcher Revision", StringComparison.Ordinal) => ResearchResult(request.Sample, request.Template?.Id),
            _ => new { message = "No deterministic script exists for this role." }
        };
        var output = System.Text.Json.JsonSerializer.Serialize(result);
        var toolResults = request.Sample is null ? "[]" : System.Text.Json.JsonSerializer.Serialize(new { sample = request.Sample.Id, files = request.Sample.Files.Select(file => new { file.Path, file.Sha256 }) });
        var tools = request.Sample is null ? "[]" : "[\"sample.read\"]";
        var audit = new AgentAuditOutput(output, $"Deterministic {request.Role} result generated from mission inputs, structured artifacts, and allowlisted sample evidence.", Name, "forgeboard-fake-v1", tools, toolResults, 0, 0m);
        return Task.FromResult(audit);
    }

    private static object ResearchResult(SampleSnapshot? sample, string? templateId)
    {
        if (templateId == "identify-security-defect")
            return new { finding = "GreetingRenderer.Render inserts user-controlled input directly into HTML.", evidence = "GreetingRenderer.cs interpolates name inside an h1 element without encoding.", recommendation = "Encode name with System.Net.WebUtility.HtmlEncode before rendering." };
        if (templateId == "find-inefficient-implementation")
            return new { finding = "PrefixReport repeatedly enumerates every prefix of the input.", evidence = "PrefixReport.cs calls values.Take(index).Sum() once for each index.", recommendation = "Accumulate a running prefix sum in one pass." };
        if (templateId == "recommend-architecture-improvement")
            return new { finding = "Presentation markup and input handling are coupled in UserInputFormatter.", evidence = "UserInputFormatter.cs constructs HTML directly from its input.", recommendation = "Introduce a presentation boundary that owns encoding and markup generation." };
        var calculator = sample?.Files.SingleOrDefault(file => file.Path == "Calculator.cs")?.Content;
        var hasAdditionDefect = calculator?.Contains("Subtract(int left, int right) => left + right", StringComparison.Ordinal) == true;
        return hasAdditionDefect
            ? new { finding = "Calculator.Subtract adds instead of subtracting its right operand.", evidence = "Calculator.cs: Subtract returns left + right; CalculatorTests expects Calculator.Subtract(5, 3) to equal 2.", recommendation = "Replace the addition operator with subtraction and retain the existing test." }
            : new { finding = "No scripted calculator defect was found in the supplied sample evidence.", evidence = "Allowlisted sample snapshot", recommendation = "Review the captured files manually." };
    }
}
