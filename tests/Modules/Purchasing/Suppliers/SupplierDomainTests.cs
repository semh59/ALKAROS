using System;
using FluentAssertions;
using Xunit;

namespace ALKAROS.Purchasing.Suppliers.Tests;

public sealed class SupplierDomainTests
{
    [Fact]
    public void CreateSupplierWithValidDetailsNormalizesCodeAndFields()
    {
        var supplier = Supplier.Create(
            code: " sup-001 ",
            name: " Fresh Farms Ltd ",
            taxNumber: " 1234567890 ",
            taxOffice: " Kadikoy ",
            phone: " +905551234567 ",
            email: " info@freshfarms.com ");

        supplier.Code.Should().Be("SUP-001");
        supplier.Name.Should().Be("Fresh Farms Ltd");
        supplier.TaxNumber.Should().Be("1234567890");
        supplier.TaxOffice.Should().Be("Kadikoy");
        supplier.Phone.Should().Be("+905551234567");
        supplier.Email.Should().Be("info@freshfarms.com");
        supplier.Active.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateSupplierWithInvalidCodeThrowsInvalidSupplierDataException(string? invalidCode)
    {
        var act = () => Supplier.Create(invalidCode!, "Valid Name");
        act.Should().Throw<InvalidSupplierDataException>()
            .WithMessage("*code*");
    }

    [Fact]
    public void CreateSupplierWithCodeExceeding64CharsThrowsInvalidSupplierDataException()
    {
        var longCode = new string('A', 65);
        var act = () => Supplier.Create(longCode, "Valid Name");
        act.Should().Throw<InvalidSupplierDataException>()
            .WithMessage("*cannot exceed 64*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateSupplierWithInvalidNameThrowsInvalidSupplierDataException(string? invalidName)
    {
        var act = () => Supplier.Create("SUP-01", invalidName!);
        act.Should().Throw<InvalidSupplierDataException>()
            .WithMessage("*name*");
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("@domain.com")]
    [InlineData("user@")]
    public void CreateSupplierWithInvalidEmailThrowsInvalidSupplierDataException(string invalidEmail)
    {
        var act = () => Supplier.Create("SUP-01", "Supplier 1", email: invalidEmail);
        act.Should().Throw<InvalidSupplierDataException>()
            .WithMessage("*Invalid email format*");
    }

    [Fact]
    public void AssertCanAcceptOrdersWhenActiveSucceeds()
    {
        var supplier = Supplier.Create("SUP-01", "Supplier 1", active: true);
        var act = () => supplier.AssertCanAcceptOrders();
        act.Should().NotThrow();
    }

    [Fact]
    public void AssertCanAcceptOrdersWhenInactiveThrowsInactiveSupplierException()
    {
        var supplier = Supplier.Create("SUP-01", "Supplier 1", active: false);
        var act = () => supplier.AssertCanAcceptOrders();
        act.Should().Throw<InactiveSupplierException>()
            .WithMessage("*inactive*");
    }

    [Fact]
    public void DeactivateAndActivateUpdatesStatusAndTimestamp()
    {
        var supplier = Supplier.Create("SUP-01", "Supplier 1");
        var initialUpdated = supplier.UpdatedAt;

        supplier.Deactivate();
        supplier.Active.Should().BeFalse();
        supplier.UpdatedAt.Should().BeOnOrAfter(initialUpdated);

        supplier.Activate();
        supplier.Active.Should().BeTrue();
    }

    [Fact]
    public void UpdateDetailsUpdatesProperties()
    {
        var supplier = Supplier.Create("SUP-01", "Old Name");
        supplier.UpdateDetails("New Name", "9876543210", "Besiktas", "+905559876543", "contact@supplier.com");

        supplier.Name.Should().Be("New Name");
        supplier.TaxNumber.Should().Be("9876543210");
        supplier.TaxOffice.Should().Be("Besiktas");
        supplier.Phone.Should().Be("+905559876543");
        supplier.Email.Should().Be("contact@supplier.com");
    }

    [Theory]
    [InlineData("Manager", false)]
    [InlineData("Admin", false)]
    [InlineData("Finance", false)]
    [InlineData("Cashier", true)]
    [InlineData("Waiter", true)]
    [InlineData(null, true)]
    [InlineData("", true)]
    public void MaskingPolicyProjectsCorrectlyBasedOnRole(string? role, bool shouldBeMasked)
    {
        var supplier = Supplier.Create(
            code: "SUP-01",
            name: "Dairy Supplier",
            taxNumber: "1234567890",
            taxOffice: "Uskudar",
            phone: "+905551234567",
            email: "sales@dairysupplier.com");

        var view = SupplierAccessPolicy.ProjectToView(supplier, role);

        view.IsMasked.Should().Be(shouldBeMasked);
        if (shouldBeMasked)
        {
            view.TaxNumber.Should().Contain("*");
            view.TaxOffice.Should().Be("***");
            view.Phone.Should().Contain("***");
            view.Email.Should().Contain("***");
        }
        else
        {
            view.TaxNumber.Should().Be("1234567890");
            view.TaxOffice.Should().Be("Uskudar");
            view.Phone.Should().Be("+905551234567");
            view.Email.Should().Be("sales@dairysupplier.com");
        }
    }
}
