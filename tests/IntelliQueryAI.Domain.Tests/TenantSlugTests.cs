using FluentAssertions;
using IntelliQueryAI.Domain.Errors;
using IntelliQueryAI.Domain.ValueObjects;

namespace IntelliQueryAI.Domain.Tests;

public class TenantSlugTests
{
    [Theory]
    [InlineData("acme")]
    [InlineData("globex")]
    [InlineData("acme-corp")]
    [InlineData("tenant-123")]
    public void Create_WithValidSlug_ShouldSucceed(string input)
    {
        // Act
        var result = TenantSlug.Create(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(input);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithEmptyOrNull_ShouldFailWithSlugRequired(string? input)
    {
        // Act
        var result = TenantSlug.Create(input);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WorkspaceErrors.SlugRequired.Code);
    }

    [Theory]
    [InlineData("ab")] // length 2 (less than 3)
    [InlineData("this-is-a-very-long-slug-that-exceeds-the-maximum-allowed-sixty-three-characters-limit")]
    public void Create_WithInvalidLength_ShouldFail(string input)
    {
        // Act
        var result = TenantSlug.Create(input);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WorkspaceErrors.SlugInvalidLength(3, 63).Code);
    }

    [Theory]
    [InlineData("-invalid")]
    [InlineData("invalid-")]
    [InlineData("invalid--slug")]
    [InlineData("invalid.slug")]
    [InlineData("invalid_slug")]
    [InlineData("Invalid!Slug")]
    public void Create_WithInvalidCharactersOrFormat_ShouldFail(string input)
    {
        // Act
        var result = TenantSlug.Create(input);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WorkspaceErrors.SlugInvalidFormat.Code);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("api")]
    [InlineData("billing")]
    [InlineData("www")]
    public void Create_WithReservedSlug_ShouldFail(string input)
    {
        // Act
        var result = TenantSlug.Create(input);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WorkspaceErrors.SlugReserved(input).Code);
    }

    [Theory]
    [InlineData("Acme Analytics", "acme-analytics")]
    [InlineData("Mark's Big Data & AI 2026!", "mark-s-big-data-ai-2026")]
    [InlineData("admin", "admin-ws")]
    [InlineData("api", "api-ws")]
    [InlineData("A", "a-ws")]
    [InlineData("AB", "ab-w")]
    [InlineData(null, "workspace")]
    [InlineData("", "workspace")]
    [InlineData("   ", "workspace")]
    [InlineData("---", "workspace")]
    public void Generate_ShouldProduceValidUrlSafeSlug(string? input, string expected)
    {
        // Act
        string generated = TenantSlug.Generate(input);

        // Assert
        generated.Should().Be(expected);

        // Verify that the generated slug passes Create validation
        var result = TenantSlug.Create(generated);
        result.IsSuccess.Should().BeTrue();
    }
}
