using ForgeBoard.Api;
using ForgeBoard.Application;
using ForgeBoard.Domain;
using ForgeBoard.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using OpenTelemetry.Trace;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole();
var connectionString = builder.Configuration.GetConnectionString("ForgeBoard") ?? "Data Source=forgeboard.db";
var samplesRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "samples"));
var runWorkspacesRoot = builder.Configuration["ForgeBoard:RunWorkspacesRoot"] ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "runs");
builder.Services.AddForgeBoardInfrastructure(connectionString, builder.Configuration, samplesRoot, runWorkspacesRoot);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<IMissionAuditExporter, MissionAuditExporter>();
builder.Services.AddScoped<MissionWorkflowService>();
builder.Services.AddSignalR();
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddAspNetCoreInstrumentation());
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ForgeBoardDbContext>();
    await ForgeBoardDatabaseInitializer.EnsureCreatedAndUpgradedAsync(db, CancellationToken.None);
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/api/samples", (ISampleWorkspace samples) => Results.Ok(samples.List()));
app.MapGet("/api/mission-templates", (IEngineeringMissionCatalog catalog) => Results.Ok(catalog.List()));
app.MapGet("/api/missions", async (MissionWorkflowService service, CancellationToken cancellationToken) => Results.Ok(await service.ListAsync(cancellationToken)));
app.MapGet("/api/missions/{missionId:guid}", async (Guid missionId, MissionWorkflowService service, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await service.GetAsync(missionId, cancellationToken)); }
    catch (KeyNotFoundException) { return Results.NotFound(); }
});
app.MapGet("/api/missions/{missionId:guid}/changes", async (Guid missionId, MissionWorkflowService service, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(await service.GetChangesAsync(missionId, cancellationToken)); }
    catch (KeyNotFoundException) { return Results.NotFound(); }
    catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
});
app.MapGet("/api/missions/{missionId:guid}/audit-export", async (Guid missionId, MissionWorkflowService service, IMissionAuditExporter exporter, CancellationToken cancellationToken) =>
{
    try
    {
        var export = exporter.Create(await service.GetAsync(missionId, cancellationToken));
        return Results.File(export.Content, "application/json", export.FileName);
    }
    catch (KeyNotFoundException) { return Results.NotFound(); }
});
app.MapPost("/api/missions", async (CreateMissionRequest request, MissionWorkflowService service, IHubContext<MissionHub> hub, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description) || request.SampleId?.Length > 100 || request.MaxTokenUsage is <= 0 || request.MaxEstimatedCost is <= 0)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["mission"] = ["Title and description are required."] });
    try
    {
        var mission = await service.CreateAsync(request.Title, request.Description, request.SampleId, request.TemplateId, request.MaxTokenUsage, request.MaxEstimatedCost, cancellationToken);
        await hub.Clients.All.SendAsync("missionUpdated", mission.Id, cancellationToken);
        return Results.Created($"/api/missions/{mission.Id}", mission);
    }
    catch (ArgumentException exception) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["mission"] = [exception.Message] }); }
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
app.MapPost("/api/missions/{missionId:guid}/retry", async (Guid missionId, MissionWorkflowService service, IHubContext<MissionHub> hub, CancellationToken cancellationToken) =>
{
    try
    {
        var mission = await service.RetryFailedStepAsync(missionId, cancellationToken);
        await hub.Clients.All.SendAsync("missionUpdated", mission.Id, cancellationToken);
        return Results.Ok(mission);
    }
    catch (KeyNotFoundException) { return Results.NotFound(); }
    catch (InvalidOperationException exception) { return Results.Conflict(new { error = exception.Message }); }
});
app.MapHub<MissionHub>("/hubs/missions");
app.Run();

public partial class Program;
