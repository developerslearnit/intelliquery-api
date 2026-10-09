using FluentAssertions;
using IntelliQueryAI.Infrastructure.SchemaDiscovery.Aliasing;
using Xunit;

namespace IntelliQueryAI.Application.Tests;

public sealed class BusinessNameGeneratorTests
{
    [Theory]
    [InlineData("tbl_customer", "Customer")]
    [InlineData("tblCustomer", "Customer")]
    [InlineData("vw_sales_summary", "Sales Summary")]
    [InlineData("usp_calculate_tax", "Calculate Tax")]
    [InlineData("usr_first_name", "User First Name")]
    [InlineData("cust_id", "Customer ID")]
    [InlineData("inv_amt", "Invoice Amount")]
    [InlineData("order_created_at_dt", "Order Created At Date")]
    [InlineData("billing_address_line1", "Billing Address Line 1")]
    [InlineData("is_active", "Is Active")]
    public void ToFriendlyName_ShouldGenerateExpectedBusinessAlias(string technicalName, string expectedFriendlyName)
    {
        // Act
        var result = BusinessNameGenerator.ToFriendlyName(technicalName);

        // Assert
        result.Should().Be(expectedFriendlyName);
    }

    [Fact]
    public void ToFriendlyName_WhenNullOrWhiteSpace_ShouldReturnEmptyString()
    {
        BusinessNameGenerator.ToFriendlyName("").Should().BeEmpty();
        BusinessNameGenerator.ToFriendlyName("   ").Should().BeEmpty();
        BusinessNameGenerator.ToFriendlyName(null!).Should().BeEmpty();
    }
}
