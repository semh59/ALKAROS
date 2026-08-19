using System.Diagnostics;

namespace ALKAROS.Host.Composition.Migrations;

/// <summary>
/// Connection and process settings for psql-based migration execution. The
/// password, when provided, is passed to the child process through the
/// PGPASSWORD environment variable and never as a command-line argument.
/// </summary>
public sealed record PsqlOptions(
    string DatabaseUrl,
    string PsqlExecutable = "psql",
    string? Password = null,
    TimeSpan Timeout = default)
{
    public TimeSpan EffectiveTimeout => Timeout == default ? TimeSpan.FromMinutes(5) : Timeout;
}

public sealed record ScriptExecutionResult(bool Success, string StandardOutput, string StandardError)
{
    public string ErrorSummary => StandardError
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
        .Select(static line => line.Trim())
        .LastOrDefault()
        ?? string.Empty;
}

/// <summary>
/// Runs a single SQL script against PostgreSQL 18 through the psql command
/// line client with ON_ERROR_STOP so a failing script never silently
/// continues, and -w so a missing password fails fast instead of waiting on
/// an interactive prompt.
/// </summary>
public static class PsqlScriptRunner
{
    /// <summary>
    /// Runs a single SQL command (not a file) through psql in tuples-only,
    /// unaligned mode, suitable for control queries.
    /// </summary>
    public static async Task<ScriptExecutionResult> RunCommandAsync(
        string command,
        PsqlOptions options,
        CancellationToken cancellationToken)
    {
        var startInfo = CreateStartInfo(options);
        startInfo.ArgumentList.Add("-tAc");
        startInfo.ArgumentList.Add(command);

        return await RunProcessAsync(startInfo, options, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<ScriptExecutionResult> RunAsync(
        string scriptPath,
        PsqlOptions options,
        CancellationToken cancellationToken)
    {
        var startInfo = CreateStartInfo(options);
        startInfo.ArgumentList.Add("--single-transaction");
        startInfo.ArgumentList.Add("--file");
        startInfo.ArgumentList.Add(scriptPath);

        return await RunProcessAsync(startInfo, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs a migration script and its control-table command in one PostgreSQL
    /// transaction. Either both effects commit or neither effect commits.
    /// </summary>
    public static async Task<ScriptExecutionResult> RunScriptWithCommandAsync(
        string scriptPath,
        string command,
        PsqlOptions options,
        CancellationToken cancellationToken)
    {
        var startInfo = CreateStartInfo(options);
        startInfo.ArgumentList.Add("--single-transaction");
        startInfo.ArgumentList.Add("--file");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("--command");
        startInfo.ArgumentList.Add(command);

        return await RunProcessAsync(startInfo, options, cancellationToken).ConfigureAwait(false);
    }

    private static ProcessStartInfo CreateStartInfo(PsqlOptions options)
    {
        var startInfo = new ProcessStartInfo(options.PsqlExecutable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-X");
        startInfo.ArgumentList.Add("-q");
        startInfo.ArgumentList.Add("-w");
        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("ON_ERROR_STOP=1");

        ApplyConnectionSettings(startInfo, options);

        return startInfo;
    }

    private static void ApplyConnectionSettings(ProcessStartInfo startInfo, PsqlOptions options)
    {
        if (options.Password is not null)
            startInfo.Environment["PGPASSWORD"] = options.Password;

        var dbUrl = options.DatabaseUrl;
        if (string.IsNullOrWhiteSpace(dbUrl))
            return;

        if (Uri.TryCreate(dbUrl, UriKind.Absolute, out var uri)
            && (string.Equals(uri.Scheme, "postgres", StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, "postgresql", StringComparison.OrdinalIgnoreCase)))
        {
            if (!string.IsNullOrEmpty(uri.Host))
            {
                startInfo.Environment["PGHOST"] = uri.Host;
                startInfo.ArgumentList.Add("-h");
                startInfo.ArgumentList.Add(uri.Host);
            }

            if (uri.Port > 0)
            {
                var portStr = uri.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
                startInfo.Environment["PGPORT"] = portStr;
                startInfo.ArgumentList.Add("-p");
                startInfo.ArgumentList.Add(portStr);
            }

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var userParts = uri.UserInfo.Split(':', 2);
                if (!string.IsNullOrEmpty(userParts[0]))
                {
                    var user = Uri.UnescapeDataString(userParts[0]);
                    startInfo.Environment["PGUSER"] = user;
                    startInfo.ArgumentList.Add("-U");
                    startInfo.ArgumentList.Add(user);
                }

                if (userParts.Length > 1 && !string.IsNullOrEmpty(userParts[1]))
                {
                    startInfo.Environment["PGPASSWORD"] = Uri.UnescapeDataString(userParts[1]);
                }
            }

            var dbName = uri.AbsolutePath.TrimStart('/');
            if (!string.IsNullOrEmpty(dbName))
            {
                var unescapedDb = Uri.UnescapeDataString(dbName);
                startInfo.Environment["PGDATABASE"] = unescapedDb;
                startInfo.ArgumentList.Add("-d");
                startInfo.ArgumentList.Add(unescapedDb);
            }
        }
        else
        {
            startInfo.ArgumentList.Add("-d");
            startInfo.ArgumentList.Add(dbUrl);
        }
    }

    private static async Task<ScriptExecutionResult> RunProcessAsync(
        ProcessStartInfo startInfo,
        PsqlOptions options,
        CancellationToken cancellationToken)
    {
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start '{options.PsqlExecutable}'.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.EffectiveTimeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            KillProcessTree(process);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            return new ScriptExecutionResult(
                false, string.Empty, $"psql timed out after {options.EffectiveTimeout}.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            KillProcessTree(process);
            throw;
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        return new ScriptExecutionResult(
            process.ExitCode == 0,
            Redact(stdout),
            Redact(stderr));
    }

    public static string Redact(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        var redacted = System.Text.RegularExpressions.Regex.Replace(
            input,
            @"(password\s*=\s*)([^;\s]+)",
            "$1***",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        redacted = System.Text.RegularExpressions.Regex.Replace(
            redacted,
            @"(postgres(?:ql)?://[^:]+:)([^@]+)(@)",
            "$1***$3",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return redacted;
    }

    /// <summary>
    /// Kills the psql process tree. If the process has already exited in the
    /// instant the timeout fired, the kill is a no-op; any other kill failure
    /// is rethrown so a hung psql process is never silently left behind.
    /// </summary>
    private static void KillProcessTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
            // The process exited before the kill could run; nothing to kill.
        }
    }
}
