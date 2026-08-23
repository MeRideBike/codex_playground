using System.Net;
using System.Net.Http.Json;
using ForgeBoard.Infrastructure;
using ForgeBoard.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ForgeBoard.IntegrationTests;

public sealed class MissionApiTests : IClassFixture<ForgeBoardApiFactory>
{
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
        var output = await provider.ExecuteAsync(new AgentExecutionRequest("Planner", "Sample", "Inspect the sample.", []), CancellationToken.None);

        Assert.Equal("deterministic-fake", output.Provider);
        Assert.Equal(0, output.TokenUsage);
        Assert.Equal(0m, output.EstimatedCost);
        Assert.False(string.IsNullOrWhiteSpace(output.RationaleSummary));
    }

    private sealed record MissionResponse(Guid Id, string Status, List<StepResponse> Steps);
    private sealed record StepResponse(string NodeKey, string Status);
}

public sealed class ForgeBoardApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"forgeboard-integration-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ForgeBoard"] = $"Data Source={_databasePath}"
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && File.Exists(_databasePath)) File.Delete(_databasePath);
    }
}
