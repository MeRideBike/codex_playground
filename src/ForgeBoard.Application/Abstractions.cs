using ForgeBoard.Domain;

namespace ForgeBoard.Application;

public interface IMissionRepository
{
    Task AddAsync(Mission mission, CancellationToken cancellationToken);
    Task<Mission?> GetAsync(Guid missionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Mission>> ListAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IAgentProvider
{
    string Name { get; }
    Task<AgentAuditOutput> ExecuteAsync(AgentExecutionRequest request, CancellationToken cancellationToken);
}

public sealed record AgentExecutionRequest(string Role, string MissionTitle, string MissionDescription, IReadOnlyList<ArtifactReference> InputArtifacts);
public sealed record ArtifactReference(string NodeKey, string StructuredOutput);

public interface IClock { DateTimeOffset UtcNow { get; } }
public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
