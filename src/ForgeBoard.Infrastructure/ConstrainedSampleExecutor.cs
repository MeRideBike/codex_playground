using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ForgeBoard.Application;
using ForgeBoard.Domain;

namespace ForgeBoard.Infrastructure;

/// <summary>
/// Applies only reviewed, template-specific operations to an isolated copy of an allowlisted sample.
/// No command, arbitrary path, or model-produced patch is accepted by this capability.
/// </summary>
public sealed class ConstrainedSampleExecutor : IConstrainedSampleExecutor
{
    private const string CalculatorSampleId = "defective-calculator";
    private const string SecuritySampleId = "insecure-greeting";
    private const string PerformanceSampleId = "inefficient-reporting";
    private const string CalculatorFile = "Calculator.cs";
    private const string CalculatorProject = "Calculator.Verification.csproj";
    private const string FormatterFile = "GreetingRenderer.cs";
    private const string GreetingProject = "GreetingRenderer.Verification.csproj";
    private const string ReportFile = "PrefixReport.cs";
    private const string ReportingProject = "PrefixReport.Verification.csproj";
    private const string DefectiveSubtract = "Subtract(int left, int right) => left + right";
    private const string CorrectSubtract = "Subtract(int left, int right) => left - right";
    private const string UnsafeGreeting = "$\"<h1>Hello {name}</h1>\"";
    private const string SafeGreeting = "$\"<h1>Hello {System.Net.WebUtility.HtmlEncode(name)}</h1>\"";
    private const string InefficientPrefixLoop = "var total = 0;\n        for (var index = 1; index <= values.Count; index++) total += values.Take(index).Sum();\n        return total;";
    private const string LinearPrefixLoop = "var total = 0;\n        var running = 0;\n        foreach (var value in values)\n        {\n            running += value;\n            total += running;\n        }\n        return total;";
    private static readonly string[] ArchitectureEvidence = ["UserInputFormatter mixes presentation markup and input handling."];
    private static readonly string[] ArchitectureVerificationChecks = ["No source change was expected for this advisory template"];
    private static readonly string[] MutationCapabilities = ["sample.workspace.create", "sample.write", "sample.patch.apply"];
    private static readonly string[] AdvisoryImplementationCapabilities = ["sample.workspace.create", "sample.read"];
    private static readonly string[] VerificationCapabilities = ["sample.read", "sample.assert", "sample.verify.dotnet"];
    private static readonly string[] AdvisoryVerificationCapabilities = ["sample.assert"];
    private readonly string _runWorkspacesRoot;
    private readonly IApprovedVerificationRunner _verificationRunner;
    private readonly IIsolatedGitWorkspace _gitWorkspace;

    public ConstrainedSampleExecutor(string runWorkspacesRoot, IApprovedVerificationRunner verificationRunner, IIsolatedGitWorkspace gitWorkspace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runWorkspacesRoot);
        ArgumentNullException.ThrowIfNull(verificationRunner);
        ArgumentNullException.ThrowIfNull(gitWorkspace);
        _runWorkspacesRoot = Path.GetFullPath(runWorkspacesRoot);
        _verificationRunner = verificationRunner;
        _gitWorkspace = gitWorkspace;
    }

    public async Task<AgentAuditOutput> ApplyCorrectionAsync(Guid missionId, SampleSnapshot sample, EngineeringMissionTemplate missionTemplate, IReadOnlyList<string> allowedCapabilities, CancellationToken cancellationToken)
    {
        Validate(sample, missionTemplate);
        EnsureAllowed(allowedCapabilities, missionTemplate.Id == "recommend-architecture-improvement" ? AdvisoryImplementationCapabilities : MutationCapabilities);
        var workspace = await CreateWorkspaceAsync(missionId, sample, cancellationToken);
        if (missionTemplate.Id != "recommend-architecture-improvement")
            await _gitWorkspace.InitializeBaselineAsync(workspace, sample.Files.Select(file => file.Path).ToArray(), cancellationToken);
        return missionTemplate.Id switch
        {
            "diagnose-failing-test" => await ApplyReplacementAsync(workspace, missionId, sample, CalculatorFile, DefectiveSubtract, CorrectSubtract, "reviewed-calculator-correction", "Calculator.Subtract now subtracts the right operand.", cancellationToken),
            "identify-security-defect" => await ApplyReplacementAsync(workspace, missionId, sample, FormatterFile, UnsafeGreeting, SafeGreeting, "reviewed-output-encoding-correction", "User-controlled greeting content is HTML encoded before it enters the HTML response.", cancellationToken),
            "find-inefficient-implementation" => await ApplyReplacementAsync(workspace, missionId, sample, ReportFile, InefficientPrefixLoop, LinearPrefixLoop, "reviewed-linear-prefix-sum-correction", "Repeated prefix enumeration is replaced with equivalent linear accumulation.", cancellationToken),
            "recommend-architecture-improvement" => CreateRecommendationAudit(missionId, sample),
            _ => throw new InvalidOperationException($"No reviewed implementation operation exists for template '{missionTemplate.Id}'.")
        };
    }

    public async Task<AgentAuditOutput> VerifyCorrectionAsync(Guid missionId, SampleSnapshot sample, EngineeringMissionTemplate missionTemplate, IReadOnlyList<string> allowedCapabilities, CancellationToken cancellationToken)
    {
        Validate(sample, missionTemplate);
        EnsureAllowed(allowedCapabilities, missionTemplate.Id == "recommend-architecture-improvement" ? AdvisoryVerificationCapabilities : VerificationCapabilities);
        var workspace = GetWorkspacePath(missionId, sample.Id);
        return missionTemplate.Id switch
        {
            "diagnose-failing-test" => await VerifyAsync(workspace, missionId, sample, CalculatorFile, CalculatorProject,
                content => content.Contains(CorrectSubtract, StringComparison.Ordinal) && RequireSampleFile(sample, "CalculatorTests.cs").Contains("Assert.Equal(2, Calculator.Subtract(5, 3))", StringComparison.Ordinal),
                ["Calculator.Subtract uses left - right", "Existing CalculatorTests expectation is 2 for Subtract(5, 3)"], cancellationToken),
            "identify-security-defect" => await VerifyAsync(workspace, missionId, sample, FormatterFile, GreetingProject,
                content => content.Contains("System.Net.WebUtility.HtmlEncode(name)", StringComparison.Ordinal) && !content.Contains("Hello {name}", StringComparison.Ordinal),
                ["Greeting encodes the user-controlled name", "Raw name interpolation is absent from the HTML response"], cancellationToken),
            "find-inefficient-implementation" => await VerifyAsync(workspace, missionId, sample, ReportFile, ReportingProject,
                content => content.Contains("foreach (var value in values)", StringComparison.Ordinal) && !content.Contains("values.Take(index).Sum()", StringComparison.Ordinal),
                ["Prefix accumulation uses one pass", "Repeated Take(...).Sum() enumeration is absent"], cancellationToken),
            "recommend-architecture-improvement" => VerifyRecommendation(missionId, sample),
            _ => throw new InvalidOperationException($"No reviewed verification operation exists for template '{missionTemplate.Id}'.")
        };
    }

    public async Task<IReadOnlyList<SampleFileChange>> GetChangesAsync(Guid missionId, SampleSnapshot sample, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sample);
        var workspace = GetWorkspacePath(missionId, sample.Id);
        if (!Directory.Exists(workspace)) return [];

        var changes = new List<SampleFileChange>();
        foreach (var file in sample.Files)
        {
            var path = GetContainedFilePath(workspace, file.Path);
            if (!File.Exists(path)) continue;
            var updated = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken);
            if (!string.Equals(file.Content, updated, StringComparison.Ordinal))
                changes.Add(new SampleFileChange(file.Path, file.Content, updated));
        }
        return changes;
    }

    public async Task<AgentAuditOutput> CommitReviewedChangeAsync(Guid missionId, SampleSnapshot sample, EngineeringMissionTemplate missionTemplate, IReadOnlyList<string> allowedCapabilities, string summary, CancellationToken cancellationToken)
    {
        Validate(sample, missionTemplate);
        if (missionTemplate.Id == "recommend-architecture-improvement")
            throw new InvalidOperationException("Advisory missions do not authorize an isolated source commit.");
        EnsureAllowed(allowedCapabilities, ["sample.git.commit"]);
        var workspace = GetWorkspacePath(missionId, sample.Id);
        var commit = await _gitWorkspace.CommitChangedFilesAsync(workspace, sample.Files.Select(file => file.Path).ToArray(), summary, cancellationToken);
        var runReference = ToRunReference(missionId, sample.Id);
        return new AgentAuditOutput(
            JsonSerializer.Serialize(new { operation = "isolated-git-commit", commitSha = commit.CommitSha, changedFiles = commit.ChangedFiles, workspace = runReference }),
            "Created a human-triggered Git commit only in the isolated ForgeBoard workspace after the completed review.",
            "isolated-git-workspace", "git-reviewed-v1", "[\"sample.git.commit\"]",
            JsonSerializer.Serialize(new { workspace = runReference, commit = new { sha = commit.CommitSha, diffStat = commit.DiffStat, changedFiles = commit.ChangedFiles }, safety = "No user or host repository was accessed." }), 0, 0m);
    }

    private async Task<string> CreateWorkspaceAsync(Guid missionId, SampleSnapshot sample, CancellationToken cancellationToken)
    {
        var workspace = GetWorkspacePath(missionId, sample.Id);
        Directory.CreateDirectory(workspace);
        foreach (var file in sample.Files)
            await File.WriteAllTextAsync(GetContainedFilePath(workspace, file.Path), file.Content, Encoding.UTF8, cancellationToken);
        return workspace;
    }

    private static async Task<AgentAuditOutput> ApplyReplacementAsync(string workspace, Guid missionId, SampleSnapshot sample, string fileName, string expected, string replacement, string operation, string change, CancellationToken cancellationToken)
    {
        var source = await File.ReadAllTextAsync(GetContainedFilePath(workspace, fileName), Encoding.UTF8, cancellationToken);
        if (!source.Contains(expected, StringComparison.Ordinal))
            throw new InvalidOperationException($"The reviewed correction does not apply to '{fileName}'.");
        var corrected = source.Replace(expected, replacement, StringComparison.Ordinal);
        await File.WriteAllTextAsync(GetContainedFilePath(workspace, fileName), corrected, Encoding.UTF8, cancellationToken);
        var runReference = ToRunReference(missionId, sample.Id);
        return new AgentAuditOutput(
            JsonSerializer.Serialize(new { operation, applied = true, changedFiles = new[] { fileName }, change, workspace = runReference, correctedSha256 = Hash(corrected) }),
            "Applied a reviewed, template-specific correction in an isolated allowlisted sample copy.",
            "deterministic-sample-executor", "reviewed-rule-v2", "[\"sample.workspace.create\",\"sample.write\",\"sample.patch.apply\"]",
            JsonSerializer.Serialize(new { workspace = runReference, patch = new { file = fileName, sha256 = Hash(corrected) } }), 0, 0m);
    }

    private async Task<AgentAuditOutput> VerifyAsync(string workspace, Guid missionId, SampleSnapshot sample, string fileName, string projectFileName, Func<string, bool> assertion, IReadOnlyList<string> checks, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(GetContainedFilePath(workspace, fileName), Encoding.UTF8, cancellationToken);
        if (!assertion(content)) throw new InvalidOperationException("Static verification did not establish the reviewed correction.");
        var verification = await _verificationRunner.RunAsync(workspace, projectFileName, cancellationToken);
        if (verification.ExitCode != 0)
            throw new InvalidOperationException($"Approved verification exited with code {verification.ExitCode}. {verification.StandardError}");
        var runReference = ToRunReference(missionId, sample.Id);
        return new AgentAuditOutput(
            JsonSerializer.Serialize(new { verification = "Static and approved runtime verification passed.", result = "passed", checks, workspace = runReference, verifiedSha256 = Hash(content), command = "dotnet run --project [allowlisted project]" }),
            "Verified reviewed source assertions and the bounded sample harness in the isolated application-owned workspace.",
            "deterministic-sample-executor", "reviewed-rule-v3", "[\"sample.read\",\"sample.assert\",\"sample.verify.dotnet\"]",
            JsonSerializer.Serialize(new { workspace = runReference, assertions = checks.Select(name => new { name, passed = true }), process = new { executable = "dotnet", operation = "run", project = projectFileName, verification.ExitCode, durationMilliseconds = Math.Round(verification.Duration.TotalMilliseconds), standardOutput = verification.StandardOutput, standardError = verification.StandardError } }), 0, 0m);
    }

    private static AgentAuditOutput CreateRecommendationAudit(Guid missionId, SampleSnapshot sample) => new(
        JsonSerializer.Serialize(new { operation = "architecture-recommendation", applied = false, changedFiles = Array.Empty<string>(), recommendation = "Separate HTML rendering from input encoding policy behind a presentation boundary.", workspace = ToRunReference(missionId, sample.Id) }),
        "This template intentionally produces a recommendation only; no source change was authorized.",
        "deterministic-sample-executor", "reviewed-rule-v2", "[\"sample.workspace.create\",\"sample.read\"]",
        JsonSerializer.Serialize(new { workspace = ToRunReference(missionId, sample.Id), evidence = ArchitectureEvidence }), 0, 0m);

    private static AgentAuditOutput VerifyRecommendation(Guid missionId, SampleSnapshot sample) => new(
        JsonSerializer.Serialize(new { verification = "Recommendation review passed.", result = "passed", checks = ArchitectureVerificationChecks, workspace = ToRunReference(missionId, sample.Id) }),
        "Confirmed that the advisory template did not claim an unperformed source modification.",
        "deterministic-sample-executor", "reviewed-rule-v2", "[\"sample.assert\"]",
        JsonSerializer.Serialize(new { workspace = ToRunReference(missionId, sample.Id), assertions = new[] { new { name = "no-source-change-expected", passed = true } } }), 0, 0m);

    private static string RequireSampleFile(SampleSnapshot sample, string fileName) => sample.Files.SingleOrDefault(file => file.Path == fileName)?.Content
        ?? throw new InvalidOperationException($"Required sample file '{fileName}' is missing.");

    private static void Validate(SampleSnapshot sample, EngineeringMissionTemplate missionTemplate)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(missionTemplate);
        var expectedSampleId = missionTemplate.Id switch
        {
            "diagnose-failing-test" or "recommend-architecture-improvement" => CalculatorSampleId,
            "identify-security-defect" => SecuritySampleId,
            "find-inefficient-implementation" => PerformanceSampleId,
            _ => null
        };
        if (expectedSampleId is null || !string.Equals(sample.Id, expectedSampleId, StringComparison.Ordinal) || !string.Equals(missionTemplate.SampleId, expectedSampleId, StringComparison.Ordinal))
            throw new InvalidOperationException("The sample and template combination is not allowlisted for workspace execution.");
    }

    private static void EnsureAllowed(IReadOnlyList<string> allowedCapabilities, IEnumerable<string> requiredCapabilities)
    {
        ArgumentNullException.ThrowIfNull(allowedCapabilities);
        var denied = requiredCapabilities.FirstOrDefault(required => !allowedCapabilities.Contains(required, StringComparer.Ordinal));
        if (denied is not null)
            throw new InvalidOperationException($"The immutable mission operation policy does not allow '{denied}'.");
    }

    private string GetWorkspacePath(Guid missionId, string sampleId)
    {
        if (missionId == Guid.Empty) throw new ArgumentException("A mission identifier is required.", nameof(missionId));
        var workspace = Path.GetFullPath(Path.Combine(_runWorkspacesRoot, missionId.ToString("N"), sampleId));
        if (!workspace.StartsWith(_runWorkspacesRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("The isolated workspace path is outside the application-owned run directory.");
        return workspace;
    }

    private static string GetContainedFilePath(string workspace, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.GetFileName(relativePath) != relativePath)
            throw new InvalidOperationException("The sample file path is not allowlisted.");
        var path = Path.GetFullPath(Path.Combine(workspace, relativePath));
        if (!path.StartsWith(workspace + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("The sample file path escapes the isolated workspace.");
        return path;
    }

    private static string ToRunReference(Guid missionId, string sampleId) => $"runs/{missionId:N}/{sampleId}";
    private static string Hash(string content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
}
