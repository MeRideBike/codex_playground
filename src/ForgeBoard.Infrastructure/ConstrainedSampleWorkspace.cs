using System.Security.Cryptography;
using System.Text;
using ForgeBoard.Application;

namespace ForgeBoard.Infrastructure;

public sealed class ConstrainedSampleWorkspace : ISampleWorkspace
{
    private static readonly SampleDefinition DefectiveCalculator = new(
        "defective-calculator",
        "Defective Calculator",
        "A deliberately imperfect calculator with a failing subtraction test.");
    private static readonly SampleDefinition InsecureGreeting = new(
        "insecure-greeting",
        "Insecure Greeting Renderer",
        "A small HTML greeting helper that interpolates untrusted input without output encoding.");
    private static readonly SampleDefinition InefficientReporting = new(
        "inefficient-reporting",
        "Inefficient Prefix Report",
        "A report helper that repeatedly enumerates prefixes instead of accumulating them in one pass.");
    private static readonly SampleDefinition[] Samples = [DefectiveCalculator, InsecureGreeting, InefficientReporting];
    private static readonly Dictionary<string, string[]> AllowedFiles = new(StringComparer.Ordinal)
    {
        [DefectiveCalculator.Id] = ["Calculator.cs", "CalculatorTests.cs", "UserInputFormatter.cs", "ReportGenerator.cs"],
        [InsecureGreeting.Id] = ["GreetingRenderer.cs", "GreetingRendererTests.cs"],
        [InefficientReporting.Id] = ["PrefixReport.cs", "PrefixReportTests.cs"]
    };
    private readonly string _samplesRoot;

    public ConstrainedSampleWorkspace(string samplesRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(samplesRoot);
        _samplesRoot = Path.GetFullPath(samplesRoot);
    }

    public IReadOnlyList<SampleDefinition> List() => Samples;

    public async Task<SampleSnapshot?> GetSnapshotAsync(string sampleId, CancellationToken cancellationToken)
    {
        if (!AllowedFiles.TryGetValue(sampleId, out var allowedFiles)) return null;
        var definition = Samples.Single(sample => string.Equals(sample.Id, sampleId, StringComparison.Ordinal));
        var sampleRoot = Path.GetFullPath(Path.Combine(_samplesRoot, sampleId));
        if (!sampleRoot.StartsWith(_samplesRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("The sample path is outside the allowlisted sample workspace.");

        var files = new List<SampleFileEvidence>(allowedFiles.Length);
        foreach (var allowedFile in allowedFiles)
        {
            var path = Path.Combine(sampleRoot, allowedFile);
            if (!File.Exists(path)) throw new InvalidOperationException($"Required sample file '{allowedFile}' is missing.");
            var content = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
            files.Add(new SampleFileEvidence(allowedFile, content, hash));
        }
        return new SampleSnapshot(sampleId, definition.Description, files);
    }
}
