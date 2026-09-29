using Microsoft.AspNetCore.Http;

namespace ALKAROS.Host.Composition.Errors;

/// <summary>
/// V1-RMD-431: one Host area's slice of the error table. <see cref="CaughtTypes"/> are the exception types the area's
/// endpoint filter answers for (what its own catch clauses used to list); <see cref="Map"/> turns one of them into the
/// status, code and Turkish message the area's clients already receive, or null when the area has no answer and the
/// next area out (or the Host default) decides, exactly as the old rethrow did.
/// </summary>
public sealed class ApiErrorArea
{
    public ApiErrorArea(IReadOnlyList<Type> caughtTypes, Func<Exception, (int Status, string Code, string Message)?> map)
    {
        CaughtTypes = caughtTypes ?? throw new ArgumentNullException(nameof(caughtTypes));
        Map = map ?? throw new ArgumentNullException(nameof(map));
    }

    public IReadOnlyList<Type> CaughtTypes { get; }

    public Func<Exception, (int Status, string Code, string Message)?> Map { get; }

    public bool Catches(Exception exception) => CaughtTypes.Any(type => type.IsInstanceOfType(exception));
}

/// <summary>
/// The areas a request has entered, outermost first. An area's endpoint filter enters its area before it does
/// anything else, so an exception thrown anywhere inside it is answered by that area, the way its try/catch used to.
/// </summary>
public static class ApiErrorScope
{
    private static readonly object ItemKey = new();

    public static void Enter(HttpContext context, ApiErrorArea area)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(area);
        if (context.Items[ItemKey] is not List<ApiErrorArea> areas)
        {
            areas = [];
            context.Items[ItemKey] = areas;
        }

        areas.Add(area);
    }

    public static IReadOnlyList<ApiErrorArea> Entered(HttpContext context)
        => context.Items[ItemKey] as List<ApiErrorArea> ?? [];
}
