using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ForgeBoard.Application;
using ForgeBoard.Domain;
using Microsoft.Extensions.Configuration;

namespace ForgeBoard.Infrastructure;

public sealed class OpenAiAgentProvider(HttpClient httpClient, IConfiguration configuration) : IAgentProvider
{
    public string Name => "openai";

    public async Task<AgentAuditOutput> ExecuteAsync(AgentExecutionRequest request, CancellationToken cancellationToken)
    {
        var apiKey = configuration["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("OpenAI is selected but no API key is configured.");
        var model = configuration["OpenAI:Model"] ?? "gpt-5.4";
        using var message = new HttpRequestMessage(HttpMethod.Post, "responses");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        message.Content = JsonContent.Create(new
        {
            model,
            store = false,
            max_output_tokens = 1000,
            instructions = "You are a ForgeBoard engineering role. Return only a JSON object with a structuredOutput object and a concise rationaleSummary string. Do not disclose hidden reasoning. Base claims only on the supplied mission, structured artifacts, and allowlisted sample evidence.",
            input = BuildInput(request)
        });
        using var response = await httpClient.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(body);
        var text = ExtractOutputText(document.RootElement);
        using var outputDocument = JsonDocument.Parse(text);
        if (!outputDocument.RootElement.TryGetProperty("structuredOutput", out var structuredOutput) || !outputDocument.RootElement.TryGetProperty("rationaleSummary", out var rationale))
            throw new InvalidOperationException("OpenAI returned an audit response without the required structuredOutput and rationaleSummary fields.");
        var usage = document.RootElement.TryGetProperty("usage", out var usageElement) && usageElement.TryGetProperty("total_tokens", out var tokens)
            ? tokens.GetInt32() : (int?)null;
        var tools = request.Sample is null ? "[]" : "[\"sample.read\"]";
        var toolResults = request.Sample is null ? "[]" : JsonSerializer.Serialize(new { sample = request.Sample.Id, files = request.Sample.Files.Select(file => new { file.Path, file.Sha256 }) });
        return new AgentAuditOutput(structuredOutput.GetRawText(), rationale.GetString() ?? "Provider supplied no rationale summary.", Name, model, tools, toolResults, usage, null);
    }

    private static object BuildInput(AgentExecutionRequest request) => new
    {
        role = request.Role,
        mission = new { title = request.MissionTitle, description = request.MissionDescription, template = request.Template?.Id },
        artifacts = request.InputArtifacts,
        sample = request.Sample is null ? null : new { request.Sample.Id, request.Sample.Description, files = request.Sample.Files.Select(file => new { file.Path, file.Content, file.Sha256 }) }
    };

    private static string ExtractOutputText(JsonElement response)
    {
        foreach (var item in response.GetProperty("output").EnumerateArray())
        {
            if (item.GetProperty("type").GetString() != "message") continue;
            foreach (var content in item.GetProperty("content").EnumerateArray())
                if (content.GetProperty("type").GetString() == "output_text") return content.GetProperty("text").GetString() ?? string.Empty;
        }
        throw new InvalidOperationException("OpenAI returned no output text.");
    }
}
