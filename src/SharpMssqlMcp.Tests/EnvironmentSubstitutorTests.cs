using SharpMssqlMcp.Services;
using Xunit;

namespace SharpMssqlMcp.Tests;

public class EnvironmentSubstitutorTests
{
    private readonly EnvironmentSubstitutor _substitutor = new();

    [Fact]
    public void Substitute_WithExistingVars_ReplacesCorrectly()
    {
        // Arrange
        Environment.SetEnvironmentVariable("TEST_USER", "admin");
        Environment.SetEnvironmentVariable("TEST_PASS", "secret123");
        var input = "User=${TEST_USER};Password=${TEST_PASS};";

        // Act
        var result = _substitutor.Substitute(input);

        // Assert
        Assert.Equal("User=admin;Password=secret123;", result);

        // Cleanup
        Environment.SetEnvironmentVariable("TEST_USER", null);
        Environment.SetEnvironmentVariable("TEST_PASS", null);
    }

    [Fact]
    public void Substitute_WithMissingVar_ThrowsException()
    {
        // Arrange
        var input = "Value=${MISSING_VAR}";

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => _substitutor.Substitute(input));
        Assert.Contains("MISSING_VAR", ex.Message);
    }

    [Fact]
    public void Substitute_WithNoVars_ReturnsSameString()
    {
        // Arrange
        var input = "Server=localhost;Database=testing;";

        // Act
        var result = _substitutor.Substitute(input);

        // Assert
        Assert.Equal(input, result);
    }
}
