using System.Diagnostics;

var container = Environment.GetEnvironmentVariable("ALKAROS_TEST_PG_CONTAINER")
    ?? "alkaros-rmd026-pg-20260828";
var forwarded = new List<string>(args.Length);
string? scriptPath = null;
for (var index = 0; index < args.Length; index++)
{
    if (string.Equals(args[index], "--file", StringComparison.Ordinal) && index + 1 < args.Length)
    {
        scriptPath = args[++index];
        forwarded.Add("--file=-");
        continue;
    }

    forwarded.Add(args[index]);
}

for (var index = 0; index + 1 < forwarded.Count; index++)
{
    if (string.Equals(forwarded[index], "-p", StringComparison.Ordinal)
        && string.Equals(forwarded[index + 1], "55436", StringComparison.Ordinal))
        forwarded[index + 1] = "5432";
}

var startInfo = new ProcessStartInfo("docker")
{
    RedirectStandardInput = true,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false,
};
startInfo.ArgumentList.Add("exec");
startInfo.ArgumentList.Add("-i");
startInfo.ArgumentList.Add("-e");
startInfo.ArgumentList.Add("PGPASSWORD");
startInfo.ArgumentList.Add(container);
startInfo.ArgumentList.Add("psql");
foreach (var argument in forwarded)
    startInfo.ArgumentList.Add(argument);

using var process = Process.Start(startInfo)
    ?? throw new InvalidOperationException("Docker psql proxy could not start.");
var input = ForwardInputAsync(scriptPath, process.StandardInput.BaseStream);
var output = process.StandardOutput.BaseStream.CopyToAsync(Console.OpenStandardOutput());
var error = process.StandardError.BaseStream.CopyToAsync(Console.OpenStandardError());
await Task.WhenAll(input, output, error, process.WaitForExitAsync());
return process.ExitCode;

static async Task ForwardInputAsync(string? scriptPath, Stream destination)
{
    if (scriptPath is not null)
    {
        await using var script = File.OpenRead(scriptPath);
        await script.CopyToAsync(destination);
    }
    await Console.OpenStandardInput().CopyToAsync(destination);
    await destination.DisposeAsync();
}
