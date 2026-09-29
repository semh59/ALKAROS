using ALKAROS.Audit.EventStore;
using ALKAROS.Billing.Adjustments;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Billing.SplitDesign;
using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Behavioural;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Identity.Authorization.Delegations;
using ALKAROS.Identity.Authorization.Grants;
using ALKAROS.Identity.Authorization.Policies;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Settings.GarsonFeatureToggles;
using ALKAROS.Settings.TypedSettings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data.Common;
using System.Text.Json;

namespace ALKAROS.Host.Experience.Billing;

public static class BillingSplitApplication
{
    public const string MutationPermission = ApplicationPermissions.BillsSplit;
    public const string RoutePrefix = "/api/v1/terminals/{terminalId:guid}/billing/bills/{billId:guid}/split-design";
    public const string BillsRoutePrefix = "/api/v1/terminals/{terminalId:guid}/billing/bills";

    public static IServiceCollection AddBillingSplitExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<DualScreenStore>();
        services.TryAddSingleton<IRoleRepository, PostgresRoleRepository>();
        services.TryAddSingleton<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddSingleton<IAuthorizationService, AuthorizationService>();
        services.TryAddSingleton<IBillRepository, PostgresBillRepository>();
        services.TryAddSingleton<ISplitDesignRepository, PostgresSplitDesignRepository>();
        // Found while adding a regression test for H1 (independent audit,
        // 2026-09-05): AddBillingSplitExperience never registered
        // IOrderRepository, even though BillingSplitStore.
        // CreateBillFromOrderAsync (POST .../bills/from-order/{orderId})
        // requires it — the constructor's `IOrderRepository? orders = null`
        // silently resolves to null in a standalone composition, and every
        // call throws "Order repository is not configured." This endpoint
        // only ever worked in the full Host composition, where the Orders
        // module happens to register it first — the same class of gap
        // already found and fixed for Catalog.
        services.TryAddSingleton<IOrderRepository, PostgresOrderRepository>();
        // V1-RMD-103 (B1): the grant-request flow (V1-IAM-019/020/021/023)
        // already had a first real caller (V1-BIL-005's comp endpoint); the
        // bills.discount permission it was built for had none until now.
        // Same registration set as AddOrderManagementExperience so this
        // module's standalone composition can resolve the filter chain.
        services.TryAddSingleton<IBillAdjustmentRepository, PostgresBillAdjustmentRepository>();
        services.TryAddSingleton<IAuthorizationGrantRepository, PostgresAuthorizationGrantRepository>();
        services.TryAddSingleton<IAuthorizationPolicyRepository, PostgresAuthorizationPolicyRepository>();
        services.TryAddSingleton<IAuthorizationDelegationRepository, PostgresAuthorizationDelegationRepository>();
        services.TryAddSingleton<IEscalationResolver, DelegationEscalationResolver>();
        services.TryAddSingleton<IBehaviouralRateSource, PostgresBehaviouralRateSource>();
        services.TryAddSingleton<IBehaviouralTighteningRepository, PostgresBehaviouralTighteningRepository>();
        services.TryAddSingleton<IPrePolicyGate, BehaviouralTighteningGate>();
        services.TryAddSingleton<IAuthorizationGrantService, AuthorizationGrantService>();
        // V1-SET-004: ApplyTipAsync checks GarsonFeature.VoluntaryTip — same
        // DbDataSource/ISettingValidator gap every other settings-backed
        // experience registration already had to close on its own.
        services.TryAddSingleton<DbDataSource>(serviceProvider =>
            serviceProvider.GetRequiredService<NpgsqlDataSource>());
        services.TryAddSingleton<ISettingValidator, SettingValidator>();
        services.TryAddSingleton<ISettingsRepository, PostgresSettingsRepository>();
        services.TryAddSingleton<ISettingsService, SettingsService>();
        // V1-RMD-237: IAuditEventStore existed since V1-OPS-001 with zero
        // real callers anywhere in the codebase — this is its first.
        services.TryAddSingleton<IAuditEventStore, PostgresAuditEventStore>();
        services.TryAddSingleton<BillingSplitStore>();
        services.TryAddSingleton<IBillingSplitSessionAuthorizer, BillingSplitSessionAuthorizer>();
        services.TryAddTransient<BillingSplitExceptionFilter>();
        return services;
    }

    public static RouteGroupBuilder MapBillingSplitApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var billsGroup = endpoints.MapGroup(BillsRoutePrefix)
            .WithTags("Billing")
            .AddEndpointFilter<BillingSplitExceptionFilter>();

        billsGroup.MapPost("/from-order/{orderId:guid}", async (
            Guid terminalId,
            Guid orderId,
            IBillingSplitSessionAuthorizer authorizer,
            BillingSplitStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.CreateBillFromOrderAsync(orderId, principal.CanMutate, cancellationToken));
        });

        // V1-RMD-103 (B1): bills.discount is grant-class (model §3), same
        // pattern as V1-BIL-005's bills.comp — a role that holds it outright
        // applies directly; a role that does not raises an
        // IAuthorizationGrantService request and the policy engine, an
        // active delegation, or a manager decides.
        billsGroup.MapPost("/{billId:guid}/discount", async (
            Guid terminalId,
            Guid billId,
            ApplyBillDiscountRequestV1 request,
            IBillingSplitSessionAuthorizer authorizer,
            BillingSplitStore store,
            IRoleRepository roles,
            IAuthorizationGrantService grants,
            IAuditEventStore auditEvents,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireReadAsync(context, terminalId, cancellationToken);

            if (!DiscountReasonCatalog.IsValid(request.ReasonCode))
                throw new ArgumentException(
                    $"Reason '{request.ReasonCode}' is not a valid discount catalog reason.", nameof(request));

            var permissions = await roles.GetPermissionCodesForUserAsync(principal.UserId, cancellationToken);
            if (!permissions.Contains(ApplicationPermissions.BillsDiscount, StringComparer.Ordinal))
            {
                var role = await roles.GetGoverningRoleForUserAsync(principal.UserId, cancellationToken);
                if (role is null)
                    throw new AuthorizationDeniedException(
                        principal.UserId, ApplicationPermissions.BillsDiscount, "Requester has no assigned role.");

                var resolution = await grants.RequestAsync(
                    new GrantRequest(
                        request.IdempotencyKey,
                        ApplicationPermissions.BillsDiscount,
                        principal.UserId,
                        role.Code,
                        request.ReasonCode,
                        request.Value,
                        SubjectType: "Bill",
                        SubjectId: billId,
                        SubjectServingUserId: null),
                    cancellationToken);

                switch (resolution.Outcome)
                {
                    case GrantOutcome.Refused:
                        return Results.Json(
                            new { error = new { code = "GRANT_DENIED", message = "Discount request was denied." } },
                            statusCode: StatusCodes.Status403Forbidden);
                    case GrantOutcome.Pending:
                        return Results.Accepted(value: new ApplyBillDiscountResultV1(
                            "Pending", billId, null, null, resolution.Grant.GrantId));
                    case GrantOutcome.Authorized:
                        break;
                    default:
                        throw new InvalidOperationException($"Unhandled grant outcome '{resolution.Outcome}'.");
                }
            }

            var (adjustment, summary) = await store.ApplyDiscountAsync(billId, request, principal.UserId, cancellationToken);
            // V1-RMD-237: first real caller of IAuditEventStore.AppendAsync —
            // this was registered since V1-OPS-001 but never actually
            // invoked anywhere in the codebase. Only the applied outcome is
            // recorded here; Pending/Refused grant outcomes above already
            // have their own record in identity.authorization_grants.
            await auditEvents.AppendAsync(
                new AuditEvent(
                    id: Guid.NewGuid(),
                    eventName: "bill.discount.applied",
                    aggregateType: "Bill",
                    aggregateId: billId,
                    actorType: "User",
                    correlationId: context.TraceIdentifier,
                    actorId: principal.UserId,
                    reason: request.ReasonCode,
                    beforeStateJson: JsonSerializer.Serialize(new { payableAmount = summary.OriginalPayableAmount }),
                    afterStateJson: JsonSerializer.Serialize(new
                    {
                        payableAmount = summary.AdjustedPayableAmount,
                        discountId = adjustment.Id,
                        discountValue = request.Value,
                    })),
                cancellationToken);
            return Results.Ok(new ApplyBillDiscountResultV1(
                "Applied",
                billId,
                adjustment.Id,
                new AdjustedBillSummaryV1(
                    summary.OriginalPayableAmount,
                    summary.TotalDiscounts,
                    summary.TotalFees,
                    summary.TotalTips,
                    summary.AdjustedPayableAmount),
                null));
        });

        // V1-WTR-020: records a voluntary tip. bills.split is checked
        // directly (RequireMutationAsync, no grant-request dance) - unlike
        // discount/comp, this is never a discretionary decision a role might
        // lack the authority for, only data entry of money already handed
        // over. See ApplyBillTipRequestV1's own doc comment and
        // BillAdjustment.CreateServiceFee's remark for why a service-charge
        // endpoint must never exist alongside this one.
        billsGroup.MapPost("/{billId:guid}/tip", async (
            Guid terminalId,
            Guid billId,
            ApplyBillTipRequestV1 request,
            IBillingSplitSessionAuthorizer authorizer,
            BillingSplitStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, cancellationToken);
            var (adjustment, summary) = await store.ApplyTipAsync(billId, request, principal.UserId, cancellationToken);
            return Results.Ok(new ApplyBillTipResultV1(
                billId,
                adjustment.Id,
                new AdjustedBillSummaryV1(
                    summary.OriginalPayableAmount,
                    summary.TotalDiscounts,
                    summary.TotalFees,
                    summary.TotalTips,
                    summary.AdjustedPayableAmount)));
        });

        billsGroup.MapGet("/{billId:guid}/adjustments", async (
            Guid terminalId,
            Guid billId,
            IBillingSplitSessionAuthorizer authorizer,
            BillingSplitStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            var (adjustments, summary) = await store.GetAdjustmentsAsync(billId, cancellationToken);
            return Results.Ok(new
            {
                Adjustments = adjustments.Select(a => new BillAdjustmentDto(
                    a.Id,
                    a.AdjustmentType.ToString(),
                    a.CalculationType.ToString(),
                    a.Rate,
                    a.Amount,
                    a.IsDeduction,
                    a.Reason,
                    a.Notes,
                    a.CreatedAt)).ToList(),
                Summary = new AdjustedBillSummaryV1(
                    summary.OriginalPayableAmount,
                    summary.TotalDiscounts,
                    summary.TotalFees,
                    summary.TotalTips,
                    summary.AdjustedPayableAmount),
            });
        });

        var group = endpoints.MapGroup(RoutePrefix)
            .WithTags("BillingSplitDesign")
            .AddEndpointFilter<BillingSplitExceptionFilter>();

        group.MapGet("", async (
            Guid terminalId,
            Guid billId,
            IBillingSplitSessionAuthorizer authorizer,
            BillingSplitStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.GetAsync(billId, principal.CanMutate, cancellationToken));
        });

        group.MapGet("/owners", async (
            Guid terminalId,
            Guid billId,
            IBillingSplitSessionAuthorizer authorizer,
            BillingSplitStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.GetOwnerOptionsAsync(billId, cancellationToken));
        });

        group.MapPut("/equal", async (
            Guid terminalId,
            Guid billId,
            SaveEqualSplitRequest request,
            IBillingSplitSessionAuthorizer authorizer,
            BillingSplitStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.SaveEqualAsync(billId, request, principal.UserId, cancellationToken));
        });

        group.MapPut("/items", async (
            Guid terminalId,
            Guid billId,
            SaveItemSplitRequest request,
            IBillingSplitSessionAuthorizer authorizer,
            BillingSplitStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.SaveItemsAsync(billId, request, principal.UserId, cancellationToken));
        });

        group.MapPut("/amounts", async (
            Guid terminalId,
            Guid billId,
            SaveAmountSplitRequest request,
            IBillingSplitSessionAuthorizer authorizer,
            BillingSplitStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.SaveAmountsAsync(billId, request, principal.UserId, cancellationToken));
        });

        group.MapPut("/custom", async (
            Guid terminalId,
            Guid billId,
            SaveCustomSplitRequest request,
            IBillingSplitSessionAuthorizer authorizer,
            BillingSplitStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.SaveCustomAsync(billId, request, principal.UserId, cancellationToken));
        });

        group.MapPost("/clear", async (
            Guid terminalId,
            Guid billId,
            ClearSplitDesignRequest request,
            IBillingSplitSessionAuthorizer authorizer,
            BillingSplitStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequireMutationAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.ClearAsync(billId, request, cancellationToken));
        });

        return group;
    }
}

internal sealed record BillingSplitPrincipal(Guid UserId, bool CanMutate);

internal interface IBillingSplitSessionAuthorizer
{
    Task<BillingSplitPrincipal> RequireReadAsync(HttpContext context, Guid terminalId, CancellationToken cancellationToken);

    Task<BillingSplitPrincipal> RequireMutationAsync(HttpContext context, Guid terminalId, CancellationToken cancellationToken);
}

internal sealed class BillingSplitSessionAuthorizer : IBillingSplitSessionAuthorizer
{
    private readonly DualScreenStore _sessions;
    private readonly IRoleRepository _roles;
    private readonly IAuthorizationService _authorization;

    public BillingSplitSessionAuthorizer(
        DualScreenStore sessions,
        IRoleRepository roles,
        IAuthorizationService authorization)
    {
        _sessions = sessions;
        _roles = roles;
        _authorization = authorization;
    }

    public async Task<BillingSplitPrincipal> RequireReadAsync(
        HttpContext context,
        Guid terminalId,
        CancellationToken cancellationToken)
    {
        if (terminalId == Guid.Empty)
            throw new ArgumentException("Terminal ID cannot be empty.", nameof(terminalId));
        var token = context.Request.Cookies[DualScreenApplication.CashierCookieName];
        var cashier = await _sessions.AuthenticateCashierAsync(token, terminalId, cancellationToken)
            ?? throw new BillingSplitUnauthorizedException("A valid terminal-bound cashier session is required.");
        var permissions = await _roles.GetPermissionCodesForUserAsync(cashier.UserId, cancellationToken);
        return new BillingSplitPrincipal(
            cashier.UserId,
            permissions.Contains(BillingSplitApplication.MutationPermission, StringComparer.Ordinal));
    }

    public async Task<BillingSplitPrincipal> RequireMutationAsync(
        HttpContext context,
        Guid terminalId,
        CancellationToken cancellationToken)
    {
        var principal = await RequireReadAsync(context, terminalId, cancellationToken);
        await _authorization.AuthorizeAsync(principal.UserId, BillingSplitApplication.MutationPermission, cancellationToken);
        return principal with { CanMutate = true };
    }
}

internal sealed class BillingSplitExceptionFilter : IEndpointFilter
{
    private static readonly Action<ILogger, string, string, Exception?> LogRequestFailure =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(5200, nameof(LogRequestFailure)),
            "Billing split request failed on {Path} ({TraceIdentifier}).");

    private readonly ILogger<BillingSplitExceptionFilter> _logger;

    public BillingSplitExceptionFilter(ILogger<BillingSplitExceptionFilter> logger)
    {
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext invocationContext, EndpointFilterDelegate next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (Exception exception)
        {
            var mapped = Map(exception);
            if (mapped.Status >= StatusCodes.Status500InternalServerError)
            {
                LogRequestFailure(
                    _logger,
                    invocationContext.HttpContext.Request.Path,
                    invocationContext.HttpContext.TraceIdentifier,
                    exception);
            }

            var conflict = exception is SplitDesignConcurrencyException concurrency
                ? new BillingSplitConflict(
                    concurrency.Resource,
                    concurrency.Id,
                    concurrency.Expected,
                    concurrency.Actual)
                : null;
            return Results.Json(
                new BillingSplitErrorEnvelope(new BillingSplitError(
                    mapped.Code,
                    mapped.Message,
                    mapped.Status,
                    invocationContext.HttpContext.TraceIdentifier,
                    conflict)),
                statusCode: mapped.Status);
        }
    }

    private static (int Status, string Code, string Message) Map(Exception exception) => exception switch
    {
        BillingSplitUnauthorizedException => (401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
        AuthorizationDeniedException => (403, "FORBIDDEN", "Bu işlem için yetkiniz yok."),
        BillingSplitNotFoundException or KeyNotFoundException => (404, "NOT_FOUND", "İstenen hesap bulunamadı."),
        SplitDesignConcurrencyException => (409, "CONCURRENT_MODIFICATION", "Hesap bölme tasarımı başka bir işlem tarafından değiştirildi."),
        SplitDesignUnsupportedBillStateException => (409, "UNSUPPORTED_BILL_STATE", "Bu hesap durumunda bölme tasarımı değiştirilemez."),
        BillDiscountUnsupportedBillStateException => (409, "UNSUPPORTED_BILL_STATE", "Bu hesap durumunda indirim uygulanamaz."),
        BillDiscountBelowCollectedException => (409, "DISCOUNT_BELOW_COLLECTED", "Bu indirim, hesaptan şimdiye kadar alınan tutarın altına iniyor; uygulanamaz."),
        SplitItemsOnAdjustedBillException => (409, "ITEM_SPLIT_ON_ADJUSTED_BILL", "İndirim veya ek ücret uygulanmış hesap ürün bazında bölünemez; tutar, kişi ya da serbest bölme kullanın."),
        IdempotencyKeyReusedException => (409, "IDEMPOTENCY_KEY_REUSED", "Bu işlem anahtarı farklı bir istek için zaten kullanılmış."),
        GarsonFeatureDisabledException => (403, "FEATURE_DISABLED", "Bu özellik bu işletme için kapatılmış."),
        ArgumentException or InvalidOperationException or BadHttpRequestException => (400, "VALIDATION_FAILED", "Hesap bölme isteği doğrulanamadı."),
        PostgresException postgres when postgres.SqlState == PostgresErrorCodes.SerializationFailure =>
            (409, "CONCURRENT_MODIFICATION", "Hesap bölme tasarımı başka bir işlem tarafından değiştirildi."),
        PostgresException or NpgsqlException => (503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
        _ => (500, "INTERNAL_ERROR", "İşlem tamamlanamadı."),
    };
}

public sealed class BillingSplitUnauthorizedException : Exception
{
    public BillingSplitUnauthorizedException(string message) : base(message) { }
}
