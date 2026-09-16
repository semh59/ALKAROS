using ALKAROS.Host.DualScreen;
using Microsoft.AspNetCore.SignalR;
using Npgsql;

namespace ALKAROS.Host.Experience.InventoryReporting;

/// <summary>
/// V11-RPT-002: pushes a stock item's transition INTO critical (never a
/// repeat of one already known-critical, see
/// <see cref="LowStockAlertHostedService"/>'s own doc comment) to every
/// connected manager/supervisor session — the same flat-broadcast shape as
/// <c>HelpRequestHub</c>, for the same reason (no manager-to-terminal
/// assignment tracked anywhere to target one specific device).
/// </summary>
public sealed class LowStockAlertHub : Hub
{
    public const string Route = "/hubs/low-stock-alerts";
    public const string StockBecameCritical = "StockBecameCritical";

    private readonly NpgsqlDataSource _dataSource;

    public LowStockAlertHub(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var rawToken = httpContext?.Request.Cookies[ManagerCookieName];
        var actorId = await ManagementSessionLookup.ResolveActorAsync(
            _dataSource, rawToken, allowSupervisor: true, Context.ConnectionAborted);
        if (actorId is null)
        {
            Context.Abort();
            return;
        }

        await base.OnConnectedAsync();
    }

    // Duplicated by design, not a boundary violation — see
    // ManagementSessionLookup's own doc comment (V1-RMD-121).
    private const string ManagerCookieName = "alkaros.manager";
}
