using ForgeBoard.Api;
using ForgeBoard.Application;
using ForgeBoard.Domain;
using ForgeBoard.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using OpenTelemetry.Trace;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("ForgeBoard") ?? "Data Source=forgeboard.db";
builder.Services.AddForgeBoardInfrastructure(connectionString);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<MissionWorkflowService>();
builder.Services.AddSignalR();
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddAspNetCoreInstrumentation());
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();
app.UseHttpsRedirection();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ForgeBoardDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/api/missions", async (MissionWorkflowService service, CancellationToken cancellationToken) => Results.Ok(await service.ListAsync(cancellationToken)));
app.MapGet("/api/missions/{missionId:guid}", async (Guid missionId, MissionWorkflowService service, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await service.GetAsync(missionId, cancellationToken)); }
    catch (KeyNotFoundException) { return Results.NotFound(); }
});
app.MapPost("/api/missions", async (CreateMissionRequest request, MissionWorkflowService service, IHubContext<MissionHub> hub, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["mission"] = ["Title and description are required."] });
    var mission = await service.CreateAsync(request.Title, request.Description, cancellationToken);
    await hub.Clients.All.SendAsync("missionUpdated", mission.Id, cancellationToken);
    return Results.Created($"/api/missions/{mission.Id}", mission);
});
app.MapPost("/api/missions/{missionId:guid}/approval", async (Guid missionId, ApprovalDecisionRequest request, MissionWorkflowService service, IHubContext<MissionHub> hub, CancellationToken cancellationToken) =>
{
    if (!Enum.IsDefined(request.Outcome) || string.IsNullOrWhiteSpace(request.Comment))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["approval"] = ["A valid outcome and comment are required."] });
    try
    {
        var mission = await service.DecideApprovalAsync(missionId, request.Outcome, request.Comment, cancellationToken);
        await hub.Clients.All.SendAsync("missionUpdated", mission.Id, cancellationToken);
        return Results.Ok(mission);
    }
    catch (KeyNotFoundException) { return Results.NotFound(); }
    catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
});
app.MapHub<MissionHub>("/hubs/missions");
app.Run();

public partial class Program;
