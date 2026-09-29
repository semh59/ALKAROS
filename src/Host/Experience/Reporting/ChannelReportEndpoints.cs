using ALKAROS.Host.Composition.Errors;
using ALKAROS.Host.Experience.Reconciliation;
using ALKAROS.Reporting.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ALKAROS.Host.Experience.Reporting;

/// <summary>
/// V12-RPT-001: the versioned QR/online channel report. Same gate as the payment settlement report:
/// the manager session plus reports.view; an invalid range is refused with the shared Turkish
/// validation error.
/// </summary>
public static class ChannelReportEndpoints
{
    public static RouteGroupBuilder MapChannelReportApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ApiErrorHandling.EnsureFor(endpoints);
        var group = endpoints.MapGroup("/api/v1/management/reports/channels");
        group.AddEndpointFilter<ReconciliationCaseEndpointFilter>();

        group.MapGet("/", async (
            DateOnly from,
            DateOnly to,
            string? source,
            IChannelReportService reports,
            CancellationToken cancellationToken) =>
            Results.Ok(await reports.GetReportAsync(new ChannelReportFilter(from, to, source), cancellationToken)));

        return group;
    }
}
