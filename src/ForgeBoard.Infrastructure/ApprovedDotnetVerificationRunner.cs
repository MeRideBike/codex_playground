using System.Diagnostics;
using System.Text;
using ForgeBoard.Application;

namespace ForgeBoard.Infrastructure;

/// <summary>
/// Runs the sole supported verification command against a bundled sample project.
/// The executable and arguments are intentionally fixed; project names are validated before use.
/// </summary>
public sealed class ApprovedDotnetVerificationRunner : IApprovedVerificationRunner
{
    private static readonly TimeSpan MaximumDuration = TimeSpan.FromSeconds(30);
    private const int MaximumCapturedCharacters = 16_000;

    public async Task<ApprovedVerificationResult> RunAsync(string workspacePath, string projectFileName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        if (string.IsNullOrWhiteSpace(projectFileName) || Path.GetFileName(projectFileName) != projectFileName || !projectFileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The verification project is not allowlisted.", nameof(projectFileName));

        var fullWorkspace = Path.GetFullPath(workspacePath);
        var projectPath = Path.GetFullPath(Path.Combine(fullWorkspace, projectFileName));
        if (!projectPath.StartsWith(fullWorkspace + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(projectPath))
            throw new InvalidOperationException("The verification project is outside the application-owned workspace or is missing.");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = fullWorkspace,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--verbosity");
        startInfo.ArgumentList.Add("quiet");

        using var process = new Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();
        if (!process.Start()) throw new InvalidOperationException("The approved verification process could not be started.");
        using var timeout = new CancellationTokenSource(MaximumDuration);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var registration = linkedCancellation.Token.Register(() => TryTerminate(process));
        var outputTask = process.StandardOutput.ReadToEndAsync(linkedCancellation.Token);
        var errorTask = process.StandardError.ReadToEndAsync(linkedCancellation.Token);

        try
        {
            await process.WaitForExitAsync(linkedCancellation.Token);
            var output = await outputTask;
            var error = await errorTask;
            stopwatch.Stop();
            return new ApprovedVerificationResult(process.ExitCode, stopwatch.Elapsed, Trim(output), Trim(error));
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"The approved verification exceeded its {MaximumDuration.TotalSeconds:0}-second limit.");
        }
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process has already exited between the check and termination request.
        }
    }

    private static string Trim(string value) => value.Length <= MaximumCapturedCharacters
        ? value
        : value[..MaximumCapturedCharacters] + Environment.NewLine + "[Output truncated by ForgeBoard audit limit.]";
}
