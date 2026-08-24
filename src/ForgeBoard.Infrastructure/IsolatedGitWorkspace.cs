using System.Diagnostics;
using ForgeBoard.Application;

namespace ForgeBoard.Infrastructure;

/// <summary>
/// Creates a disposable Git history only within a ForgeBoard-owned run workspace.
/// Every Git invocation is fixed and file arguments are constrained to the original sample manifest.
/// </summary>
public sealed class IsolatedGitWorkspace : IIsolatedGitWorkspace
{
    private static readonly TimeSpan MaximumDuration = TimeSpan.FromSeconds(15);

    public async Task InitializeBaselineAsync(string workspacePath, IReadOnlyList<string> allowlistedFiles, CancellationToken cancellationToken)
    {
        var workspace = ValidateWorkspace(workspacePath);
        var files = ValidateFiles(allowlistedFiles);
        if (Directory.Exists(Path.Combine(workspace, ".git"))) return;

        await RunGitAsync(workspace, ["init", "--quiet"], cancellationToken);
        await RunGitAsync(workspace, ["add", "--", .. files], cancellationToken);
        await RunGitAsync(workspace, ["-c", "user.name=ForgeBoard", "-c", "user.email=forgeboard@local", "commit", "--quiet", "-m", "ForgeBoard sample baseline"], cancellationToken);
    }

    public async Task<IsolatedGitCommitResult> CommitChangedFilesAsync(string workspacePath, IReadOnlyList<string> allowlistedFiles, string summary, CancellationToken cancellationToken)
    {
        var workspace = ValidateWorkspace(workspacePath);
        var files = ValidateFiles(allowlistedFiles);
        if (!Directory.Exists(Path.Combine(workspace, ".git")))
            throw new InvalidOperationException("The isolated Git baseline has not been initialized.");
        if (string.IsNullOrWhiteSpace(summary) || summary.Length > 300)
            throw new ArgumentException("An isolated commit summary of 1 to 300 characters is required.", nameof(summary));

        var changed = (await RunGitAsync(workspace, ["diff", "--name-only", "--", .. files], cancellationToken)).StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (changed.Length == 0) throw new InvalidOperationException("There is no reviewed source change to commit.");
        if (changed.Any(file => !files.Contains(file, StringComparer.Ordinal)))
            throw new InvalidOperationException("The isolated workspace contains a change outside the sample manifest.");

        await RunGitAsync(workspace, ["add", "--", .. changed], cancellationToken);
        await RunGitAsync(workspace, ["-c", "user.name=ForgeBoard", "-c", "user.email=forgeboard@local", "commit", "--quiet", "-m", summary.Trim()], cancellationToken);
        var sha = (await RunGitAsync(workspace, ["rev-parse", "HEAD"], cancellationToken)).StandardOutput.Trim();
        var diffStat = (await RunGitAsync(workspace, ["show", "--stat", "--oneline", "--format=%h %s", "HEAD"], cancellationToken)).StandardOutput.Trim();
        return new IsolatedGitCommitResult(sha, diffStat, changed);
    }

    private static string ValidateWorkspace(string workspacePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        var workspace = Path.GetFullPath(workspacePath);
        if (!Directory.Exists(workspace)) throw new InvalidOperationException("The isolated workspace is missing.");
        return workspace;
    }

    private static string[] ValidateFiles(IReadOnlyList<string> allowlistedFiles)
    {
        ArgumentNullException.ThrowIfNull(allowlistedFiles);
        if (allowlistedFiles.Count == 0) throw new InvalidOperationException("The sample manifest is empty.");
        return allowlistedFiles.Select(file =>
        {
            if (string.IsNullOrWhiteSpace(file) || Path.GetFileName(file) != file)
                throw new InvalidOperationException("The sample manifest contains an unsafe file path.");
            return file;
        }).Distinct(StringComparer.Ordinal).ToArray();
    }

    private static async Task<GitResult> RunGitAsync(string workspace, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workspace,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start()) throw new InvalidOperationException("The isolated Git command could not be started.");
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
            if (process.ExitCode != 0) throw new InvalidOperationException($"The isolated Git operation failed: {Trim(error)}");
            return new GitResult(Trim(output), Trim(error));
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"The isolated Git operation exceeded its {MaximumDuration.TotalSeconds:0}-second limit.");
        }
    }

    private static void TryTerminate(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
    }

    private static string Trim(string value) => value.Length <= 16_000 ? value : value[..16_000] + Environment.NewLine + "[Output truncated by ForgeBoard audit limit.]";
    private sealed record GitResult(string StandardOutput, string StandardError);
}
