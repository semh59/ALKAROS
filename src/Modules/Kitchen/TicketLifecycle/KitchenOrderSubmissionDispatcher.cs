namespace ALKAROS.Kitchen.TicketLifecycle;

using ALKAROS.Kitchen.Routing;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Orders.SubmitOrder;
using Npgsql;

/// <summary>
/// Creates the station-scoped kitchen ticket(s) for a submitted order in the
/// caller's transaction. Each active order item is routed to a station through
/// the configured printer routes (Item / Product / Default precedence); items
/// that do not resolve fall back to the configured default station. An order
/// whose items land on several stations produces one ticket per station, which
/// the <c>kitchen_tickets</c> (order_id, station_id) shape already anticipates.
/// A per-station advisory transaction lock makes replay and concurrent
/// submissions deterministic even before a ticket row exists.
///
/// When no routing collaborators are supplied the dispatcher behaves exactly as
/// before: a single ticket at the configured default station
/// (deep-analysis finding B-3).
/// </summary>
public sealed class KitchenOrderSubmissionDispatcher : IOrderSubmissionDispatcher
{
    private readonly IKitchenTicketRepository _ticketRepository;
    private readonly string _defaultStationId;
    private readonly IKitchenPrinterRouter? _router;
    private readonly IPrinterRepository? _printerRepository;
    private readonly IPrinterRouteRepository? _routeRepository;

    public KitchenOrderSubmissionDispatcher(
        IKitchenTicketRepository ticketRepository,
        string stationId,
        IKitchenPrinterRouter? router = null,
        IPrinterRepository? printerRepository = null,
        IPrinterRouteRepository? routeRepository = null)
    {
        _ticketRepository = ticketRepository ?? throw new ArgumentNullException(nameof(ticketRepository));
        _defaultStationId = stationId?.Trim() ?? string.Empty;
        _router = router;
        _printerRepository = printerRepository;
        _routeRepository = routeRepository;
    }

    private bool RoutingEnabled => _router is not null && _printerRepository is not null && _routeRepository is not null;

    public async Task DispatchAsync(
        Order order,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        var activeItems = order.Items.Where(item => item.IsActive).ToList();
        if (activeItems.Count == 0)
            return;

        var stationByItemId = await ResolveStationsAsync(activeItems, cancellationToken).ConfigureAwait(false);

        if (stationByItemId.Values.Any(string.IsNullOrWhiteSpace))
        {
            throw new OrderSubmissionDispatchException(
                "Kitchen station is not configured; submitted order was not acknowledged.");
        }

        var stations = stationByItemId.Values.Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal);

        try
        {
            foreach (var station in stations)
            {
                await using (var lockCommand = connection.CreateCommand())
                {
                    lockCommand.Transaction = transaction;
                    lockCommand.CommandText =
                        "SELECT pg_advisory_xact_lock(hashtextextended(@lock_key, 0));";
                    lockCommand.Parameters.AddWithValue("lock_key", $"{order.Id:D}:{station}");
                    await lockCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                await using (var existingCommand = connection.CreateCommand())
                {
                    existingCommand.Transaction = transaction;
                    existingCommand.CommandText =
                        "SELECT 1 FROM kitchen.kitchen_tickets WHERE order_id = @order_id AND station_id = @station_id LIMIT 1;";
                    existingCommand.Parameters.AddWithValue("order_id", order.Id);
                    existingCommand.Parameters.AddWithValue("station_id", station);
                    if (await existingCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null)
                        continue;
                }

                var stationScope = station;
                var ticket = KitchenTicket.CreateFromOrder(
                    order,
                    stationScope,
                    itemFilter: item => stationByItemId.TryGetValue(item.Id, out var resolved)
                        && string.Equals(resolved, stationScope, StringComparison.Ordinal));

                await _ticketRepository.AddAsync(ticket, connection, transaction, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OrderSubmissionDispatchException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidOperationException or NpgsqlException)
        {
            throw new OrderSubmissionDispatchException(
                $"Kitchen ticket dispatch failed for order '{order.Id}'.",
                exception);
        }
    }

    private async Task<Dictionary<Guid, string>> ResolveStationsAsync(
        IReadOnlyList<OrderItem> activeItems,
        CancellationToken cancellationToken)
    {
        var stationByItemId = new Dictionary<Guid, string>();

        if (!RoutingEnabled)
        {
            foreach (var item in activeItems)
                stationByItemId[item.Id] = _defaultStationId;
            return stationByItemId;
        }

        // Routing configuration is stable operational state; it is read outside
        // the order-submission transaction, once per submission.
        var printers = await _printerRepository!.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var routes = await _routeRepository!.GetActiveRoutesAsync(cancellationToken).ConfigureAwait(false);

        return MapItemsToStations(activeItems, routes, printers, _router!, _defaultStationId);
    }

    /// <summary>
    /// Pure routing decision: resolves each active order item to a station via
    /// the printer-route precedence chain, falling back to
    /// <paramref name="defaultStationId"/> when nothing resolves.
    /// </summary>
    public static Dictionary<Guid, string> MapItemsToStations(
        IReadOnlyList<OrderItem> activeItems,
        IReadOnlyList<PrinterRoute> routes,
        IReadOnlyList<Printer> printers,
        IKitchenPrinterRouter router,
        string defaultStationId)
    {
        var printersById = printers.DistinctBy(printer => printer.Id).ToDictionary(printer => printer.Id);
        var stationByItemId = new Dictionary<Guid, string>();

        foreach (var item in activeItems)
        {
            var request = new RoutingEvaluationRequest(item.ProductId, itemId: item.Id);
            var result = router.ResolveRoute(request, routes, printers);

            stationByItemId[item.Id] = result.Resolved
                && result.PrinterId is { } printerId
                && printersById.TryGetValue(printerId, out var printer)
                    ? printer.StationId
                    : defaultStationId;
        }

        return stationByItemId;
    }
}
