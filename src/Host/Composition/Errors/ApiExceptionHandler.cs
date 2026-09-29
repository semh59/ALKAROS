using ALKAROS.Host.DualScreen;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ALKAROS.Host.Composition.Errors;

/// <summary>
/// V1-RMD-431: the one place a Host API exception becomes the error envelope. The areas the request entered are
/// asked innermost first; an area answers only for the exception types its filter used to catch, and an area with no
/// answer passes the exception out, the way its old rethrow did. What no area answers gets the Host default
/// (<see cref="ApiErrorCatalog.Default"/>, the former DualScreen mapping).
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private static readonly Action<ILogger, string, string, Exception?> LogServerError =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(5000, nameof(LogServerError)),
            "Unhandled error on {Path} (TraceIdentifier: {TraceIdentifier})");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        await WriteAsync(httpContext, exception);
        return true;
    }

    public static (int Status, string Code, string Message) Resolve(
        IReadOnlyList<ApiErrorArea> enteredAreas, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(enteredAreas);
        ArgumentNullException.ThrowIfNull(exception);
        for (var index = enteredAreas.Count - 1; index >= 0; index--)
        {
            var area = enteredAreas[index];
            if (area.Catches(exception) && area.Map(exception) is { } mapped)
                return mapped;
        }

        return ApiErrorCatalog.Default(exception);
    }

    public static async Task WriteAsync(HttpContext context, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(context);
        var (status, code, message) = Resolve(ApiErrorScope.Entered(context), exception);
        if (status >= 500)
            LogFailure(context, exception);

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(
            new ApiErrorEnvelope(new ApiError(code, message, status, context.TraceIdentifier)),
            cancellationToken: context.RequestAborted);
    }

    internal static void LogFailure(HttpContext context, Exception exception)
    {
        var logger = context.RequestServices.GetService<ILogger<ApiExceptionHandler>>();
        if (logger is not null)
            LogServerError(logger, context.Request.Path, context.TraceIdentifier, exception);
    }
}
