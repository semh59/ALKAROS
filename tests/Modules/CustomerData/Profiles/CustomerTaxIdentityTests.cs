using Xunit;

namespace ALKAROS.CustomerData.Profiles.Tests;

/// <summary>V1-RMD-453: tax identity validation, masking and its place in the profile access policy.</summary>
public sealed class CustomerTaxIdentityTests
{
    [Theory]
    [InlineData("1234567890")]
    [InlineData("9876543217")]
    [InlineData("0000000019")]
    [InlineData("4567890128")]
    public void AValidVknWithItsTaxOfficeIsAccepted(string vkn)
    {
        var identity = CustomerTaxIdentity.Create(CustomerTaxIdKind.Vkn, $" {vkn} ", " Kadıköy ");

        Assert.Equal(CustomerTaxIdKind.Vkn, identity.Kind);
        Assert.Equal(vkn, identity.Number);
        Assert.Equal("Kadıköy", identity.TaxOffice);
        Assert.False(identity.IsMasked);
    }

    [Theory]
    [InlineData("1234567891")]
    [InlineData("9876543210")]
    [InlineData("4567890120")]
    public void AVknWithAWrongCheckDigitIsRefused(string vkn)
    {
        var exception = Assert.Throws<InvalidCustomerTaxIdentityException>(
            () => CustomerTaxIdentity.Create(CustomerTaxIdKind.Vkn, vkn, "Kadıköy"));

        Assert.Equal(CustomerTaxIdentityError.VknChecksum, exception.Error);
    }

    [Theory]
    [InlineData("123456789")]
    [InlineData("12345678901")]
    [InlineData("12345678a0")]
    [InlineData("")]
    public void AVknThatIsNotTenDigitsIsRefused(string vkn)
    {
        var exception = Assert.Throws<InvalidCustomerTaxIdentityException>(
            () => CustomerTaxIdentity.Create(CustomerTaxIdKind.Vkn, vkn, "Kadıköy"));

        Assert.Equal(CustomerTaxIdentityError.VknLength, exception.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void AVknNeedsItsTaxOffice(string? taxOffice)
    {
        var exception = Assert.Throws<InvalidCustomerTaxIdentityException>(
            () => CustomerTaxIdentity.Create(CustomerTaxIdKind.Vkn, "1234567890", taxOffice));

        Assert.Equal(CustomerTaxIdentityError.TaxOfficeRequired, exception.Error);
    }

    [Fact]
    public void ATaxOfficeLongerThanTheLimitIsRefused()
    {
        var exception = Assert.Throws<InvalidCustomerTaxIdentityException>(() => CustomerTaxIdentity.Create(
            CustomerTaxIdKind.Vkn, "1234567890", new string('a', CustomerTaxIdentity.TaxOfficeMaxLength + 1)));

        Assert.Equal(CustomerTaxIdentityError.TaxOfficeTooLong, exception.Error);
    }

    [Theory]
    [InlineData("10000000146")]
    [InlineData("12345678950")]
    [InlineData("98765432150")]
    public void AValidTcknIsAcceptedWithOrWithoutATaxOffice(string tckn)
    {
        var withoutOffice = CustomerTaxIdentity.Create(CustomerTaxIdKind.Tckn, tckn, null);
        var withOffice = CustomerTaxIdentity.Create(CustomerTaxIdKind.Tckn, tckn, "Çankaya");

        Assert.Equal(tckn, withoutOffice.Number);
        Assert.Null(withoutOffice.TaxOffice);
        Assert.Equal("Çankaya", withOffice.TaxOffice);
    }

    [Theory]
    [InlineData("10000000156")] // 10th digit wrong
    [InlineData("10000000147")] // 11th digit wrong
    [InlineData("12345678951")]
    public void ATcknWithWrongCheckDigitsIsRefused(string tckn)
    {
        var exception = Assert.Throws<InvalidCustomerTaxIdentityException>(
            () => CustomerTaxIdentity.Create(CustomerTaxIdKind.Tckn, tckn, null));

        Assert.Equal(CustomerTaxIdentityError.TcknChecksum, exception.Error);
    }

    [Theory]
    [InlineData("1000000014")]
    [InlineData("100000001460")]
    [InlineData("01234567890")]
    [InlineData("1000000014x")]
    public void ATcknThatIsNotElevenDigitsOrStartsWithZeroIsRefused(string tckn)
    {
        var exception = Assert.Throws<InvalidCustomerTaxIdentityException>(
            () => CustomerTaxIdentity.Create(CustomerTaxIdKind.Tckn, tckn, null));

        Assert.Equal(CustomerTaxIdentityError.TcknLength, exception.Error);
    }

    [Fact]
    public void MaskingKeepsOnlyTheLastThreeDigits()
    {
        var masked = CustomerTaxIdentity.Create(CustomerTaxIdKind.Vkn, "1234567890", "Kadıköy").Masked();

        Assert.Equal("*******890", masked.Number);
        Assert.True(masked.IsMasked);
        Assert.Equal("Kadıköy", masked.TaxOffice);
        Assert.Same(masked, masked.Masked());
    }

    private static CustomerProfile Sample() => new(
        Guid.NewGuid(), "Deniz Gıda Ltd.", "05551234567", null, null, DateTimeOffset.UtcNow, false, 1,
        CustomerTaxIdentity.Create(CustomerTaxIdKind.Vkn, "1234567890", "Kadıköy"));

    [Fact]
    public void TheManagerSeesTheFullTaxNumber()
    {
        var projected = CustomerProfileAccessPolicy.Project(Sample(), CustomerAccessRole.Manager);

        Assert.Equal("1234567890", projected.TaxIdentity!.Number);
        Assert.False(projected.TaxIdentity.IsMasked);
    }

    [Fact]
    public void TheCashierSeesTheTaxNumberMaskedButTheContactFieldsInFull()
    {
        var profile = Sample();

        var projected = CustomerProfileAccessPolicy.Project(profile, CustomerAccessRole.Cashier);

        Assert.Equal("*******890", projected.TaxIdentity!.Number);
        Assert.True(projected.TaxIdentity.IsMasked);
        Assert.Equal(CustomerTaxIdKind.Vkn, projected.TaxIdentity.Kind);
        Assert.Equal(profile.Name, projected.Name);
        Assert.Equal(profile.Phone, projected.Phone);
    }

    [Fact]
    public void AnyOtherRoleSeesNoTaxIdentity()
    {
        var projected = CustomerProfileAccessPolicy.Project(Sample(), CustomerAccessRole.Other);

        Assert.Null(projected.TaxIdentity);
    }
}
