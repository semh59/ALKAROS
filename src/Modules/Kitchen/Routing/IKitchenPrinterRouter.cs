namespace ALKAROS.Kitchen.Routing;

/// <summary>
/// Deterministic kitchen printer routing engine (V0-DOM-011 / V1-KIT-002).
/// Resolves a kitchen item to an active printer according to the precedence hierarchy:
/// Item (1) > Product (2) > DailySpecial (3) > Category (4) > Default (5).
/// </summary>
public interface IKitchenPrinterRouter
{
    /// <summary>
    /// Evaluates candidate routes against available printers and returns a deterministic resolution result.
    /// </summary>
    RoutingResult ResolveRoute(
        RoutingEvaluationRequest request,
        IReadOnlyList<PrinterRoute> routes,
        IReadOnlyList<Printer> printers);
}
