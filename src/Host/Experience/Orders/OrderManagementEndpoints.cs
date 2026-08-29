using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ALKAROS.Host.Experience.Orders;

public static class OrderManagementEndpoints
{
    public const string RoutePrefix = "/api/v1/terminals/{terminalId:guid}/orders";

    public static IServiceCollection AddOrderManagementExperience(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<OrderManagementStore>();
        return services;
    }

    public static RouteGroupBuilder MapOrderManagementApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup(RoutePrefix)
            .WithTags("Orders");

        group.MapPost("/table-draft", async (
            Guid terminalId,
            CreateTableDraftRequest request,
            OrderManagementStore store,
            CancellationToken cancellationToken) =>
        {
            if (request.TableId == Guid.Empty)
                return Results.BadRequest(new { error = new { code = "INVALID_TABLE", message = "TableId cannot be empty." } });

            if (request.Items == null || request.Items.Count == 0)
                return Results.BadRequest(new { error = new { code = "EMPTY_ITEMS", message = "Order items cannot be empty." } });

            var draft = await store.CreateOrUpdateTableDraftAsync(request, cancellationToken);
            return Results.Ok(draft);
        });

        group.MapGet("/table/{tableId:guid}", async (
            Guid terminalId,
            Guid tableId,
            OrderManagementStore store,
            CancellationToken cancellationToken) =>
        {
            var order = await store.GetActiveOrderByTableIdAsync(tableId, cancellationToken);
            if (order == null)
                return Results.NotFound(new { error = new { code = "ORDER_NOT_FOUND", message = "No active order for table." } });

            return Results.Ok(order);
        });

        group.MapGet("/{orderId:guid}", async (
            Guid terminalId,
            Guid orderId,
            OrderManagementStore store,
            CancellationToken cancellationToken) =>
        {
            var order = await store.GetOrderByIdAsync(orderId, cancellationToken);
            if (order == null)
                return Results.NotFound(new { error = new { code = "ORDER_NOT_FOUND", message = "Order not found." } });

            return Results.Ok(order);
        });

        group.MapPost("/{orderId:guid}/submit", async (
            Guid terminalId,
            Guid orderId,
            SubmitTableOrderRequest request,
            OrderManagementStore store,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var submitted = await store.SubmitOrderAsync(orderId, request.ExpectedRowVersion, cancellationToken);
                return Results.Ok(submitted);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = new { code = "ORDER_NOT_FOUND", message = "Order not found." } });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = new { code = "CONCURRENCY_CONFLICT", message = ex.Message } });
            }
        });

        return group;
    }
}
