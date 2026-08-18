using FluentAssertions;
using Xunit;

namespace ALKAROS.Reconciliation.CaseFoundation.Tests;

public sealed class ReconciliationDomainTests
{
    [Fact]
    public void CreateCaseRequestValidatesRequiredFields()
    {
        var validRequest = new CreateCaseRequest(
            DeduplicationKey: "qnb:txn:12345",
            CaseType: CaseType.PaymentMismatch,
            SourceARef: "order:ORD-101",
            SourceBRef: "bank:TX-999",
            DiscrepancyAmount: 50.00m,
            Severity: CaseSeverity.High,
            PerformedBy: Guid.NewGuid());

        var act = () => validRequest.Validate();
        act.Should().NotThrow();

        // Empty dedup key
        var invalidKey = validRequest with { DeduplicationKey = "" };
        var actKey = () => invalidKey.Validate();
        actKey.Should().Throw<ArgumentException>();

        // Empty source A
        var invalidSrcA = validRequest with { SourceARef = " " };
        var actSrcA = () => invalidSrcA.Validate();
        actSrcA.Should().Throw<ArgumentException>();

        // Empty user
        var invalidUser = validRequest with { PerformedBy = Guid.Empty };
        var actUser = () => invalidUser.Validate();
        actUser.Should().Throw<ArgumentException>();
    }
}
