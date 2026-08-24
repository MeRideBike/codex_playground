namespace ForgeBoard.Application;

public sealed class BundledEngineeringMissionCatalog : IEngineeringMissionCatalog
{
    private static readonly string[] MutationCapabilities = ["sample.read", "sample.workspace.create", "sample.write", "sample.patch.apply", "sample.assert", "sample.verify.dotnet", "sample.git.commit"];
    private static readonly string[] AdvisoryCapabilities = ["sample.read", "sample.workspace.create", "sample.assert"];
    private static readonly EngineeringMissionTemplate[] Templates =
    [
        new("diagnose-failing-test", "Diagnose failing test", "Find and correct the Calculator.Subtract regression, then verify the existing test expectation.", "defective-calculator", MutationCapabilities),
        new("identify-security-defect", "Identify security defect", "Identify and correct unencoded user-controlled HTML output in the dedicated greeting sample.", "insecure-greeting", MutationCapabilities),
        new("find-inefficient-implementation", "Find inefficient implementation", "Replace repeated prefix enumeration with an equivalent linear accumulation in the dedicated reporting sample.", "inefficient-reporting", MutationCapabilities),
        new("recommend-architecture-improvement", "Recommend architecture improvement", "Assess the sample for a small architecture improvement without applying a source change.", "defective-calculator", AdvisoryCapabilities)
    ];

    public IReadOnlyList<EngineeringMissionTemplate> List() => Templates;

    public EngineeringMissionTemplate? Find(string templateId) =>
        string.IsNullOrWhiteSpace(templateId) ? null : Templates.SingleOrDefault(template => string.Equals(template.Id, templateId, StringComparison.Ordinal));

    public EngineeringMissionTemplate? GetDefaultForSample(string sampleId) =>
        string.Equals(sampleId, "defective-calculator", StringComparison.Ordinal) ? Templates[0] :
        string.Equals(sampleId, "insecure-greeting", StringComparison.Ordinal) ? Templates[1] :
        string.Equals(sampleId, "inefficient-reporting", StringComparison.Ordinal) ? Templates[2] : null;
}
