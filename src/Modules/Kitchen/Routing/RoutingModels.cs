namespace ALKAROS.Kitchen.Routing;

/// <summary>
/// Input parameters for routing evaluation (V0-DOM-011 Section 6).
/// </summary>
public sealed class RoutingEvaluationRequest
{
    public RoutingEvaluationRequest(
        Guid productId,
        Guid categoryId,
        Guid? itemId = null,
        DateOnly? date = null)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId cannot be empty.", nameof(productId));
        if (categoryId == Guid.Empty)
            throw new ArgumentException("CategoryId cannot be empty.", nameof(categoryId));

        ProductId = productId;
        CategoryId = categoryId;
        ItemId = itemId;
        Date = date;
    }

    public Guid? ItemId { get; }
    public Guid ProductId { get; }
    public Guid CategoryId { get; }
    public DateOnly? Date { get; }
}

/// <summary>
/// Output of routing resolution (V0-DOM-011 Section 6).
/// </summary>
public sealed class RoutingResult
{
    public const string ErrorNoAvailablePrinter = "NO_AVAILABLE_PRINTER";
    public const string ErrorAmbiguousRoute = "AMBIGUOUS_ROUTE";
    public const string ErrorConfigurationError = "CONFIGURATION_ERROR";

    private RoutingResult(
        bool resolved,
        Guid? printerId,
        RouteLevel? routeLevel,
        string? errorCode,
        string? errorDetails)
    {
        Resolved = resolved;
        PrinterId = printerId;
        RouteLevel = routeLevel;
        ErrorCode = errorCode;
        ErrorDetails = errorDetails;
    }

    public bool Resolved { get; }
    public Guid? PrinterId { get; }
    public RouteLevel? RouteLevel { get; }
    public string? ErrorCode { get; }
    public string? ErrorDetails { get; }

    public static RoutingResult Success(Guid printerId, RouteLevel routeLevel)
    {
        if (printerId == Guid.Empty)
            throw new ArgumentException("PrinterId cannot be empty.", nameof(printerId));

        return new RoutingResult(
            resolved: true,
            printerId: printerId,
            routeLevel: routeLevel,
            errorCode: null,
            errorDetails: null);
    }

    public static RoutingResult NoAvailablePrinter(string details)
    {
        return new RoutingResult(
            resolved: false,
            printerId: null,
            routeLevel: null,
            errorCode: ErrorNoAvailablePrinter,
            errorDetails: details);
    }

    public static RoutingResult AmbiguousRoute(string details)
    {
        return new RoutingResult(
            resolved: false,
            printerId: null,
            routeLevel: null,
            errorCode: ErrorAmbiguousRoute,
            errorDetails: details);
    }

    public static RoutingResult ConfigurationError(string details)
    {
        return new RoutingResult(
            resolved: false,
            printerId: null,
            routeLevel: null,
            errorCode: ErrorConfigurationError,
            errorDetails: details);
    }
}
