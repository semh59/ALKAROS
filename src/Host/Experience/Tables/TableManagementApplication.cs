using ALKAROS.Host.DualScreen;
using ALKAROS.Identity.Authorization;
using ALKAROS.Identity.Authorization.Catalog;
using ALKAROS.Tables.CurrentPointers;
using ALKAROS.Tables.FloorPlan;
using ALKAROS.Tables.Reservations;
using ALKAROS.Tables.TableLifecycle;
using ALKAROS.Tables.TableMerge;
using ALKAROS.Tables.TableTransfer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ALKAROS.Host.Experience.Tables;

public static class TableManagementApplication
{
    public const string RoutePrefix = "/api/v1/terminals/{terminalId:guid}/table-management";

    public static IServiceCollection AddTableManagementExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<DualScreenStore>();
        services.TryAddSingleton<IRoleRepository, PostgresRoleRepository>();
        services.TryAddSingleton<IDenialEventSink, PostgresDenialEventSink>();
        services.TryAddSingleton<IAuthorizationService, AuthorizationService>();
        services.TryAddSingleton<IZoneRepository, PostgresZoneRepository>();
        services.TryAddSingleton<ITableRepository, PostgresTableRepository>();
        services.TryAddSingleton<ITableFloorPlanRepository, PostgresTableFloorPlanRepository>();
        services.TryAddSingleton<ITableTransferRepository, PostgresTableTransferRepository>();
        services.TryAddSingleton<ITableTransferService, TableTransferService>();
        services.TryAddSingleton<ITableMergeRepository, PostgresTableMergeRepository>();
        services.TryAddSingleton<ITableMergeService, TableMergeService>();
        services.TryAddSingleton<ITableReservationRepository, PostgresTableReservationRepository>();
        services.TryAddSingleton<ITableReservationService, TableReservationService>();
        services.TryAddSingleton<ITablePointerProjector, PostgresTablePointerProjector>();

        services.TryAddSingleton<ZoneConcurrencyStore>();
        services.TryAddSingleton<TableManagementStore>();
        services.TryAddSingleton<ITableManagementSessionAuthorizer, TableManagementSessionAuthorizer>();
        services.TryAddTransient<TableManagementExceptionFilter>();
        return services;
    }

    public static RouteGroupBuilder MapTableManagementApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // V1-RMD-163: found by the 2026-09-10 Garson audit — this whole
        // group had no rate limiting at all, unlike DualScreenApplication's
        // own endpoints and Orders (both of which reuse these exact same
        // globally-registered "terminal-read"/"terminal-write" policies,
        // AddRateLimiter is called once for the whole app). Group default
        // is the write bucket, since most of this group's endpoints
        // mutate; each read-only GET below overrides it individually.
        var group = endpoints.MapGroup(RoutePrefix)
            .WithTags("TableManagement")
            .RequireRateLimiting("terminal-write")
            .AddEndpointFilter<TableManagementExceptionFilter>();

        group.MapGet("/zones", async (
            Guid terminalId,
            ITableManagementSessionAuthorizer authorizer,
            ZoneConcurrencyStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            return Results.Ok(await store.GetAllAsync(cancellationToken));
        }).RequireRateLimiting("terminal-read");

        group.MapPost("/zones", async (
            Guid terminalId,
            CreateZoneRequest request,
            ITableManagementSessionAuthorizer authorizer,
            ZoneConcurrencyStore store,
            IZoneRepository repository,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequireMutationAsync(context, terminalId, ApplicationPermissions.FloorplanManage, cancellationToken);
            var created = await store.CreateAsync(request, repository, cancellationToken);
            return Results.Created($"{Prefix(terminalId)}/zones/{created.ZoneId:D}", created);
        });

        group.MapPut("/zones/{zoneId:guid}", async (
            Guid terminalId,
            Guid zoneId,
            UpdateZoneRequest request,
            ITableManagementSessionAuthorizer authorizer,
            ZoneConcurrencyStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequireMutationAsync(context, terminalId, ApplicationPermissions.FloorplanManage, cancellationToken);
            return Results.Ok(await store.UpdateAsync(zoneId, request, cancellationToken));
        });

        group.MapDelete("/zones/{zoneId:guid}", async (
            Guid terminalId,
            Guid zoneId,
            long? expectedRowVersion,
            ITableManagementSessionAuthorizer authorizer,
            ZoneConcurrencyStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequireMutationAsync(context, terminalId, ApplicationPermissions.FloorplanManage, cancellationToken);
            await store.DeleteAsync(zoneId, expectedRowVersion, cancellationToken);
            return Results.NoContent();
        });

        group.MapGet("/tables", async (
            Guid terminalId,
            Guid? zoneId,
            ITableManagementSessionAuthorizer authorizer,
            TableManagementStore store,
            ITableReservationRepository reservations,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            var tables = await store.GetAllAsync(zoneId, cancellationToken);
            var dtos = new List<TableDto>(tables.Count);
            foreach (var (table, currentOrderTotal, currentOrderOpenedAt, statusChangedAt) in tables)
            {
                var activeReservation = await ActiveReservationOrNullAsync(table, reservations, cancellationToken);
                dtos.Add(TableContractMapper.ToDto(table, principal.Permissions, activeReservation, currentOrderTotal, currentOrderOpenedAt, statusChangedAt));
            }
            return Results.Ok(dtos);
        }).RequireRateLimiting("terminal-read");

        group.MapGet("/tables/{tableId:guid}", async (
            Guid terminalId,
            Guid tableId,
            ITableManagementSessionAuthorizer authorizer,
            TableManagementStore store,
            ITableReservationRepository reservations,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            var (table, currentOrderTotal, currentOrderOpenedAt, statusChangedAt) = await store.GetAsync(tableId, cancellationToken)
                ?? throw new TableManagementNotFoundException($"Table {tableId} was not found.");
            var activeReservation = await ActiveReservationOrNullAsync(table, reservations, cancellationToken);
            return Results.Ok(TableContractMapper.ToDto(table, principal.Permissions, activeReservation, currentOrderTotal, currentOrderOpenedAt, statusChangedAt));
        }).RequireRateLimiting("terminal-read");

        group.MapPost("/tables", async (
            Guid terminalId,
            CreateTableRequest request,
            ITableManagementSessionAuthorizer authorizer,
            TableManagementStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, ApplicationPermissions.FloorplanManage, cancellationToken);
            var created = await store.CreateAsync(request, cancellationToken);
            var dto = await ToDtoWithStatusChangedAtAsync(store, created, principal.Permissions, activeReservation: null, cancellationToken);
            return Results.Created($"{Prefix(terminalId)}/tables/{created.Id:D}", dto);
        });

        group.MapPut("/tables/{tableId:guid}", async (
            Guid terminalId,
            Guid tableId,
            UpdateTableRequest request,
            ITableManagementSessionAuthorizer authorizer,
            TableManagementStore store,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, ApplicationPermissions.FloorplanManage, cancellationToken);
            var updated = await store.UpdateAsync(tableId, request, cancellationToken);
            return Results.Ok(await ToDtoWithStatusChangedAtAsync(store, updated, principal.Permissions, activeReservation: null, cancellationToken));
        });

        group.MapPost("/tables/{tableId:guid}/status", async (
            Guid terminalId,
            Guid tableId,
            ChangeTableStatusRequest request,
            ITableManagementSessionAuthorizer authorizer,
            TableManagementStore store,
            ITableReservationRepository reservations,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, ApplicationPermissions.TablesStatus, cancellationToken);
            var updated = await store.ChangeStatusAsync(tableId, request, principal.UserId, cancellationToken);
            var activeReservation = await ActiveReservationOrNullAsync(updated, reservations, cancellationToken);
            return Results.Ok(await ToDtoWithStatusChangedAtAsync(store, updated, principal.Permissions, activeReservation, cancellationToken));
        });

        group.MapGet("/tables/{tableId:guid}/current-pointer", async (
            Guid terminalId,
            Guid tableId,
            ITableManagementSessionAuthorizer authorizer,
            ITablePointerProjector projector,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            var pointer = await projector.DetectTableDriftAsync(tableId, cancellationToken)
                ?? throw new TableManagementNotFoundException($"Table {tableId} was not found.");
            return Results.Ok(TablePointerDto.From(pointer));
        }).RequireRateLimiting("terminal-read");

        group.MapGet("/floor-plans/{zoneId:guid}", async (
            Guid terminalId,
            Guid zoneId,
            ITableManagementSessionAuthorizer authorizer,
            ITableFloorPlanRepository repository,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            var floorPlan = await repository.GetAsync(zoneId, cancellationToken)
                ?? throw new FloorPlanNotFoundException($"Floor plan for zone {zoneId} was not found.");
            return Results.Ok(TableContractMapper.ToDto(floorPlan, principal.Permissions));
        }).RequireRateLimiting("terminal-read");

        group.MapPut("/floor-plans/{zoneId:guid}", async (
            Guid terminalId,
            Guid zoneId,
            SaveFloorPlanRequest request,
            ITableManagementSessionAuthorizer authorizer,
            ITableFloorPlanRepository repository,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, ApplicationPermissions.FloorplanManage, cancellationToken);
            var result = await repository.SaveAsync(
                TableContractMapper.ToCommand(zoneId, request),
                cancellationToken);
            return Results.Ok(TableContractMapper.ToDto(result, principal.Permissions));
        });

        group.MapGet("/reservations/{reservationId:guid}", async (
            Guid terminalId,
            Guid reservationId,
            ITableManagementSessionAuthorizer authorizer,
            ITableReservationService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            await authorizer.RequireReadAsync(context, terminalId, cancellationToken);
            var record = await service.GetByIdAsync(reservationId, cancellationToken);
            return Results.Ok(TableReservationDto.From(record));
        }).RequireRateLimiting("terminal-read");

        group.MapPost("/reservations", async (
            Guid terminalId,
            CreateTableReservationRequest request,
            ITableManagementSessionAuthorizer authorizer,
            ITableReservationService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, ApplicationPermissions.TablesReserve, cancellationToken);
            var result = await service.CreateReservationAsync(new CreateReservationRequest(
                request.TableId,
                TableContractMapper.RequiredVersion(request.ExpectedTableRowVersion, nameof(request.ExpectedTableRowVersion)),
                request.OrderId,
                principal.UserId,
                TableReservationActorType.User,
                request.Reason,
                request.PartySize,
                request.ReservedAt,
                request.ExpiresAt), cancellationToken);
            return Results.Created($"{Prefix(terminalId)}/reservations/{result.ReservationId:D}", result);
        });

        group.MapPost("/reservations/{reservationId:guid}/claim", async (
            Guid terminalId,
            Guid reservationId,
            ClaimTableReservationRequest request,
            ITableManagementSessionAuthorizer authorizer,
            ITableReservationService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, ApplicationPermissions.TablesReserve, cancellationToken);
            return Results.Ok(await service.ClaimReservationAsync(new ClaimReservationRequest(
                reservationId,
                TableContractMapper.RequiredVersion(request.ExpectedReservationRowVersion, nameof(request.ExpectedReservationRowVersion)),
                TableContractMapper.RequiredVersion(request.ExpectedTableRowVersion, nameof(request.ExpectedTableRowVersion)),
                request.OrderId,
                principal.UserId,
                request.ClaimedAt), cancellationToken));
        });

        group.MapPost("/reservations/{reservationId:guid}/cancel", async (
            Guid terminalId,
            Guid reservationId,
            CancelTableReservationRequest request,
            ITableManagementSessionAuthorizer authorizer,
            ITableReservationService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, ApplicationPermissions.TablesReserve, cancellationToken);
            return Results.Ok(await service.CancelReservationAsync(new CancelReservationRequest(
                reservationId,
                TableContractMapper.RequiredVersion(request.ExpectedReservationRowVersion, nameof(request.ExpectedReservationRowVersion)),
                TableContractMapper.RequiredVersion(request.ExpectedTableRowVersion, nameof(request.ExpectedTableRowVersion)),
                principal.UserId,
                request.Reason,
                request.CancelledAt), cancellationToken));
        });

        group.MapPost("/reservations/{reservationId:guid}/expire", async (
            Guid terminalId,
            Guid reservationId,
            ExpireTableReservationRequest request,
            ITableManagementSessionAuthorizer authorizer,
            ITableReservationService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, ApplicationPermissions.TablesReserve, cancellationToken);
            return Results.Ok(await service.ExpireReservationAsync(new ExpireReservationRequest(
                reservationId,
                TableContractMapper.RequiredVersion(request.ExpectedReservationRowVersion, nameof(request.ExpectedReservationRowVersion)),
                TableContractMapper.RequiredVersion(request.ExpectedTableRowVersion, nameof(request.ExpectedTableRowVersion)),
                principal.UserId,
                request.Reason,
                request.ExpiredAt), cancellationToken));
        });

        group.MapPost("/transfers", async (
            Guid terminalId,
            TransferTableRequest request,
            ITableManagementSessionAuthorizer authorizer,
            ITableTransferService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, ApplicationPermissions.TablesTransfer, cancellationToken);
            return Results.Ok(await service.TransferTableAsync(new TableTransferRequest(
                request.SourceTableId,
                TableContractMapper.RequiredVersion(request.ExpectedSourceRowVersion, nameof(request.ExpectedSourceRowVersion)),
                request.TargetTableId,
                TableContractMapper.RequiredVersion(request.ExpectedTargetRowVersion, nameof(request.ExpectedTargetRowVersion)),
                request.Reason,
                principal.UserId,
                request.TransferredAt), cancellationToken));
        });

        group.MapPost("/merges", async (
            Guid terminalId,
            MergeTablesRequest request,
            ITableManagementSessionAuthorizer authorizer,
            ITableMergeService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, ApplicationPermissions.TablesMerge, cancellationToken);
            return Results.Ok(await service.MergeTablesAsync(new TableMergeRequest(
                request.PrimaryTableId,
                TableContractMapper.RequiredVersion(request.ExpectedPrimaryRowVersion, nameof(request.ExpectedPrimaryRowVersion)),
                TableContractMapper.ToParticipants(request.Participants, nameof(request.Participants)),
                request.Reason,
                principal.UserId,
                request.MergedAt), cancellationToken));
        });

        group.MapPost("/merges/{mergeGroupId:guid}/unmerge", async (
            Guid terminalId,
            Guid mergeGroupId,
            UnmergeTablesRequest request,
            ITableManagementSessionAuthorizer authorizer,
            ITableMergeService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            var principal = await authorizer.RequireMutationAsync(context, terminalId, ApplicationPermissions.TablesMerge, cancellationToken);
            return Results.Ok(await service.UnmergeTablesAsync(new TableUnmergeRequest(
                mergeGroupId,
                TableContractMapper.RequiredVersion(request.ExpectedPrimaryRowVersion, nameof(request.ExpectedPrimaryRowVersion)),
                TableContractMapper.ToParticipants(request.Participants, nameof(request.Participants)),
                request.Reason,
                principal.UserId,
                request.UnmergedAt), cancellationToken));
        });

        return group;
    }

    private static string Prefix(Guid terminalId)
        => $"/api/v1/terminals/{terminalId:D}/table-management";

    // V1-RMD-118: only a Reserved table can have an Active reservation row
    // (the invariant V1-RMD-117 enforces), so this skips a lookup for every
    // other status instead of querying unconditionally per table.
    private static Task<TableReservationRecord?> ActiveReservationOrNullAsync(
        Table table, ITableReservationRepository reservations, CancellationToken cancellationToken)
        => table.State == TableState.Reserved
            ? reservations.GetActiveByTableIdAsync(table.Id, cancellationToken)
            : Task.FromResult<TableReservationRecord?>(null);

    // V1-TBL-009: Create/Update/ChangeStatus all hand back a bare Table from
    // their own store method (no CurrentOrderTotal/CurrentOrderOpenedAt/
    // StatusChangedAt — those only exist on TableManagementStore.GetAsync's
    // read-model tuple, V1-RMD-135/V1-WTR-019/this task respectively). A
    // status change is exactly the moment StatusChangedAt needs to be
    // right, so this re-reads the row once more rather than serving a
    // default(DateTimeOffset) in the mutation's own response — found by
    // this task's own HTTP test, which failed loudly on exactly that.
    private static async Task<TableDto> ToDtoWithStatusChangedAtAsync(
        TableManagementStore store,
        Table table,
        IReadOnlySet<string> permissions,
        TableReservationRecord? activeReservation,
        CancellationToken cancellationToken)
    {
        var read = await store.GetAsync(table.Id, cancellationToken)
            ?? throw new TableManagementNotFoundException($"Table {table.Id} was not found after its own mutation.");
        return TableContractMapper.ToDto(
            read.Table, permissions, activeReservation, read.CurrentOrderTotal, read.CurrentOrderOpenedAt, read.StatusChangedAt);
    }
}

internal interface ITableManagementSessionAuthorizer
{
    Task<TableManagementPrincipal> RequireReadAsync(
        HttpContext context,
        Guid terminalId,
        CancellationToken cancellationToken);

    Task<TableManagementPrincipal> RequireMutationAsync(
        HttpContext context,
        Guid terminalId,
        string permissionCode,
        CancellationToken cancellationToken);
}

internal sealed class TableManagementSessionAuthorizer : ITableManagementSessionAuthorizer
{
    private readonly DualScreenStore _sessions;
    private readonly IRoleRepository _roles;
    private readonly IAuthorizationService _authorization;

    public TableManagementSessionAuthorizer(
        DualScreenStore sessions,
        IRoleRepository roles,
        IAuthorizationService authorization)
    {
        _sessions = sessions;
        _roles = roles;
        _authorization = authorization;
    }

    public async Task<TableManagementPrincipal> RequireReadAsync(
        HttpContext context,
        Guid terminalId,
        CancellationToken cancellationToken)
    {
        if (terminalId == Guid.Empty)
            throw new ArgumentException("Terminal ID cannot be empty.", nameof(terminalId));

        var cashierToken = context.Request.Cookies[DualScreenApplication.CashierCookieName];
        var cashier = await _sessions.AuthenticateCashierAsync(cashierToken, terminalId, cancellationToken);
        if (cashier is null)
        {
            var displayToken = context.Request.Cookies[DualScreenApplication.DisplayCookieName];
            if (await _sessions.AuthenticateDisplayAsync(displayToken, null, cancellationToken) is not null)
                throw new TableManagementForbiddenException("Customer displays cannot access table management resources.");
            throw new TableManagementUnauthorizedException("A valid terminal-bound cashier session is required.");
        }

        var permissions = await _roles.GetPermissionCodesForUserAsync(cashier.UserId, cancellationToken);
        return new TableManagementPrincipal(
            cashier.UserId,
            permissions.ToHashSet(StringComparer.Ordinal));
    }

    public async Task<TableManagementPrincipal> RequireMutationAsync(
        HttpContext context,
        Guid terminalId,
        string permissionCode,
        CancellationToken cancellationToken)
    {
        var principal = await RequireReadAsync(context, terminalId, cancellationToken);
        await _authorization.AuthorizeAsync(principal.UserId, permissionCode, cancellationToken);
        return principal;
    }
}

internal sealed class TableManagementExceptionFilter : IEndpointFilter
{
    private static readonly Action<ILogger, string, string, Exception?> LogRequestFailure =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(5100, nameof(LogRequestFailure)),
            "Table management request failed on {Path} ({TraceIdentifier}).");

    private readonly ILogger<TableManagementExceptionFilter> _logger;

    public TableManagementExceptionFilter(ILogger<TableManagementExceptionFilter> logger)
    {
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext invocationContext,
        EndpointFilterDelegate next)
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

            return Results.Json(
                new TableManagementErrorEnvelope(new TableManagementError(
                    mapped.Code,
                    mapped.Message,
                    mapped.Status,
                    invocationContext.HttpContext.TraceIdentifier)),
                statusCode: mapped.Status);
        }
    }

    private static (int Status, string Code, string Message) Map(Exception exception) => exception switch
    {
        TableManagementUnauthorizedException => (401, "UNAUTHORIZED", "Oturum geçersiz veya süresi dolmuş."),
        TableManagementForbiddenException or AuthorizationDeniedException =>
            (403, "FORBIDDEN", "Bu işlem için yetkiniz yok."),
        TableManagementNotFoundException
            or ALKAROS.Tables.TableTransfer.TableNotFoundException
            or ALKAROS.Tables.TableMerge.TableNotFoundException
            or ALKAROS.Tables.Reservations.TableNotFoundException
            or ReservationNotFoundException
            or MergeRecordNotFoundException
            or KeyNotFoundException => (404, "NOT_FOUND", "İstenen masa kaydı bulunamadı."),
        TableManagementConcurrencyException
            or FloorPlanConcurrencyException
            or TableTransferConcurrencyException
            or TableMergeConcurrencyException
            or TableReservationConcurrencyException =>
            (409, "CONCURRENT_MODIFICATION", "Kayıt başka bir işlem tarafından değiştirildi."),
        // A payment that could not be confirmed (card attempt awaiting manual
        // reconciliation) blocks moving or merging the table. It gets its own
        // code and message: the generic conflict text below tells the manager
        // nothing, and retrying can never help until the payment is resolved.
        ALKAROS.Tables.TableTransfer.PaymentPolicyRequiredException
            or ALKAROS.Tables.TableMerge.PaymentPolicyRequiredException =>
            (409, "PAYMENT_UNSETTLED",
                "Bu masanın hesabında çözülmemiş bir ödeme var. Ödeme mutabakatı tamamlanmadan masa taşınamaz veya birleştirilemez."),
        TableManagementConflictException
            or SameTableTransferException
            or InvalidSourceTableStateException
            or InvalidTargetTableStateException
            or SameTableMergeException
            or DuplicateMergeParticipantException
            or InvalidTableMergeStateException
            or TableNotAvailableForReservationException
            or InvalidReservationStateException =>
            (409, "DOMAIN_CONFLICT", "Masa işlemi mevcut durumla çakışıyor."),
        PostgresException postgres when postgres.SqlState == PostgresErrorCodes.UniqueViolation =>
            (409, "DUPLICATE_RESOURCE", "Aynı kimlikte bir masa kaydı zaten var."),
        PostgresException postgres when postgres.SqlState == PostgresErrorCodes.ForeignKeyViolation =>
            (409, "RESOURCE_IN_USE", "Bağlı kayıtlar nedeniyle işlem tamamlanamadı."),
        ArgumentException or BadHttpRequestException => (400, "VALIDATION_FAILED", "İstek doğrulanamadı."),
        PostgresException or NpgsqlException =>
            (503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı."),
        _ => (500, "INTERNAL_ERROR", "İşlem tamamlanamadı."),
    };
}
