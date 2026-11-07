using System.Globalization;
using ALKAROS.CustomerAccounts.Aging;
using ALKAROS.CustomerAccounts.BalanceProjection;
using ALKAROS.Host.Composition.Errors;
using ALKAROS.Host.Experience.CustomerAccounts;
using ALKAROS.Host.Experience.SecurityAdministration.Maintenance;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Reporting.BusinessDayTotals;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ALKAROS.Host.Experience.CustomerAging;

/// <summary>
/// F0097: the customer receivables aging report. Same gate as the other management reports: a manager or supervisor
/// session plus reports.view. Customers are aged from the ledger (payments settle the oldest charges first); the
/// balances 30/60/90 days ago come from the daily snapshots.
/// </summary>
public static class ReceivablesAgingEndpoints
{
    public const string ManagerCookieName = "alkaros.manager";
    public const string ViewPermission = ApplicationPermissions.ReportsView;
    public const string ReportVersion = "customer-aging.v1";

    /// <summary>Shown when the customer has no readable profile (anonymized or removed).</summary>
    public const string UnknownCustomerName = "Profili okunamayan müşteri";

    public static readonly BusinessCalendar Calendar = new(BusinessDayWindow.DefaultTimeZoneId, BusinessDayWindow.StartHour);

    private static readonly int[] LookbackDays = [30, 60, 90];

    public static IServiceCollection AddCustomerAgingExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IRoleRepository, PostgresRoleRepository>();
        services.TryAddScoped<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddScoped<IAuthorizationService, AuthorizationService>();
        services.TryAddScoped<CustomerAgingEndpointFilter>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMaintenanceJob, BalanceSnapshotMaintenanceJob>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMaintenanceJob, BalanceReconcileMaintenanceJob>());
        return services;
    }

    public static RouteGroupBuilder MapCustomerAgingApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ApiErrorHandling.EnsureFor(endpoints);
        var group = endpoints.MapGroup("/api/v1/management/reports/customer-aging");
        group.AddEndpointFilter<CustomerAgingEndpointFilter>();

        group.MapGet("/", async (
            IReceivablesAgingService aging,
            IBalanceSnapshotStore snapshots,
            CustomerAccountsStore accounts,
            TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var asOf = Calendar.DateOf(clock.GetUtcNow());
            var customers = await aging.GetAsync(asOf, Calendar, cancellationToken);
            var past = (await snapshots.GetSnapshotsOnAsync(LookbackDays.Select(days => asOf.AddDays(-days)).ToArray(), cancellationToken))
                .ToDictionary(snapshot => (snapshot.CustomerId, snapshot.SnapshotDate), snapshot => snapshot.Balance);

            decimal? Past(Guid customerId, int days)
                => past.TryGetValue((customerId, asOf.AddDays(-days)), out var balance) ? balance : null;

            var rows = new List<CustomerAgingRowV1>(customers.Count);
            foreach (var customer in customers)
            {
                var summary = await accounts.GetAsync(customer.CustomerId, cancellationToken);
                rows.Add(new CustomerAgingRowV1(
                    customer.CustomerId, summary?.Name ?? UnknownCustomerName,
                    customer.Days0To30, customer.Days31To60, customer.Days61To90, customer.Over90,
                    customer.TotalDebt, customer.UnappliedCredit,
                    Past(customer.CustomerId, 30), Past(customer.CustomerId, 60), Past(customer.CustomerId, 90)));
            }

            return Results.Ok(new CustomerAgingReportV1(
                ReportVersion, asOf.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), rows, new CustomerAgingCheckV1(customers.All(customer => customer.IsBalanced))));
        });

        return group;
    }
}

public sealed record CustomerAgingRowV1(
    Guid CustomerId,
    string Name,
    decimal Days0To30,
    decimal Days31To60,
    decimal Days61To90,
    decimal Over90,
    decimal TotalDebt,
    decimal UnappliedCredit,
    decimal? Balance30DaysAgo,
    decimal? Balance60DaysAgo,
    decimal? Balance90DaysAgo);

public sealed record CustomerAgingCheckV1(bool IsBalanced);

public sealed record CustomerAgingReportV1(string ReportVersion, string AsOf, IReadOnlyList<CustomerAgingRowV1> Rows, CustomerAgingCheckV1 Check);

public sealed class CustomerAgingUnauthorizedException : Exception
{
    public CustomerAgingUnauthorizedException() : base("A valid management session is required.")
    {
    }
}

public sealed class CustomerAgingEndpointFilter : IEndpointFilter
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IAuthorizationService _authorization;

    public CustomerAgingEndpointFilter(NpgsqlDataSource dataSource, IAuthorizationService authorization)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        ApiErrorScope.Enter(httpContext, ApiErrorCatalog.CustomerAging);
        var actorId = await ManagementSessionLookup.ResolveActorAsync(
                _dataSource, httpContext.Request.Cookies[ReceivablesAgingEndpoints.ManagerCookieName],
                allowSupervisor: true, httpContext.RequestAborted)
            ?? throw new CustomerAgingUnauthorizedException();
        await _authorization.AuthorizeAsync(actorId, ReceivablesAgingEndpoints.ViewPermission, httpContext.RequestAborted);
        return await next(context);
    }
}
