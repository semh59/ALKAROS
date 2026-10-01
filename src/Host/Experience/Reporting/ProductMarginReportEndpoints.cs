using ALKAROS.Host.Composition.Errors;
using ALKAROS.Host.Experience.Reconciliation;
using ALKAROS.Reporting.ProductMargin;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ALKAROS.Host.Experience.Reporting;

/// <summary>
/// The versioned product margin report. Same gate as the channel and payment settlement reports: the manager session plus
/// reports.view; an invalid range is refused with the shared Turkish validation error.
/// </summary>
public static class ProductMarginReportEndpoints
{
    public static RouteGroupBuilder MapProductMarginReportApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ApiErrorHandling.EnsureFor(endpoints);
        var group = endpoints.MapGroup("/api/v1/management/reports/product-margin");
        group.AddEndpointFilter<ReconciliationCaseEndpointFilter>();

        group.MapGet("/", async (
            DateOnly from,
            DateOnly to,
            IProductMarginReportService reports,
            CancellationToken cancellationToken) =>
            Results.Ok(await reports.GetReportAsync(new ProductMarginFilter(from, to), cancellationToken)));

        return group;
    }
}
