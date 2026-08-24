using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ForgeBoard.Domain;

namespace ForgeBoard.Application;

public sealed record MissionAuditExport(byte[] Content, string FileName, string Sha256);

public interface IMissionAuditExporter
{
    MissionAuditExport Create(Mission mission);
}

public sealed class MissionAuditExporter(IClock clock) : IMissionAuditExporter
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public MissionAuditExport Create(Mission mission)
    {
        ArgumentNullException.ThrowIfNull(mission);
        var exportedAt = clock.UtcNow;
        var payload = new { schemaVersion = "forgeboard.audit.v1", exportedAt, replay = new { workflowName = mission.WorkflowName, templateId = mission.TemplateId, guidance = "This export records observable artifacts and decisions only; it does not contain hidden model reasoning." }, mission };
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, Options);
        var sha256 = Convert.ToHexString(SHA256.HashData(payloadBytes)).ToLowerInvariant();
        var document = new { payload, integrity = new { algorithm = "SHA-256", payloadSha256 = sha256 } };
        return new MissionAuditExport(JsonSerializer.SerializeToUtf8Bytes(document, Options), $"forgeboard-audit-{mission.Id:N}.json", sha256);
    }
}
