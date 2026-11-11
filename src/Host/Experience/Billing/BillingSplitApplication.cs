using ALKAROS.Audit.EventStore;
using ALKAROS.Billing.Adjustments;
using ALKAROS.Billing.BillFoundation;
using ALKAROS.Billing.PaymentClosure;
using ALKAROS.Billing.SplitDesign;
using ALKAROS.Host.Composition.Errors;
using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Behavioural;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Identity.Authorization.Delegations;
using ALKAROS.Identity.Authorization.Grants;
using ALKAROS.Identity.Authorization.Policies;
using ALKAROS.Host.Experience.Orders;
using ALKAROS.Host.Experience.Orders.OrderCancellation;
using ALKAROS.Host.Experience.Orders.OrderCleanup;
using ALKAROS.Host.Experience.Orders.SentItemVoid;
using ALKAROS.Payments.Allocations.Persistence;
using ALKAROS.Payments.PaymentAggregate;
using ALKAROS.Orders.OrderAggregate;
using ALKAROS.Settings.GarsonFeatureToggles;
using ALKAROS.Settings.ManagerSelfApproval;
using ALKAROS.Settings.TypedSettings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
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
        // A discount that zeroes the payable closes the bill, so the standalone composition needs the closure chain.
        services.AddSignalR();
        services.TryAddSingleton<IPaymentRepository, PostgresPaymentRepository>();
        services.TryAddSingleton<IPaymentAllocationRepository, PostgresPaymentAllocationRepository>();
        services.TryAddSingleton<IBillPaymentClosureProjector, BillPaymentClosureProjector>();
        services.TryAddSingleton<IBillClosureService, BillClosureService>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, BillClosureSweeper>());
        services.TryAddSingleton<OrderSettlementService>();
        services.TryAddSingleton<ALKAROS.Tables.TableRelease.ITableReleasePort, ALKAROS.Tables.TableRelease.PostgresTableReleasePort>();
        services.TryAddSingleton<IAuthorizationGrantRepository, PostgresAuthorizationGrantRepository>();
        services.TryAddSingleton<IAuthorizationPolicyRepository, PostgresAuthorizationPolicyRepository>();
        services.TryAddSingleton<IAuthorizationDelegationRepository, PostgresAuthorizationDelegationRepository>();
        services.TryAddSingleton<IEscalationResolver, DelegationEscalationResolver>();
        services.TryAddSingleton<IBehaviouralRateSource, PostgresBehaviouralRateSource>();
        services.TryAddSingleton<IBehaviouralTighteningRepository, PostgresBehaviouralTighteningRepository>();
        services.TryAddSingleton<IPrePolicyGate, BehaviouralTighteningGate>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPrePolicyGate, BillAdjustmentTotalGate>());
        services.TryAddSingleton<IBillAdjustmentTotalCap, SettingsBillAdjustmentTotalCap>();
        services.TryAddSingleton<IBillAdjustmentTotals, BillAppliedAdjustmentTotals>();
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
        services.AddOrderCleanup();
        services.TryAddSingleton<ICashierQueueAnnouncer, NoOpCashierQueueAnnouncer>();
        services.TryAddSingleton<BillCancellationStore>();
        services.TryAddSingleton<PaymentRefundStore>();
        services.AddBillTerminalAccess();
        services.TryAddSingleton<IBillingSplitSessionAuthorizer, BillingSplitSessionAuthorizer>();
        services.TryAddTransient<BillingSplitExceptionFilter>();
        return services;
    }

    private static async Task<IResult> ApplyDiscountOrCompAsync(
        string permission,
        bool complimentary,
        Guid terminalId,
        Guid billId,
        ApplyBillDiscountRequestV1 request,
        IBillingSplitSessionAuthorizer authorizer,
        BillingSplitStore store,
        IRoleRepository roles,
        IAuthorizationGrantService grants,
        IAuditEventStore auditEvents,
        IBillClosureService billClosure,
        OrderSettlementService orderSettlement,
        IHubContext<CustomerDisplayHub> customerDisplayHub,
        ILoggerFactory loggerFactory,
        NpgsqlDataSource dataSource,
        IBillAdjustmentTotalCap totalCap,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var principal = await authorizer.RequireReadAsync(context, terminalId, cancellationToken);

        await using var adjustmentLock = await BillAdjustmentLock.AcquireAsync(dataSource, billId, cancellationToken);

        await store.EnsureDiscountRequestableAsync(billId, request, principal.UserId, complimentary, cancellationToken);

        Guid? approverId = null;
        Guid? grantId = null;
        var permissions = await roles.GetPermissionCodesForUserAsync(principal.UserId, cancellationToken);
        if (!permissions.Contains(permission, StringComparer.Ordinal))
        {
            var role = await roles.GetGoverningRoleForUserAsync(principal.UserId, cancellationToken);
            if (role is null)
                throw new AuthorizationDeniedException(
                    principal.UserId, permission, "Requester has no assigned role.");

            // Limits and delegations are in currency; a percentage is turned into the amount it takes off this bill.
            var grantAmount = string.Equals(request.CalculationType, "Percentage", StringComparison.Ordinal)
                ? BillMath.RoundCurrency(
                    (await store.GetAdjustmentsAsync(billId, cancellationToken)).Summary.OriginalPayableAmount * request.Value / 100m)
                : request.Value;

            var resolution = await grants.RequestAsync(
                new GrantRequest(
                    request.IdempotencyKey,
                    permission,
                    principal.UserId,
                    role.Code,
                    request.ReasonCode,
                    grantAmount,
                    SubjectType: "Bill",
                    SubjectId: billId,
                    SubjectServingUserId: null),
                cancellationToken);

            switch (resolution.Outcome)
            {
                case GrantOutcome.Refused:
                    await AppendGrantOutcomeAuditOnceAsync(
                        auditEvents, "denied", complimentary, billId, principal.UserId, request, grantAmount, resolution.Grant,
                        context.TraceIdentifier, cancellationToken);
                    throw new GrantDeniedException(complimentary);
                case GrantOutcome.Pending:
                    await AppendGrantOutcomeAuditOnceAsync(
                        auditEvents, "pending", complimentary, billId, principal.UserId, request, grantAmount, resolution.Grant,
                        context.TraceIdentifier, cancellationToken);
                    return Results.Accepted(value: new ApplyBillDiscountResultV1(
                        "Pending", billId, null, null, resolution.Grant.GrantId));
                case GrantOutcome.Authorized:
                    approverId = resolution.Grant.ApproverUserId;
                    grantId = resolution.Grant.GrantId;
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled grant outcome '{resolution.Outcome}'.");
            }
        }

        var (adjustment, summary) = await store.ApplyDiscountAsync(
            billId, request, principal.UserId, auditEvents, context.TraceIdentifier, approverId, grantId,
            complimentary, await totalCap.GetAsync(cancellationToken), cancellationToken);
        await adjustmentLock.DisposeAsync();
        // A discount that takes the payable to zero leaves nothing to collect, so no tender will ever close the bill.
        if (summary.AdjustedPayableAmount <= 0m)
            await DualScreenApplication.TryCloseBillAsync(
                billClosure, orderSettlement, billId, customerDisplayHub, loggerFactory, terminalId, cancellationToken);
        else
            await DualScreenApplication.NotifyCustomerDisplayAsync(
                customerDisplayHub, loggerFactory, terminalId, billId, "PaymentChanged", cancellationToken);
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
    }

    /// <summary>
    /// The cashier screen polls a waiting request by resending it, so the record's id is derived from the grant and the
    /// outcome and its time is the grant's own (the audit table's key is id plus time): a repeat insert hits the primary
    /// key and is dropped, which also holds for concurrent polls and for a first write that was cut short.
    /// </summary>
    private static async Task AppendGrantOutcomeAuditOnceAsync(
        IAuditEventStore auditEvents, string outcome, bool complimentary, Guid billId, Guid actorId,
        ApplyBillDiscountRequestV1 request, decimal amount, AuthorizationGrant grant, string correlationId,
        CancellationToken cancellationToken)
    {
        var eventName = $"{(complimentary ? "bill.comp" : "bill.discount")}.{outcome}";
        var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{grant.GrantId:N}:{eventName}"))[..16]);
        try
        {
            await auditEvents.AppendAsync(
                new AuditEvent(
                    id: id,
                    eventName: eventName,
                    aggregateType: "Bill",
                    aggregateId: billId,
                    actorType: "User",
                    correlationId: correlationId,
                    actorId: actorId,
                    reason: request.ReasonCode,
                    afterStateJson: JsonSerializer.Serialize(new { grantId = grant.GrantId, amount, calculationType = request.CalculationType }),
                    occurredAt: grant.ResolvedAt ?? grant.RequestedAt),
                cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation && ex.SchemaName == "audit")
        {
            // An earlier poll of the same request already wrote this record.
        }
    }

    /// <summary>Reads count against the terminal's read budget and everything else against its write budget.</summary>
    private static RouteGroupBuilder WithTerminalRateLimits(this RouteGroupBuilder group)
    {
        ((IEndpointConventionBuilder)group).Add(builder =>
        {
            var methods = builder.Metadata.OfType<IHttpMethodMetadata>().SelectMany(metadata => metadata.HttpMethods);
            var policy = methods.Contains(HttpMethods.Get, StringComparer.OrdinalIgnoreCase) ? "terminal-read" : "terminal-write";
            builder.Metadata.Add(new EnableRateLimitingAttribute(policy));
        });
        return group;
    }

    public static RouteGroupBuilder MapBillingSplitApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var billsGroup = endpoints.MapGroup(BillsRoutePrefix)
            .WithTags("Billing")
            .AddEndpointFilter<BillingSplitExceptionFilter>()
            .AddEndpointFilter<BillTerminalAccessFilter>()
            .WithTerminalRateLimits();

        billsGroup.MapPost("/from-order/{orderId:guid}", async (
            Guid terminalId,
            Guid orderId,
            IBillingSplitSessionAuthorizer authorizer,
            BillTerminalAccess access,
            BillingSplitStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, cancellationToken);
            var design = await store.CreateBillFromOrderAsync(orderId, principal.CanMutate, cancellationToken);
            await access.RequireAsync(context, terminalId, design.BillId, cancellationToken);
            return Results.Ok(design);
        });

        billsGroup.MapPost("/merge", async (
            Guid terminalId,
            MergeOrdersRequestV1 request,
            IBillingSplitSessionAuthorizer authorizer,
            BillTerminalAccess access,
            BillingSplitStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, cancellationToken);
            var design = await store.CreateMergedBillAsync(terminalId, request.OrderIds, request.IdempotencyKey, principal.CanMutate, cancellationToken);
            await access.RequireAsync(context, terminalId, design.BillId, cancellationToken);
            return Results.Ok(design);
        });

        // bills.cancel is checked directly (cashier tier and up); the reason is mandatory.
        billsGroup.MapPost("/{billId:guid}/cancel", async (
            Guid terminalId,
            Guid billId,
            CancelBillRequestV1 request,
            IBillingSplitSessionAuthorizer authorizer,
            IAuthorizationService authorization,
            BillCancellationStore store,
            ICashierQueueAnnouncer queueAnnouncer,
            IHubContext<CustomerDisplayHub> customerDisplayHub,
            ILoggerFactory loggerFactory,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            await authorization.AuthorizeAsync(principal.UserId, ApplicationPermissions.BillsCancel, cancellationToken);
            var result = await store.CancelAsync(
                billId, request.Reason, request.CancelOrder, principal.UserId, context.TraceIdentifier, cancellationToken);
            foreach (var cancelledOrderId in result.CancelledOrderIds ?? [])
                await queueAnnouncer.AnnouncePendingChecksChangedAsync("Cancelled", cancelledOrderId, null, cancellationToken);
            await DualScreenApplication.NotifyCustomerDisplayAsync(
                customerDisplayHub, loggerFactory, terminalId, billId, "BillCancelled", cancellationToken);
            return Results.Ok(result);
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
            IBillClosureService billClosure,
            OrderSettlementService orderSettlement,
            IHubContext<CustomerDisplayHub> customerDisplayHub,
            ILoggerFactory loggerFactory,
            NpgsqlDataSource dataSource,
            IBillAdjustmentTotalCap totalCap,
            HttpContext context,
            CancellationToken cancellationToken) =>
            await ApplyDiscountOrCompAsync(
                ApplicationPermissions.BillsDiscount, false, terminalId, billId, request, authorizer, store, roles, grants,
                auditEvents, billClosure, orderSettlement, customerDisplayHub, loggerFactory, dataSource, totalCap, context,
                cancellationToken));

        // Kasada ikram: same grant engine as the discount, but under bills.comp - a cashier holds it only within the
        // limit a manager set for that role, anything above waits for a manager decision.
        billsGroup.MapPost("/{billId:guid}/comp", async (
            Guid terminalId,
            Guid billId,
            ApplyBillCompRequestV1 request,
            IBillingSplitSessionAuthorizer authorizer,
            BillingSplitStore store,
            IRoleRepository roles,
            IAuthorizationGrantService grants,
            IAuditEventStore auditEvents,
            IBillClosureService billClosure,
            OrderSettlementService orderSettlement,
            IHubContext<CustomerDisplayHub> customerDisplayHub,
            ILoggerFactory loggerFactory,
            NpgsqlDataSource dataSource,
            IBillAdjustmentTotalCap totalCap,
            HttpContext context,
            CancellationToken cancellationToken) =>
            await ApplyDiscountOrCompAsync(
                ApplicationPermissions.BillsComp, true, terminalId, billId,
                new ApplyBillDiscountRequestV1(request.IdempotencyKey, "FixedAmount", request.Amount, request.ReasonCode, request.Notes),
                authorizer, store, roles, grants, auditEvents, billClosure, orderSettlement, customerDisplayHub,
                loggerFactory, dataSource, totalCap, context, cancellationToken));

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
            IAuditEventStore auditEvents,
            IHubContext<CustomerDisplayHub> customerDisplayHub,
            ILoggerFactory loggerFactory,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, cancellationToken);
            var (adjustment, summary) = await store.ApplyTipAsync(
                billId, request, principal.UserId, auditEvents, context.TraceIdentifier, cancellationToken);
            await DualScreenApplication.NotifyCustomerDisplayAsync(
                customerDisplayHub, loggerFactory, terminalId, billId, "PaymentChanged", cancellationToken);
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

        // Takes a wrongly entered discount or tip back off. Needs no tender-style approval: it only returns the
        // payable toward its original value, so the same bills.split check as entering a tip applies. No idempotency
        // key: a repeat finds nothing left to remove and answers 404, so it can never apply twice.
        billsGroup.MapPost("/{billId:guid}/adjustments/{adjustmentId:guid}/undo", async (
            Guid terminalId,
            Guid billId,
            Guid adjustmentId,
            IBillingSplitSessionAuthorizer authorizer,
            BillingSplitStore store,
            IAuditEventStore auditEvents,
            IHubContext<CustomerDisplayHub> customerDisplayHub,
            ILoggerFactory loggerFactory,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, cancellationToken);
            var summary = await store.UndoAdjustmentAsync(
                billId, adjustmentId, principal.UserId, auditEvents, context.TraceIdentifier, cancellationToken);
            await DualScreenApplication.NotifyCustomerDisplayAsync(
                customerDisplayHub, loggerFactory, terminalId, billId, "PaymentChanged", cancellationToken);
            return Results.Ok(new UndoBillAdjustmentResultV1(
                billId,
                adjustmentId,
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

        var refundsGroup = endpoints.MapGroup("/api/v1/terminals/{terminalId:guid}/billing")
            .WithTags("BillingRefunds")
            .AddEndpointFilter<BillingSplitExceptionFilter>()
            .AddEndpointFilter<BillTerminalAccessFilter>()
            .WithTerminalRateLimits();

        refundsGroup.MapPost("/bills/{billId:guid}/refund-requests", async (
            Guid terminalId,
            Guid billId,
            RefundRequestInputV1 request,
            [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey,
            IBillingSplitSessionAuthorizer authorizer,
            IAuthorizationService authorization,
            PaymentRefundStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            await authorization.AuthorizeAsync(principal.UserId, ApplicationPermissions.PaymentsRefundRequest, cancellationToken);
            return Results.Ok(await store.RequestAsync(
                billId, request.PaymentId, request.Reason, principal.UserId, terminalId, request.Items, idempotencyKey,
                cancellationToken));
        });

        refundsGroup.MapGet("/refund-requests", async (
            Guid terminalId,
            string? status,
            Guid? billId,
            DateTimeOffset? before,
            bool? selfApproved,
            int? limit,
            IBillingSplitSessionAuthorizer authorizer,
            IAuthorizationService authorization,
            ISettingsService settings,
            BillTerminalAccess access,
            PaymentRefundStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            await authorization.AuthorizeAsync(principal.UserId, ApplicationPermissions.PaymentsRefundRequest, cancellationToken);
            if (billId is { } listedBill)
                await access.RequireAsync(context, terminalId, listedBill, cancellationToken);
            var requests = await store.ListAsync(
                status, billId, before, selfApproved, limit ?? PostgresRefundRequestRepository.MaxListLimit, cancellationToken);
            if (billId is null && !await access.SeesAllBillsAsync(principal.UserId, cancellationToken))
            {
                var visible = await access.VisibleBillIdsAsync(requests.Select(request => request.BillId), terminalId, cancellationToken);
                requests = requests.Where(request => visible.Contains(request.BillId)).ToList();
            }

            var selfApprovalAllowed = await ManagerSelfApprovalSetting.IsEnabledAsync(settings, cancellationToken);
            return Results.Ok(requests.Select(request => request with { MayDecide = selfApprovalAllowed || request.RequestedBy != principal.UserId }));
        });

        refundsGroup.MapPost("/refund-requests/{requestId:guid}/approve", async (
            Guid terminalId,
            Guid requestId,
            IBillingSplitSessionAuthorizer authorizer,
            IAuthorizationService authorization,
            ISettingsService settings,
            PaymentRefundStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            await authorization.AuthorizeAsync(principal.UserId, ApplicationPermissions.PaymentsRefundApprove, cancellationToken);
            return Results.Ok(await store.ApproveAsync(
                requestId, principal.UserId, await ManagerSelfApprovalSetting.IsEnabledAsync(settings, cancellationToken),
                cancellationToken));
        });

        refundsGroup.MapPost("/refund-requests/{requestId:guid}/reject", async (
            Guid terminalId,
            Guid requestId,
            RefundDecisionInputV1 request,
            IBillingSplitSessionAuthorizer authorizer,
            IAuthorizationService authorization,
            ISettingsService settings,
            PaymentRefundStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            await authorization.AuthorizeAsync(principal.UserId, ApplicationPermissions.PaymentsRefundApprove, cancellationToken);
            return Results.Ok(await store.RejectAsync(
                requestId, principal.UserId, request.Note,
                await ManagerSelfApprovalSetting.IsEnabledAsync(settings, cancellationToken), cancellationToken));
        });

        refundsGroup.MapPost("/refund-requests/{requestId:guid}/complete", async (
            Guid terminalId,
            Guid requestId,
            RefundCompleteInputV1 request,
            IBillingSplitSessionAuthorizer authorizer,
            IAuthorizationService authorization,
            BillTerminalAccess access,
            PaymentRefundStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            await authorization.AuthorizeAsync(principal.UserId, ApplicationPermissions.PaymentsRefundRequest, cancellationToken);
            var billId = await access.RequireForRefundRequestAsync(context, terminalId, requestId, cancellationToken);
            var completed = await store.CompleteManualAsync(
                requestId, request.SlipNumber, request.Note, principal.UserId, cancellationToken);
            if (billId is { } owned)
                await access.ClaimAsync(context, terminalId, owned);
            return Results.Ok(completed);
        });

        refundsGroup.MapGet("/refund-allowance", async (
            Guid terminalId,
            IBillingSplitSessionAuthorizer authorizer,
            IAuthorizationService authorization,
            PaymentRefundStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            await authorization.AuthorizeAsync(principal.UserId, ApplicationPermissions.PaymentsRefundRequest, cancellationToken);
            return Results.Ok(await store.GetAllowanceAsync(principal.UserId, terminalId, cancellationToken));
        });

        refundsGroup.MapPut("/refund-allowance", async (
            Guid terminalId,
            RefundAllowanceInputV1 request,
            IBillingSplitSessionAuthorizer authorizer,
            IAuthorizationService authorization,
            ISettingsService settings,
            PaymentRefundStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            await authorization.AuthorizeAsync(principal.UserId, ApplicationPermissions.PaymentsRefundApprove, cancellationToken);
            return Results.Ok(await store.SetAllowanceAsync(
                request.CashierUserId, request.CashierTerminalId, request.Amount, principal.UserId,
                await ManagerSelfApprovalSetting.IsEnabledAsync(settings, cancellationToken), cancellationToken));
        });

        var group = endpoints.MapGroup(RoutePrefix)
            .WithTags("BillingSplitDesign")
            .AddEndpointFilter<BillingSplitExceptionFilter>()
            .AddEndpointFilter<BillTerminalAccessFilter>()
            .WithTerminalRateLimits();

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
        BillingSplitNotFoundException notFound => (404, "NOT_FOUND", $"İstenen {notFound.Resource} bulunamadı."),
        KeyNotFoundException => (404, "NOT_FOUND", "İstenen hesap bulunamadı."),
        GrantDeniedException { Complimentary: false } => (403, "GRANT_DENIED", "İndirim isteği reddedildi."),
        GrantDeniedException => (403, "GRANT_DENIED", "İkram isteği reddedildi."),
        BillOtherTerminalException => (403, "BILL_OTHER_TERMINAL", BillOtherTerminalException.UserMessage),
        UnsupportedOrderStateException => (409, "UNSUPPORTED_ORDER_STATE", "Bu sipariş durumunda hesap açılamaz."),
        BillMergeConflictException conflict => (409, conflict.Code, conflict.UserMessage),
        BillingValidationException validation => (400, validation.Code, validation.UserMessage),
        SplitDesignConcurrencyException => (409, "CONCURRENT_MODIFICATION", "Hesap bölme tasarımı başka bir işlem tarafından değiştirildi."),
        SplitDesignUnsupportedBillStateException => (409, "UNSUPPORTED_BILL_STATE", "Bu hesap durumunda bölme tasarımı değiştirilemez."),
        BillDiscountUnsupportedBillStateException => (409, "UNSUPPORTED_BILL_STATE", "Bu hesap durumunda indirim ya da bahşiş uygulanamaz."),
        BillDiscountAfterRefundException => (409, "DISCOUNT_AFTER_REFUND", "Bu hesapta kalem iadesi yapılmış; iadeden sonra indirim ya da ikram uygulanamaz."),
        BillDiscountBelowCollectedException => (409, "DISCOUNT_BELOW_COLLECTED", "Bu indirim, hesaptan şimdiye kadar alınan tutarın altına iniyor; uygulanamaz."),
        AdjustmentUndoBillStateException => (409, "UNSUPPORTED_BILL_STATE", "Bu hesap durumunda indirim ya da bahşiş geri alınamaz."),
        AdjustmentUndoBelowCollectedException => (409, "ADJUSTMENT_UNDO_BELOW_COLLECTED", "Geri alınırsa ödenecek tutar, hesaptan şimdiye kadar alınan tutarın altına iniyor; geri alınamaz."),
        SplitItemsOnAdjustedBillException => (409, "ITEM_SPLIT_ON_ADJUSTED_BILL", "Ek ücret veya bahşiş eklenmiş hesap ürün bazında bölünemez; tutar, kişi ya da serbest bölme kullanın."),
        BillCancelHasPaymentException => (409, "BILL_HAS_PAYMENT", "Bu hesapta ödeme var; iptal etmeden önce ödeme iade edilmeli."),
        BillCancelNotAllowedException => (409, "UNSUPPORTED_BILL_STATE", "Bu hesap zaten iptal edilmiş."),
        RefundInputException refundInput => (
            400, refundInput.Code, refundInput.UserMessage ?? "İade bilgisi geçersiz."),
        RefundPaymentNotRefundableException => (409, "PAYMENT_NOT_REFUNDABLE", "Bu ödeme iade edilemez; yalnızca onaylanmış ve iade edilmemiş ödeme iade istenebilir."),
        RefundAlreadyRequestedException => (409, "REFUND_ALREADY_REQUESTED", "Bu ödeme için açık bir iade isteği zaten var."),
        RefundStateException => (409, "REFUND_STATE", "İade isteği bu durumda değil; sayfayı yenileyin."),
        RefundSelfDecisionException => (403, "SELF_DECISION", "İadeyi isteyen kişi kendi isteği hakkında karar veremez; başka bir yetkili onaylamalı veya reddetmeli. Tek yöneticili işletmelerde Ayarlar'dan \"Yönetici kendi talebini onaylayabilir\" açılabilir."),
        RefundNoOpenCashSessionException => (409, "NO_OPEN_CASH_SESSION", "İadeyi isteyen kasiyerin, isteği yaptığı terminalde açık kasa oturumu yok."),
        ALKAROS.Cash.TenderHandler.ClosedCashSessionException => (409, "NO_OPEN_CASH_SESSION", "İadeyi isteyen kasiyerin, isteği yaptığı terminalde açık kasa oturumu yok."),
        RefundItemQuantityException => (409, "REFUND_ITEM_QUANTITY", "Bu kalemden istenen adet iade edilebilir adedi aşıyor."),
        RefundAmountExceedsPaymentException => (409, "REFUND_AMOUNT_EXCEEDS_PAYMENT", "İade tutarı seçilen ödemenin kalan tutarını aşıyor."),
        RefundAccountChargeUnsupportedException => (409, "ACCOUNT_REFUND_UNSUPPORTED", "Müşteri hesabına yazılan ödeme buradan iade edilemez."),
        OrderCancelNotAllowedException => (409, "ORDER_CANCEL_NOT_ALLOWED", OrderCancellationMessages.NotAllowed),
        OrderCancelHasPaymentException => (409, "ORDER_HAS_PAYMENT", OrderCancellationMessages.HasPayment),
        OrderCancelHasServedItemsException => (409, "ORDER_HAS_SERVED_ITEMS", OrderCancellationMessages.HasServedItems),
        VoidStockRestoreFailedException => (409, "STOCK_RESTORE_FAILED", "Ürünün stoğu geri verilemedi; iptal yapılmadı."),
        ALKAROS.Orders.SubmitOrder.OrderNotFoundException => (404, "NOT_FOUND", "İstenen sipariş bulunamadı."),
        BillCancelReasonException => (400, "REASON_REQUIRED", "İptal gerekçesi 1 ile 500 karakter arasında olmalı."),
        IdempotencyKeyReusedException => (409, "IDEMPOTENCY_KEY_REUSED", "Bu işlem anahtarı farklı bir istek için zaten kullanılmış."),
        OperationCanceledException => (499, "REQUEST_CANCELLED", "İstek iptal edildi."),
        GarsonFeatureDisabledException => (403, "FEATURE_DISABLED", "Bu özellik bu işletme için kapatılmış."),
        DiscountExceedsPayableException => (400, "DISCOUNT_EXCEEDS_PAYABLE", "İndirim toplamı hesap tutarını aşıyor."),
        BillConcurrencyException or OrderConcurrencyException or PaymentConcurrencyException => ApiErrorCatalog.RowVersionConflict,
        ArgumentException or BadHttpRequestException => ApiErrorCatalog.ValidationAnswer(exception),
        InvalidOperationException => (400, "VALIDATION_FAILED", "İstek hesabın şu anki durumuyla uyumlu değil."),
        PostgresException postgres when postgres.SqlState == PostgresErrorCodes.SerializationFailure =>
            (409, "CONCURRENT_MODIFICATION", "Hesap bölme tasarımı başka bir işlem tarafından değiştirildi."),
        PostgresException postgres when postgres.SqlState is PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.LockNotAvailable =>
            (503, "BILL_BUSY", "Hesap şu anda başka bir işlem tarafından kullanılıyor; birkaç saniye sonra tekrar deneyin."),
        PostgresException or NpgsqlException => (503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
        _ => (500, "INTERNAL_ERROR", "İşlem tamamlanamadı."),
    };
}

public sealed class BillingSplitUnauthorizedException : Exception
{
    public BillingSplitUnauthorizedException(string message) : base(message) { }
}
