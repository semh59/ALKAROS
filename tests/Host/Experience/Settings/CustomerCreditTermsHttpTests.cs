using ALKAROS.Host.DualScreen;
using System.Net;
using System.Net.Http.Json;
using ALKAROS.Host.Experience.CustomerCredit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ALKAROS.Host.Experience.Settings.Tests;

/// <summary>
/// V1-RMD-440: a manager sets a customer's credit limit and payment term over HTTP; the same manager session and
/// settings.manage permission as settings management guard it.
/// </summary>
[Collection("Settings management PostgreSQL HTTP")]
public sealed class CustomerCreditTermsHttpTests : IAsyncLifetime
{
    private readonly SettingsManagementTestDatabase _database = new();
    private WebApplication? _application;
    private Uri? _baseAddress;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(_database.DataSource);
        builder.Services.AddCustomerCreditTermsExperience();

        _application = builder.Build();
        _application.MapCustomerCreditTerms();
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
    public async Task AManagerSetsAndReadsACustomersCreditTerms()
    {
        var customerId = await SeedCustomerAsync(anonymized: false);
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);

        using var initial = await client.GetAsync(Path(customerId));
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        var none = await initial.Content.ReadFromJsonAsync<CustomerCreditTermsV1>();
        Assert.Equal(0m, none!.CreditLimit);
        Assert.Null(none.PaymentTermDays);
        Assert.Null(none.UpdatedAt);

        using var put = await client.PutAsJsonAsync(Path(customerId), new UpdateCustomerCreditTermsV1(500m, 30));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        using var read = await client.GetAsync(Path(customerId));
        var stored = await read.Content.ReadFromJsonAsync<CustomerCreditTermsV1>();
        Assert.Equal(500m, stored!.CreditLimit);
        Assert.Equal(30, stored.PaymentTermDays);
        Assert.NotNull(stored.UpdatedAt);
    }

    [Fact]
    public async Task MissingAndUnpermissionedSessionsAreRejectedWithoutWriting()
    {
        var customerId = await SeedCustomerAsync(anonymized: false);

        using var anonymous = CreateClient(null);
        using var unauthorized = await anonymous.PutAsJsonAsync(Path(customerId), new UpdateCustomerCreditTermsV1(100m, null));
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal("UNAUTHORIZED", (await ReadErrorAsync(unauthorized)).Error.Code);

        using var denied = CreateClient(SettingsManagementTestDatabase.DeniedToken);
        using var forbidden = await denied.PutAsJsonAsync(Path(customerId), new UpdateCustomerCreditTermsV1(100m, null));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("FORBIDDEN", (await ReadErrorAsync(forbidden)).Error.Code);

        Assert.Equal(0L, await CountTermsAsync(customerId));
    }

    [Fact]
    public async Task AnUnknownOrAnonymizedCustomerIsNotFound()
    {
        var anonymized = await SeedCustomerAsync(anonymized: true);
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);

        using var unknown = await client.PutAsJsonAsync(Path(Guid.NewGuid()), new UpdateCustomerCreditTermsV1(100m, null));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("NOT_FOUND", (await ReadErrorAsync(unknown)).Error.Code);

        using var anonymizedResponse = await client.GetAsync(Path(anonymized));
        Assert.Equal(HttpStatusCode.NotFound, anonymizedResponse.StatusCode);
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(100.001, null)]
    [InlineData(100, 0)]
    [InlineData(100, 366)]
    public async Task InvalidTermsAreRejected(double creditLimit, int? paymentTermDays)
    {
        var customerId = await SeedCustomerAsync(anonymized: false);
        using var client = CreateClient(SettingsManagementTestDatabase.ManagerToken);

        using var response = await client.PutAsJsonAsync(
            Path(customerId), new UpdateCustomerCreditTermsV1((decimal)creditLimit, paymentTermDays));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await ReadErrorAsync(response);
        Assert.Equal("VALIDATION_FAILED", error.Error.Code);
        Assert.Equal("Kredi limiti 0 veya üstü (en çok iki ondalık), vade 1-365 gün olmalı.", error.Error.Message);
        Assert.Equal(0L, await CountTermsAsync(customerId));
    }

    private static string Path(Guid customerId) => $"/api/v1/management/customers/{customerId}/credit-terms";

    private async Task<Guid> SeedCustomerAsync(bool anonymized)
    {
        var customerId = Guid.NewGuid();
        await using var command = _database.DataSource.CreateCommand(
            """
            INSERT INTO customer_data.profiles (customer_id, envelope_bytes, created_at, anonymized)
            VALUES (@id, '\x00'::bytea, now(), @anonymized);
            """);
        command.Parameters.AddWithValue("id", customerId);
        command.Parameters.AddWithValue("anonymized", anonymized);
        await command.ExecuteNonQueryAsync();
        return customerId;
    }

    private async Task<long> CountTermsAsync(Guid customerId)
    {
        await using var command = _database.DataSource.CreateCommand(
            "SELECT COUNT(*) FROM customer_account.credit_terms WHERE customer_id = @id;");
        command.Parameters.AddWithValue("id", customerId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private HttpClient CreateClient(string? token)
    {
        var client = new HttpClient { BaseAddress = _baseAddress };
        if (token is not null)
            client.DefaultRequestHeaders.Add("Cookie", $"{CustomerCreditTermsEndpoints.ManagerCookieName}={token}");
        return client;
    }

    private static async Task<ApiErrorEnvelope> ReadErrorAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<ApiErrorEnvelope>()
            ?? throw new InvalidOperationException("Expected a customer credit error response.");
}
