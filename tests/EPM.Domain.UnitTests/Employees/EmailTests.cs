using EPM.Domain.Employees;
using FluentAssertions;

namespace EPM.Domain.UnitTests.Employees;

public class EmailTests
{
    [Theory]
    [InlineData("ada@epm.local")]
    [InlineData("ada.lovelace+projects@sub.epm.co.uk")]
    [InlineData("a_b-c@epm.io")]
    public void Create_WithValidAddress_Succeeds(string input)
    {
        Email.Create(input).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankInput_ReportsRequiredRatherThanInvalid(string? input)
    {
        // Separate errors because the frontend shows different messages for "you left this
        // empty" and "what you typed is wrong".
        Email.Create(input).Error.Should().Be(EmployeeErrors.EmailRequired);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@domain")]
    [InlineData("@epm.local")]
    [InlineData("two@@epm.local")]
    [InlineData("spaces in@epm.local")]
    [InlineData("trailing@epm.local.")]
    public void Create_WithMalformedAddress_Fails(string input)
    {
        Email.Create(input).Error.Should().Be(EmployeeErrors.EmailInvalid);
    }

    [Fact]
    public void Create_TrimsAndLowercases()
    {
        Email.Create("  Ada.Lovelace@EPM.Local  ").Value.Value.Should().Be("ada.lovelace@epm.local");
    }

    [Fact]
    public void Create_OverMaxLength_Fails()
    {
        var local = new string('a', Email.MaxLength);

        Email.Create($"{local}@epm.local").Error.Should().Be(EmployeeErrors.EmailTooLong);
    }

    [Fact]
    public void Equality_IsByValue_IgnoringOriginalCasing()
    {
        Email.Create("ada@epm.local").Value.Should().Be(Email.Create("ADA@EPM.LOCAL").Value);
    }
}
