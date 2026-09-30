using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ALKAROS.Host.Composition;
using ALKAROS.Host.Composition.Modules;
using ALKAROS.Host.Experience.OrderInvoices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Reconciliation.Tests;

/// <summary>
/// The online order invoice list over HTTP with the real module composition: manager session plus reports.view, the
/// drafts by service date, the delivered orders still waiting for a draft with their days left, and the range limit.
/// </summary>
[Collection("Reconciliation case PostgreSQL HTTP")]
public sealed class OrderInvoiceHttpTests : IAsyncLifetime
{
    private static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;
    private const string ListPath = "/api/v1/management/order-invoices?from=2026-09-20&to=2026-09-26";

    private readonly ReconciliationCaseTestDatabase _database = new();
    private WebApplication? _application;
    private Uri? _baseAddress;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        var composition = ModuleRegistry.ComposeRoot(ModuleRegistry.DefaultCatalog);
        HostComposition.ApplyComposedModuleServices(builder.Services, composition.Services);
        builder.Services.AddReconciliationCaseExperience();

        _application = builder.Build();
        _application.MapOrderInvoiceApi();
        await _application.StartAsync();
        var addresses = _application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        _baseAddress = new Uri(Assert.Single(addresses!.Addresses), UriKind.Absolute);
    }

    public async Task DisposeAsync()
    {
        if (_application is not null)
            await _application.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task OnlyASessionWithReportsViewReadsTheInvoices()
    {
        using var anonymous = CreateClient(null);
        using var denied = CreateClient(ReconciliationCaseTestDatabase.DeniedToken);
        using var viewOnly = CreateClient(ReconciliationCaseTestDatabase.ViewOnlyToken);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(ListPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync(ListPath)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewOnly.GetAsync(ListPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/management/order-invoices/by-order/{Guid.NewGuid():D}")).StatusCode);
    }

    [Fact]
    public async Task TheListHoldsTheDraftsOfTheRangeAndTheDeliveredOrdersStillWaitingForOne()
    {
        var inRange = await SeedDraftAsync("YS-1", "2026-09-22", 110.00m, 10.00m);
        await SeedDraftAsync("YS-OLD", "2026-08-01", 55.00m, 5.00m);
        var waiting = await SeedDeliveredOrderAsync("YS-WAIT", "2 days", 80.00m);
        var drafted = await SeedDeliveredOrderAsync("YS-DONE", "1 day", 60.00m);
        await SeedDraftForOrderAsync(drafted, "YS-DONE", "2026-09-29", 60.00m, 5.45m);
        await SeedDeliveredOrderAsync("YS-LATE", "8 days", 70.00m);
        using var client = CreateClient(ReconciliationCaseTestDatabase.ViewOnlyToken);

        var body = await (await client.GetAsync(ListPath)).Content.ReadFromJsonAsync<JsonElement>();

        var invoice = Assert.Single(body.GetProperty("invoices").EnumerateArray());
        Assert.Equal(inRange, invoice.GetProperty("orderId").GetGuid());
        Assert.Equal("Draft", invoice.GetProperty("status").GetString());
        Assert.Equal("yemeksepeti", invoice.GetProperty("provider").GetString());
        Assert.Equal("2026-09-22", invoice.GetProperty("serviceDate").GetString());
        Assert.Equal(100.00m, invoice.GetProperty("netAmount").GetDecimal());
        Assert.Equal(10.00m, invoice.GetProperty("taxAmount").GetDecimal());
        Assert.Equal(110.00m, invoice.GetProperty("grossAmount").GetDecimal());
        var missing = Assert.Single(body.GetProperty("missing").EnumerateArray());
        Assert.Equal(waiting, missing.GetProperty("orderId").GetGuid());
        Assert.Equal("YS-WAIT", missing.GetProperty("orderNumber").GetString());
        Assert.Equal(80.00m, missing.GetProperty("total").GetDecimal());
        Assert.Equal(5, missing.GetProperty("daysLeft").GetInt32());
    }

    [Fact]
    public async Task ADraftIsOpenedByOrderAndAnUnknownOrderIsANotFoundInTurkish()
    {
        var orderId = await SeedDraftAsync("YS-2", "2026-09-23", 110.00m, 10.00m);
        using var client = CreateClient(ReconciliationCaseTestDatabase.ViewOnlyToken);

        using var found = await client.GetAsync($"/api/v1/management/order-invoices/by-order/{orderId:D}");
        using var none = await client.GetAsync($"/api/v1/management/order-invoices/by-order/{Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        var draft = await found.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Deniz Lokantası", draft.GetProperty("seller").GetProperty("legalName").GetString());
        Assert.Equal("Lahmacun", Assert.Single(draft.GetProperty("lines").EnumerateArray()).GetProperty("description").GetString());
        Assert.Equal(HttpStatusCode.NotFound, none.StatusCode);
        Assert.Equal("Bu sipariş için fatura taslağı yok.", (await none.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("from=2026-09-26&to=2026-09-20")]
    [InlineData("from=2026-08-01&to=2026-09-26")]
    public async Task AReversedOrTooLongRangeIsRefusedInTurkish(string query)
    {
        using var client = CreateClient(ReconciliationCaseTestDatabase.ViewOnlyToken);

        using var response = await client.GetAsync("/api/v1/management/order-invoices?" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("İstek doğrulanamadı.", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetProperty("message").GetString());
    }

    private async Task<Guid> SeedDraftAsync(string orderNumber, string serviceDate, decimal gross, decimal tax)
    {
        var orderId = Guid.NewGuid();
        await SeedDraftForOrderAsync(orderId, orderNumber, serviceDate, gross, tax);
        return orderId;
    }

    private async Task SeedDraftForOrderAsync(Guid orderId, string orderNumber, string serviceDate, decimal gross, decimal tax)
    {
        var invoiceId = Guid.NewGuid();
        var (netText, taxText, grossText) = ((gross - tax).ToString(Inv), tax.ToString(Inv), gross.ToString(Inv));
        await _database.ExecuteAsync(
            $"""
            INSERT INTO invoicing.order_invoices
                (invoice_id, order_id, provider, external_order_id, order_number, profile, ubl_profile_id, invoice_type_code, currency_code,
                 issue_date, service_date, status, buyer_kind, seller_legal_name, seller_tax_id_kind, seller_tax_id_number,
                 seller_tax_office, seller_address, web_address, line_extension_amount, tax_total, payable_amount)
            VALUES
                ('{invoiceId}', '{orderId}', 'yemeksepeti', '{orderNumber}', '{orderNumber}', 'EArsiv', 'EARSIVFATURA', 'SATIS', 'TRY',
                 '{serviceDate}', '{serviceDate}', 'Draft', 'FinalConsumer', 'Deniz Lokantası', 'Vkn', '1234567890',
                 'Kadıköy', 'Moda Cad. 1', 'https://www.yemeksepeti.com', {netText}, {taxText}, {grossText});
            INSERT INTO invoicing.order_invoice_lines (invoice_id, line_number, description, quantity, unit_code, tax_rate, net_amount, tax_amount, gross_amount)
            VALUES ('{invoiceId}', 1, 'Lahmacun', 1, 'C62', 10, {netText}, {taxText}, {grossText});
            """);
    }

    private async Task<Guid> SeedDeliveredOrderAsync(string orderNumber, string deliveredAgo, decimal total)
    {
        var orderId = Guid.NewGuid();
        await _database.ExecuteAsync(
            $"""
            INSERT INTO orders.orders (order_id, source, status, confirmation_status, order_number, total, created_at, updated_at, closed_at)
            VALUES ('{orderId}', 'Online', 'Completed', 'Accepted', '{orderNumber}', {total.ToString(Inv)}, now() - interval '{deliveredAgo}', now() - interval '{deliveredAgo}', now() - interval '{deliveredAgo}');
            INSERT INTO online_ordering.online_orders (order_id, provider, external_order_id) VALUES ('{orderId}', 'yemeksepeti', '{orderNumber}');
            """);
        return orderId;
    }

    private HttpClient CreateClient(string? managerToken)
    {
        var client = new HttpClient { BaseAddress = _baseAddress };
        if (managerToken is not null)
            client.DefaultRequestHeaders.Add("Cookie", $"{ReconciliationCaseEndpoints.ManagerCookieName}={managerToken}");
        return client;
    }
}
