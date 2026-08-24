using ForgeBoard.Domain;

namespace ForgeBoard.Api;

public sealed record CreateMissionRequest(string Title, string Description, string? SampleId = null, string? TemplateId = null, int? MaxTokenUsage = null, decimal? MaxEstimatedCost = null);
public sealed record ApprovalDecisionRequest(ApprovalOutcome Outcome, string Comment);
public sealed class MissionHub : Microsoft.AspNetCore.SignalR.Hub { }
