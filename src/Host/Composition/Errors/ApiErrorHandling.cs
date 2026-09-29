using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ALKAROS.Host.Composition.Errors;

/// <summary>V1-RMD-431: wires <see cref="ApiExceptionHandler"/> through ASP.NET Core's exception handler middleware.</summary>
public static class ApiErrorHandling
{
    private const string RegisteredKey = "alkaros.api-error-handling";

    // The middleware logs every exception it handles at Error level; a refused request (401/404/409) is not a server
    // failure, so its own log is silenced and ApiExceptionHandler logs the 5xx ones.
    private const string MiddlewareLogCategory = "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware";

    public static IServiceCollection AddApiErrorHandling(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddLogging(logging => logging.AddFilter(MiddlewareLogCategory, LogLevel.None));
        return services;
    }

    /// <summary>
    /// Adds the error pipeline once per application. A response that already started cannot carry an envelope: the
    /// middleware rethrows it, and the outer guard logs it with its trace id and aborts the connection.
    /// </summary>
    public static IApplicationBuilder UseApiErrorHandling(this IApplicationBuilder application)
    {
        ArgumentNullException.ThrowIfNull(application);
        if (application.Properties.ContainsKey(RegisteredKey))
            return application;
        application.Properties[RegisteredKey] = true;

        application.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (Exception exception) when (context.Response.HasStarted)
            {
                ApiExceptionHandler.LogFailure(context, exception);
                context.Abort();
            }
        });
        return application.UseExceptionHandler(new ExceptionHandlerOptions
        {
            // An area's 404 (NOT_FOUND and similar) is a handled answer, not "no handler found".
            AllowStatusCode404Response = true,
            // Used when the host did not register ApiExceptionHandler as an IExceptionHandler (a test host that only
            // maps one area); the answer is the same.
            ExceptionHandler = context => ApiExceptionHandler.WriteAsync(
                context, context.Features.Get<IExceptionHandlerFeature>()!.Error),
        });
    }

    /// <summary>
    /// An area's Map method calls this so the area answers errors even on a host that maps only that area; on the
    /// full Host the pipeline is already in place and this does nothing.
    /// </summary>
    public static void EnsureFor(IEndpointRouteBuilder endpoints)
    {
        if (endpoints is IApplicationBuilder application)
            application.UseApiErrorHandling();
    }
}
