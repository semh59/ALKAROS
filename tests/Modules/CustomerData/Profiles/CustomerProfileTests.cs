using Xunit;

namespace ALKAROS.CustomerData.Profiles.Tests;

public sealed class CustomerProfileTests
{
    private static CustomerProfile Sample(DateTimeOffset? createdAt = null, bool anonymized = false, int rowVersion = 1) => new(
        Guid.NewGuid(), "Ayşe Yılmaz", "05551234567", "ayse@example.com", "İstanbul",
        createdAt ?? DateTimeOffset.UtcNow, anonymized, rowVersion);

    [Fact]
    public void CustomerIdMustNotBeEmpty()
    {
        Assert.Throws<ArgumentException>(() => new CustomerProfile(
            Guid.Empty, "Ayşe", null, null, null, DateTimeOffset.UtcNow, false, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RowVersionMustBePositive(int rowVersion)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CustomerProfile(
            Guid.NewGuid(), "Ayşe", null, null, null, DateTimeOffset.UtcNow, false, rowVersion));
    }

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
        // The id itself is not PII and survives - referential integrity for
        // an order/invoice history still needs to point somewhere.
        Assert.Equal(profile.CustomerId, projected.CustomerId);
    }

    [Fact]
    public void AnonymizedCustomerCannotHaveAnInvoiceIssuedAgainstIt()
    {
        var anonymized = Sample(anonymized: true);

        Assert.Throws<CustomerProfileAnonymizedException>(() => CustomerProfileRetention.ThrowIfCannotInvoice(anonymized));
    }

    [Fact]
    public void ANonAnonymizedCustomerPassesTheInvoiceGuard()
    {
        var profile = Sample();

        var exception = Record.Exception(() => CustomerProfileRetention.ThrowIfCannotInvoice(profile));

        Assert.Null(exception);
    }
}
