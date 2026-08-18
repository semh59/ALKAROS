using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Observability.Foundation.Tests;

public sealed class CorrelationAndRedactionTests
{
    private readonly ObservabilityRedactionHook _redactionHook = new();

    [Fact]
    public void CorrelationScopePropagatesAndRestoresContext()
    {
        var parentCorrelationId = "corr-order-12345";
        var parentRequestId = "req-1";
        var userId = Guid.NewGuid();

        // 1. Initial State
        CorrelationContext.Current.Should().BeNull();

        // 2. Begin Parent Scope
        using (CorrelationContext.BeginScope(parentCorrelationId, parentRequestId, userId, "order.create"))
        {
            CorrelationContext.Current.Should().NotBeNull();
            CorrelationContext.CorrelationId.Should().Be(parentCorrelationId);
            CorrelationContext.RequestId.Should().Be(parentRequestId);
            CorrelationContext.Current!.UserId.Should().Be(userId);
            CorrelationContext.Current.TraceChain.Should().ContainSingle().Which.Should().Be("order.create");

            // Add Step
            CorrelationContext.AddTraceStep("order.validate");
            CorrelationContext.Current.TraceChain.Should().Equal("order.create", "order.validate");

            // 3. Begin Nested Scope (e.g., kitchen print background task)
            using (CorrelationContext.BeginScope(parentCorrelationId, "req-2-print", null, "kitchen.print"))
            {
                CorrelationContext.CorrelationId.Should().Be(parentCorrelationId);
                CorrelationContext.RequestId.Should().Be("req-2-print");
                CorrelationContext.Current!.TraceChain.Should().Equal("order.create", "order.validate", "kitchen.print");
            }

            // 4. Back in Parent Scope
            CorrelationContext.RequestId.Should().Be(parentRequestId);
        }

        // 5. Exited All Scopes
        CorrelationContext.Current.Should().BeNull();
    }

    [Fact]
    public void RedactionHookMasksSensitiveKeysInJsonPayload()
    {
        var sensitiveJson = """
            {
                "order_id": "ORD-001",
                "table_number": "T-12",
                "total_amount": 350.50,
                "auth_token": "bearer_jwt_secret_token_12345",
                "customer": {
                    "name": "Ahmet Yilmaz",
                    "tc_kimlik": "12345678901",
                    "card_number": "5421000011112222",
                    "cvv": "999"
                },
                "payment_gateway": {
                    "provider": "QNB",
                    "api_key": "qnb_live_api_key_secret",
                    "client_secret": "top_secret_key"
                }
            }
            """;

        var redacted = _redactionHook.RedactJson(sensitiveJson);

        using var doc = JsonDocument.Parse(redacted);
        var root = doc.RootElement;

        // Non-sensitive preserved
        root.GetProperty("order_id").GetString().Should().Be("ORD-001");
        root.GetProperty("table_number").GetString().Should().Be("T-12");
        root.GetProperty("total_amount").GetDecimal().Should().Be(350.50m);
        root.GetProperty("customer").GetProperty("name").GetString().Should().Be("Ahmet Yilmaz");
        root.GetProperty("payment_gateway").GetProperty("provider").GetString().Should().Be("QNB");

        // Sensitive masked with ***REDACTED***
        root.GetProperty("auth_token").GetString().Should().Be(ObservabilityRedactionHook.RedactedPlaceholder);
        root.GetProperty("customer").GetProperty("tc_kimlik").GetString().Should().Be(ObservabilityRedactionHook.RedactedPlaceholder);
        root.GetProperty("customer").GetProperty("card_number").GetString().Should().Be(ObservabilityRedactionHook.RedactedPlaceholder);
        root.GetProperty("customer").GetProperty("cvv").GetString().Should().Be(ObservabilityRedactionHook.RedactedPlaceholder);
        root.GetProperty("payment_gateway").GetProperty("api_key").GetString().Should().Be(ObservabilityRedactionHook.RedactedPlaceholder);
        root.GetProperty("payment_gateway").GetProperty("client_secret").GetString().Should().Be(ObservabilityRedactionHook.RedactedPlaceholder);
    }

    [Theory]
    [InlineData("password", true)]
    [InlineData("client_secret", true)]
    [InlineData("api_key", true)]
    [InlineData("card_number", true)]
    [InlineData("cvv", true)]
    [InlineData("order_id", false)]
    [InlineData("table_number", false)]
    [InlineData("status", false)]
    public void IsSensitiveKeyIdentifiesProtectedFields(string keyName, bool isSensitive)
    {
        _redactionHook.IsSensitiveKey(keyName).Should().Be(isSensitive);
    }

    [Theory]
    [InlineData(RetentionPolicyCatalog.HotOperational7D, true)]
    [InlineData(RetentionPolicyCatalog.StandardOperational30D, true)]
    [InlineData(RetentionPolicyCatalog.ComplianceAudit90D, true)]
    [InlineData(RetentionPolicyCatalog.ExtendedAudit365D, true)]
    [InlineData("UNKNOWN_POLICY", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void RetentionPolicyCatalogValidatesApprovedPolicies(string? policyId, bool isApproved)
    {
        RetentionPolicyCatalog.IsApproved(policyId).Should().Be(isApproved);
    }
}
