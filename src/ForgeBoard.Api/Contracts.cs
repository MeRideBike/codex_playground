using ForgeBoard.Domain;

namespace ForgeBoard.Api;

public sealed record CreateMissionRequest(string Title, string Description);
public sealed record ApprovalDecisionRequest(ApprovalOutcome Outcome, string Comment);
public sealed class MissionHub : Microsoft.AspNetCore.SignalR.Hub { }
