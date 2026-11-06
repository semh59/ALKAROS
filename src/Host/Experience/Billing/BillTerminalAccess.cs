using ALKAROS.Billing.BillFoundation;
using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ALKAROS.Host.Experience.Billing;

public static class BillTerminalAccessRegistration
{
    public static IServiceCollection AddBillTerminalAccess(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<DualScreenStore>();
        services.TryAddSingleton<IRoleRepository, PostgresRoleRepository>();
        services.TryAddSingleton<IBillTerminalOwnerRepository, PostgresBillTerminalOwnerRepository>();
        services.TryAddSingleton<BillTerminalAccess>();
        services.TryAddTransient<BillTerminalAccessFilter>();
        return services;
    }
}

/// <summary>A terminal sees and handles only its own bills; a user holding cash.session.override handles any.</summary>
public sealed class BillTerminalAccess
{
    private readonly DualScreenStore _sessions;
    private readonly IRoleRepository _roles;
    private readonly IBillTerminalOwnerRepository _owners;
    private readonly NpgsqlDataSource _dataSource;

    public BillTerminalAccess(
        DualScreenStore sessions, IRoleRepository roles, IBillTerminalOwnerRepository owners, NpgsqlDataSource dataSource)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _roles = roles ?? throw new ArgumentNullException(nameof(roles));
        _owners = owners ?? throw new ArgumentNullException(nameof(owners));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    /// <summary>
    /// Refuses the caller when another terminal owns the bill. A write also claims a bill nobody owns yet for the terminal;
    /// a read never does. A user who sees all bills owns nothing and claims nothing. A request without a valid cashier session, or for a bill
    /// that does not exist, passes: the endpoint answers those itself.
    /// </summary>
    public async Task RequireAsync(HttpContext context, Guid terminalId, Guid billId, CancellationToken cancellationToken)
    {
        var cashier = await _sessions.AuthenticateCashierAsync(
            context.Request.Cookies[DualScreenApplication.CashierCookieName], terminalId, cancellationToken);
        if (cashier is null || await SeesAllBillsAsync(cashier.UserId, cancellationToken))
            return;

        var owner = HttpMethods.IsGet(context.Request.Method)
            ? await _owners.FindOwnerAsync(billId, cancellationToken)
            : await _owners.ClaimAsync(billId, terminalId, cancellationToken);
        if (owner is { } ownerTerminal && ownerTerminal != terminalId)
            throw new BillOtherTerminalException(billId, ownerTerminal);
    }

    public async Task RequireForRefundRequestAsync(
        HttpContext context, Guid terminalId, Guid refundRequestId, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            "SELECT bill_id FROM payments.refund_requests WHERE refund_request_id = @id;");
        command.Parameters.AddWithValue("id", refundRequestId);
        if (await command.ExecuteScalarAsync(cancellationToken) is Guid billId)
            await RequireAsync(context, terminalId, billId, cancellationToken);
    }

    /// <summary>The bills among the given ones that the terminal may handle: its own and those nobody owns yet.</summary>
    public async Task<IReadOnlySet<Guid>> VisibleBillIdsAsync(
        IEnumerable<Guid> billIds, Guid terminalId, CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            "SELECT bill_id FROM billing.bills WHERE bill_id = ANY(@ids) AND (terminal_id IS NULL OR terminal_id = @terminal);");
        command.Parameters.AddWithValue("ids", billIds.Distinct().ToArray());
        command.Parameters.AddWithValue("terminal", terminalId);
        var visible = new HashSet<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            visible.Add(reader.GetGuid(0));
        return visible;
    }

    public async Task<bool> SeesAllBillsAsync(Guid userId, CancellationToken cancellationToken) =>
        (await _roles.GetPermissionCodesForUserAsync(userId, cancellationToken))
            .Contains(ApplicationPermissions.CashSessionOverride, StringComparer.Ordinal);
}

/// <summary>Runs the bill ownership check for every route that carries a <c>billId</c>.</summary>
public sealed class BillTerminalAccessFilter : IEndpointFilter
{
    private readonly BillTerminalAccess _access;

    public BillTerminalAccessFilter(BillTerminalAccess access)
    {
        _access = access ?? throw new ArgumentNullException(nameof(access));
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var route = http.GetRouteData().Values;
        if (route.TryGetValue("billId", out var bill) && Guid.TryParse(bill?.ToString(), out var billId)
            && route.TryGetValue("terminalId", out var terminal) && Guid.TryParse(terminal?.ToString(), out var terminalId))
            await _access.RequireAsync(http, terminalId, billId, http.RequestAborted);
        return await next(context);
    }
}

public sealed class BillOtherTerminalException(Guid billId, Guid ownerTerminalId)
    : Exception($"Bill {billId} belongs to terminal {ownerTerminalId}.")
{
    public const string UserMessage = "Bu hesap başka bir kasada açık; yalnızca o kasa veya bir yönetici işlem yapabilir.";
}
