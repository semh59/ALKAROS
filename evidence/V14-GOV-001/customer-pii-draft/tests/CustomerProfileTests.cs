using Xunit;

namespace ALKAROS.CustomerData.Draft.Tests;

public sealed class CustomerProfileTests
{
    private static CustomerProfile Sample(DateTimeOffset? createdAt = null, bool anonymized = false) => new(
        Guid.NewGuid(), "Ayşe Yılmaz", "05551234567", "ayse@example.com", "İstanbul", createdAt ?? DateTimeOffset.UtcNow, anonymized);

    [Fact]
    public void RetentionIsNotExpiredBeforeTenYears()
    {
        var profile = Sample(createdAt: DateTimeOffset.UtcNow.AddYears(-9));

        Assert.False(profile.IsRetentionExpired(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RetentionExpiresAtExactlyTenYears()
    {
        var createdAt = DateTimeOffset.UtcNow.AddYears(-10);
        var profile = Sample(createdAt: createdAt);

        Assert.True(profile.IsRetentionExpired(createdAt.AddYears(10)));
    }

    [Theory]
    [InlineData(CustomerAccessRole.Cashier)]
    [InlineData(CustomerAccessRole.Manager)]
    public void CashierAndManagerBothSeeTheFullProfile(CustomerAccessRole role)
    {
        var profile = Sample();

        var projected = CustomerProfileAccessPolicy.Project(profile, role);

        Assert.Equal(profile.Name, projected.Name);
        Assert.Equal(profile.Phone, projected.Phone);
        Assert.Equal(profile.Email, projected.Email);
        Assert.Equal(profile.Address, projected.Address);
    }

    [Fact]
    public void AnyOtherRoleGetsAFullyRedactedProfileNeverAPartialLeak()
    {
        var profile = Sample();

        var projected = CustomerProfileAccessPolicy.Project(profile, CustomerAccessRole.Other);

        Assert.Null(projected.Name);
        Assert.Null(projected.Phone);
        Assert.Null(projected.Email);
        Assert.Null(projected.Address);
        // The id itself is not PII and survives — referential integrity for
        // an order/invoice history still needs to point somewhere.
        Assert.Equal(profile.CustomerId, projected.CustomerId);
    }

    [Fact]
    public void AnonymizeWipesIdentifyingFieldsButKeepsTheRecordAndItsId()
    {
        var profile = Sample();

        var anonymized = CustomerProfileRetention.Anonymize(profile);

        Assert.True(anonymized.Anonymized);
        Assert.Null(anonymized.Name);
        Assert.Null(anonymized.Phone);
        Assert.Null(anonymized.Email);
        Assert.Null(anonymized.Address);
        Assert.Equal(profile.CustomerId, anonymized.CustomerId);
    }

    [Fact]
    public void AnonymizedCustomerCannotHaveAnInvoiceIssuedAgainstIt()
    {
        var anonymized = CustomerProfileRetention.Anonymize(Sample());

        Assert.Throws<InvalidOperationException>(() => CustomerProfileRetention.ThrowIfCannotInvoice(anonymized));
    }

    [Fact]
    public void ANonAnonymizedCustomerPassesTheInvoiceGuard()
    {
        var profile = Sample();

        var exception = Record.Exception(() => CustomerProfileRetention.ThrowIfCannotInvoice(profile));

        Assert.Null(exception);
    }
}
