using ForgeBoard.Domain;

namespace ForgeBoard.Application;

public interface IMissionRepository
{
    Task AddAsync(Mission mission, CancellationToken cancellationToken);
    Task AddApprovalAsync(ApprovalRecord approval, CancellationToken cancellationToken);
    Task AddStepAsync(RunStep runStep, CancellationToken cancellationToken);
    Task<Mission?> GetAsync(Guid missionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Mission>> ListAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IAgentProvider
{
    string Name { get; }
    Task<AgentAuditOutput> ExecuteAsync(AgentExecutionRequest request, CancellationToken cancellationToken);
}

public sealed record AgentExecutionRequest(string Role, string MissionTitle, string MissionDescription, IReadOnlyList<ArtifactReference> InputArtifacts, SampleSnapshot? Sample, EngineeringMissionTemplate? Template = null);
public sealed record ArtifactReference(string NodeKey, string StructuredOutput);

public sealed record SampleDefinition(string Id, string DisplayName, string Description);
public sealed record SampleFileEvidence(string Path, string Content, string Sha256);
public sealed record SampleSnapshot(string Id, string Description, IReadOnlyList<SampleFileEvidence> Files)
{
    public object ToAuditReference() => new { Id, Description, Files = Files.Select(file => new { file.Path, file.Sha256 }) };
}
public sealed record SampleFileChange(string Path, string OriginalContent, string UpdatedContent);

public interface ISampleWorkspace
{
    IReadOnlyList<SampleDefinition> List();
    Task<SampleSnapshot?> GetSnapshotAsync(string sampleId, CancellationToken cancellationToken);
}

public sealed record EngineeringMissionTemplate(string Id, string DisplayName, string Description, string SampleId, IReadOnlyList<string> AllowedCapabilities);

public interface IEngineeringMissionCatalog
{
    IReadOnlyList<EngineeringMissionTemplate> List();
    EngineeringMissionTemplate? Find(string templateId);
    EngineeringMissionTemplate? GetDefaultForSample(string sampleId);
}

/// <summary>
/// Performs narrowly defined, deterministic operations in an isolated copy of an allowlisted sample.
/// This is deliberately separate from <see cref="IAgentProvider"/>: source changes and verification
/// must remain explicit application capabilities rather than model-directed tool calls.
/// </summary>
public interface IConstrainedSampleExecutor
{
    Task<AgentAuditOutput> ApplyCorrectionAsync(Guid missionId, SampleSnapshot sample, EngineeringMissionTemplate missionTemplate, IReadOnlyList<string> allowedCapabilities, CancellationToken cancellationToken);
    Task<AgentAuditOutput> VerifyCorrectionAsync(Guid missionId, SampleSnapshot sample, EngineeringMissionTemplate missionTemplate, IReadOnlyList<string> allowedCapabilities, CancellationToken cancellationToken);
    Task<IReadOnlyList<SampleFileChange>> GetChangesAsync(Guid missionId, SampleSnapshot sample, CancellationToken cancellationToken);
}

public interface IClock { DateTimeOffset UtcNow { get; } }
public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
