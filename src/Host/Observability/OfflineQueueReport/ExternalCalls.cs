using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using ALKAROS.Observability.ErrorEvents;
using Microsoft.Extensions.DependencyInjection;

namespace ALKAROS.Host.Observability.OfflineQueueReport;

public sealed class ExternalCallOptions
{
    public int SlowWarnMilliseconds { get; set; } = 2000;
    public int SlowErrorMilliseconds { get; set; } = 10_000;
}

/// <summary>
/// Reduces an outgoing address to host and a path template before it reaches an event: only plain words and
/// version markers survive, so ids, tokens and keys in the path (a bot token is part of the path) are replaced,
/// and the query string, fragment, user info and port are dropped.
/// </summary>
public static partial class ExternalAddress
{
    private const int MaxLength = 120;

    public static string Sanitize(string? address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return "unknown";

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => PlainWord().IsMatch(segment) ? segment : ":id");
        var sanitized = $"{uri.Host.ToLowerInvariant()}/{string.Join('/', segments)}".TrimEnd('/');
        return sanitized.Length > MaxLength ? sanitized[..MaxLength] : sanitized;
    }

    [GeneratedRegex(@"^([A-Za-z]{1,24}|v\d{1,2})$")]
    private static partial Regex PlainWord();
}

/// <summary>
/// Reports outgoing calls that fail or take too long, naming them by sanitized address. The runtime's HTTP client
/// publishes the request and response objects through its diagnostic listener (its activity carries no address),
/// so the tap reads them from there and times each call itself.
/// </summary>
public sealed class ExternalCallTap : IDisposable, IObserver<DiagnosticListener>
{
    private const string ListenerName = "HttpHandlerDiagnosticListener";
    private const string ActivityName = "System.Net.Http.HttpRequestOut";

    private readonly ErrorEventWriter _writer;
    private readonly ExternalCallOptions _options;
    private readonly ConditionalWeakTable<HttpRequestMessage, StartedCall> _started = new();
    private readonly IDisposable _discovery;
    private IDisposable? _subscription;

    public ExternalCallTap(ErrorEventWriter writer, ExternalCallOptions? options = null)
    {
        _writer = writer;
        _options = options ?? new ExternalCallOptions();
        _discovery = DiagnosticListener.AllListeners.Subscribe(this);
    }

    public long Failures { get; private set; }

    void IObserver<DiagnosticListener>.OnNext(DiagnosticListener listener)
    {
        if (listener.Name == ListenerName)
            _subscription = listener.Subscribe(new CallEvents(this), static (name, _, _) => name.StartsWith(ActivityName, StringComparison.Ordinal) || name == "System.Net.Http.Exception");
    }

    void IObserver<DiagnosticListener>.OnCompleted()
    {
    }

    void IObserver<DiagnosticListener>.OnError(Exception error) => Failures++;

    private void OnEvent(string name, object? payload)
    {
        try
        {
            if (payload is null || Read(payload, "Request") is not HttpRequestMessage request)
                return;

            if (name == ActivityName + ".Start")
                _started.AddOrUpdate(request, new StartedCall(Stopwatch.GetTimestamp()));
            else if (name == ActivityName + ".Stop" && _started.TryGetValue(request, out var started))
            {
                _started.Remove(request);
                Observe(request, Read(payload, "Response") as HttpResponseMessage, Stopwatch.GetElapsedTime(started.Timestamp));
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Failures++;
        }
    }

    private static object? Read(object payload, string property) => payload.GetType().GetProperty(property)?.GetValue(payload);

    private void Observe(HttpRequestMessage request, HttpResponseMessage? response, TimeSpan elapsed)
    {
        var status = response is null ? 0 : (int)response.StatusCode;
        var failed = response is null || status >= 400;
        var slow = elapsed.TotalMilliseconds >= _options.SlowWarnMilliseconds;
        if (!failed && !slow)
            return;

        var detail = $"{request.Method.Method} {ExternalAddress.Sanitize(request.RequestUri?.AbsoluteUri)}";
        var trace = Activity.Current?.TraceId.ToString();
        if (failed)
        {
            var severity = status is >= 400 and < 500 ? ErrorSeverity.Warning : ErrorSeverity.Error;
            _writer.TryEnqueue(new ErrorEventInput(ErrorLayer.Integration, severity, "integration.external_call_failed", detail, trace));
            return;
        }

        var slowSeverity = elapsed.TotalMilliseconds >= _options.SlowErrorMilliseconds ? ErrorSeverity.Error : ErrorSeverity.Warning;
        _writer.TryEnqueue(new ErrorEventInput(ErrorLayer.Integration, slowSeverity, "integration.external_call_slow", detail, trace));
    }

    public void Dispose()
    {
        _discovery.Dispose();
        _subscription?.Dispose();
    }

    private sealed record StartedCall(long Timestamp);

    private sealed class CallEvents(ExternalCallTap tap) : IObserver<KeyValuePair<string, object?>>
    {
        public void OnNext(KeyValuePair<string, object?> value) => tap.OnEvent(value.Key, value.Value);

        public void OnCompleted()
        {
        }

        public void OnError(Exception error) => tap.Failures++;
    }
}

public static class ExternalCallExtensions
{
    public static IServiceCollection AddExternalCallDetection(this IServiceCollection services, ExternalCallOptions? options = null)
    {
        services.AddSingleton(sp => new ExternalCallTap(sp.GetRequiredService<ErrorEventWriter>(), options));
        services.AddHostedService(sp => new TapStarter(sp.GetRequiredService<ExternalCallTap>()));
        return services;
    }

    private sealed class TapStarter(ExternalCallTap tap) : Microsoft.Extensions.Hosting.IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken)
        {
            tap.Dispose();
            return Task.CompletedTask;
        }
    }
}
