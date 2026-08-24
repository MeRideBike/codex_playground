using System.Net;
using System.Net.Http.Json;
using ForgeBoard.Infrastructure;
using ForgeBoard.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;

namespace ForgeBoard.IntegrationTests;

public sealed class MissionApiTests : IClassFixture<ForgeBoardApiFactory>
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web);
    private readonly HttpClient _client;
    public MissionApiTests(ForgeBoardApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task CreateMissionRunsToApprovalAndIsRetrievable()
    {
        var response = await _client.PostAsJsonAsync("/api/missions", new { title = "Fix sample", description = "Diagnose its failing test." });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<MissionResponse>();
        Assert.NotNull(created);
        Assert.Equal("AwaitingApproval", created.Status);

        var detail = await _client.GetFromJsonAsync<MissionResponse>($"/api/missions/{created.Id}");
        Assert.NotNull(detail);
        Assert.Equal(6, detail.Steps.Count);

        var missions = await _client.GetFromJsonAsync<List<MissionResponse>>("/api/missions");
        Assert.Contains(missions!, mission => mission.Id == created.Id);
    }

    [Fact]
    public async Task DeterministicProviderEmitsACompleteAuditContractWithoutCredentials()
    {
        var provider = new DeterministicFakeAgentProvider();
        var output = await provider.ExecuteAsync(new AgentExecutionRequest("Planner", "Sample", "Inspect the sample.", [], null), CancellationToken.None);

        Assert.Equal("deterministic-fake", output.Provider);
        Assert.Equal(0, output.TokenUsage);
        Assert.Equal(0m, output.EstimatedCost);
        Assert.False(string.IsNullOrWhiteSpace(output.RationaleSummary));
    }

    [Fact]
    public async Task AllowlistedSampleMissionPersistsToolEvidenceWithoutHostCommandExecution()
    {
        var samples = await _client.GetFromJsonAsync<List<SampleResponse>>("/api/samples");
        Assert.Contains(samples!, sample => sample.Id == "defective-calculator");
        Assert.Contains(samples!, sample => sample.Id == "insecure-greeting");
        Assert.Contains(samples!, sample => sample.Id == "inefficient-reporting");

        var response = await _client.PostAsJsonAsync("/api/missions", new { title = "Diagnose calculator", description = "Inspect the bundled sample.", sampleId = "defective-calculator" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var mission = await response.Content.ReadFromJsonAsync<MissionResponse>();
        Assert.NotNull(mission);
        Assert.Equal("defective-calculator", mission.SampleId);
        var researcher = mission.Steps.Single(step => step.NodeKey == "researcher");
        Assert.Contains("sample.read", researcher.ToolsInvoked);
        Assert.Contains("Calculator.cs", researcher.ToolResults);
        Assert.Contains("adds instead of subtracting", researcher.StructuredOutput);

        var unsupported = await _client.PostAsJsonAsync("/api/missions", new { title = "Unsafe", description = "Do not inspect outside samples.", sampleId = ".." });
        Assert.Equal(HttpStatusCode.BadRequest, unsupported.StatusCode);
    }

    [Fact]
    public async Task ApprovalExecutesTheReviewedSampleCorrectionAndStaticVerification()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/missions", new
        {
            title = "Fix calculator",
            description = "Correct the bundled calculator defect.",
            sampleId = "defective-calculator"
        });
        var awaitingApproval = await createResponse.Content.ReadFromJsonAsync<MissionResponse>();
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(awaitingApproval);

        var approvalResponse = await _client.PostAsJsonAsync($"/api/missions/{awaitingApproval.Id}/approval", new
        {
            outcome = "Approved",
            comment = "The isolated correction is authorized."
        });

        var approvalBody = await approvalResponse.Content.ReadAsStringAsync();
        Assert.True(approvalResponse.StatusCode == HttpStatusCode.OK, approvalBody);
        var completed = System.Text.Json.JsonSerializer.Deserialize<MissionResponse>(approvalBody, JsonOptions);
        Assert.NotNull(completed);
        Assert.Equal("Completed", completed.Status);
        var implementer = completed.Steps.Single(step => step.NodeKey == "implementer");
        Assert.Contains("deterministic-sample-executor", implementer.Provider);
        Assert.Contains("Subtract now subtracts", implementer.StructuredOutput);
        var testEngineer = completed.Steps.Single(step => step.NodeKey == "test-engineer");
        Assert.Contains("Static verification passed", testEngineer.StructuredOutput);
        Assert.Contains("sample.assert", testEngineer.ToolsInvoked);
    }

    [Fact]
    public async Task SecurityTemplateSelectsItsOwnReviewedCorrectionAndAuditEvidence()
    {
        var templates = await _client.GetFromJsonAsync<List<TemplateResponse>>("/api/mission-templates");
        Assert.Contains(templates!, template => template.Id == "identify-security-defect" && template.SampleId == "insecure-greeting");

        var createResponse = await _client.PostAsJsonAsync("/api/missions", new
        {
            title = "Encode greeting input",
            description = "Correct the output encoding defect.",
            templateId = "identify-security-defect"
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var awaitingApproval = await createResponse.Content.ReadFromJsonAsync<MissionResponse>();
        Assert.NotNull(awaitingApproval);
        Assert.Equal("insecure-greeting", awaitingApproval.SampleId);
        Assert.Equal("identify-security-defect", awaitingApproval.TemplateId);
        Assert.Contains("directly into HTML", awaitingApproval.Steps.Single(step => step.NodeKey == "researcher").StructuredOutput);

        var approvalResponse = await _client.PostAsJsonAsync($"/api/missions/{awaitingApproval.Id}/approval", new { outcome = "Approved", comment = "Apply the reviewed output encoding correction." });
        Assert.Equal(HttpStatusCode.OK, approvalResponse.StatusCode);
        var completed = await approvalResponse.Content.ReadFromJsonAsync<MissionResponse>();
        Assert.NotNull(completed);
        Assert.Equal("Completed", completed.Status);
        Assert.Contains("HTML encoded", completed.Steps.Single(step => step.NodeKey == "implementer").StructuredOutput);
        Assert.Contains("Raw name interpolation is absent", completed.Steps.Single(step => step.NodeKey == "test-engineer").StructuredOutput);
    }

    [Fact]
    public async Task RevisionRequestPersistsNewStepsAndPausesForTheSecondApproval()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/missions", new { title = "Revise research", description = "Request stronger evidence before the reviewed change." });
        var created = await createResponse.Content.ReadFromJsonAsync<MissionResponse>();
        Assert.NotNull(created);

        var revisionResponse = await _client.PostAsJsonAsync($"/api/missions/{created.Id}/approval", new { outcome = "RevisionRequested", comment = "Please provide another research pass." });

        var revisionBody = await revisionResponse.Content.ReadAsStringAsync();
        Assert.True(revisionResponse.StatusCode == HttpStatusCode.OK, revisionBody);
        var revised = System.Text.Json.JsonSerializer.Deserialize<MissionResponse>(revisionBody, JsonOptions);
        Assert.NotNull(revised);
        Assert.Equal("AwaitingApproval", revised.Status);
        Assert.Contains(revised.Steps, step => step.NodeKey == "researcher-revision-1" && step.Status == "Succeeded");
        Assert.Contains(revised.Steps, step => step.NodeKey == "approval-revision-1" && step.Status == "AwaitingApproval");
    }

    [Fact]
    public async Task CompletedSampleMissionExposesTheAuditableSourceChange()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/missions", new { title = "Show source change", description = "Make the reviewed calculator correction.", templateId = "diagnose-failing-test" });
        var created = await createResponse.Content.ReadFromJsonAsync<MissionResponse>();
        Assert.NotNull(created);
        var approvalResponse = await _client.PostAsJsonAsync($"/api/missions/{created.Id}/approval", new { outcome = "Approved", comment = "Proceed with the reviewed correction." });
        Assert.Equal(HttpStatusCode.OK, approvalResponse.StatusCode);

        var changes = await _client.GetFromJsonAsync<List<SampleFileChangeResponse>>($"/api/missions/{created.Id}/changes");

        var change = Assert.Single(changes!);
        Assert.Equal("Calculator.cs", change.Path);
        Assert.Contains("left + right", change.OriginalContent);
        Assert.Contains("left - right", change.UpdatedContent);
    }

    [Fact]
    public async Task MissionAuditExportContainsVersionedReplayMetadataAndIntegrityHash()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/missions", new { title = "Export audit", description = "Export the observable workflow record." });
        var mission = await createResponse.Content.ReadFromJsonAsync<MissionResponse>();
        Assert.NotNull(mission);

        var exportResponse = await _client.GetAsync($"/api/missions/{mission.Id}/audit-export");

        Assert.Equal(HttpStatusCode.OK, exportResponse.StatusCode);
        Assert.Equal("application/json", exportResponse.Content.Headers.ContentType?.MediaType);
        var document = System.Text.Json.JsonDocument.Parse(await exportResponse.Content.ReadAsStringAsync());
        Assert.Equal("forgeboard.audit.v1", document.RootElement.GetProperty("payload").GetProperty("schemaVersion").GetString());
        Assert.Equal(mission.Id.ToString(), document.RootElement.GetProperty("payload").GetProperty("mission").GetProperty("id").GetString());
        Assert.Equal("SHA-256", document.RootElement.GetProperty("integrity").GetProperty("algorithm").GetString());
        Assert.Matches("^[a-f0-9]{64}$", document.RootElement.GetProperty("integrity").GetProperty("payloadSha256").GetString()!);
    }

    [Fact]
    public async Task TemplateMissionPersistsItsImmutableOperationPolicyAndBudget()
    {
        var response = await _client.PostAsJsonAsync("/api/missions", new
        {
            title = "Budgeted calculator correction",
            description = "Use reviewed operations within a bounded mission.",
            templateId = "diagnose-failing-test",
            maxTokenUsage = 25,
            maxEstimatedCost = 1.25m
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var mission = await response.Content.ReadFromJsonAsync<MissionResponse>();
        Assert.NotNull(mission);
        Assert.Equal(25, mission.MaxTokenUsage);
        Assert.Equal(1.25m, mission.MaxEstimatedCost);
        Assert.Contains("sample.patch.apply", mission.OperationPolicy);
        Assert.Contains("sample.assert", mission.OperationPolicy);
    }

    [Fact]
    public async Task OpenAiProviderUsesResponsesContractWithoutExposingTheKeyInAuditOutput()
    {
        var handler = new StubHttpMessageHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/v1/") };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["OpenAI:ApiKey"] = "test-key", ["OpenAI:Model"] = "test-model" }).Build();
        var provider = new OpenAiAgentProvider(client, configuration);

        var output = await provider.ExecuteAsync(new AgentExecutionRequest("Planner", "Sample", "Inspect it.", [], null), CancellationToken.None);

        Assert.Equal("openai", output.Provider);
        Assert.Equal("test-model", output.Model);
        Assert.Equal(12, output.TokenUsage);
        Assert.Equal("{\"finding\":\"test\"}", output.StructuredOutput);
        Assert.Contains("\"store\":false", handler.RequestBody);
        Assert.DoesNotContain("test-key", output.StructuredOutput);
    }

    private sealed record SampleResponse(string Id, string DisplayName, string Description);
    private sealed record TemplateResponse(string Id, string DisplayName, string Description, string SampleId);
    private sealed record MissionResponse(Guid Id, string Status, List<StepResponse> Steps, string? SampleId = null, string? TemplateId = null, int? MaxTokenUsage = null, decimal? MaxEstimatedCost = null, string OperationPolicy = "[]");
    private sealed record StepResponse(string NodeKey, string Status, string? ToolsInvoked = null, string? ToolResults = null, string? StructuredOutput = null, string? Provider = null);
    private sealed record SampleFileChangeResponse(string Path, string OriginalContent, string UpdatedContent);

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            var auditOutput = System.Text.Json.JsonSerializer.Serialize(new { structuredOutput = new { finding = "test" }, rationaleSummary = "Short audit summary." });
            var response = System.Text.Json.JsonSerializer.Serialize(new
            {
                output = new[] { new { type = "message", content = new[] { new { type = "output_text", text = auditOutput } } } },
                usage = new { total_tokens = 12 }
            });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }
}

public sealed class ForgeBoardApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"forgeboard-integration-{Guid.NewGuid():N}.db");
    private readonly string _runWorkspacesRoot = Path.Combine(Path.GetTempPath(), $"forgeboard-integration-runs-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ForgeBoard"] = $"Data Source={_databasePath}"
                , ["ForgeBoard:RunWorkspacesRoot"] = _runWorkspacesRoot
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(_databasePath)) File.Delete(_databasePath);
        if (disposing && Directory.Exists(_runWorkspacesRoot)) Directory.Delete(_runWorkspacesRoot, recursive: true);
    }
}
