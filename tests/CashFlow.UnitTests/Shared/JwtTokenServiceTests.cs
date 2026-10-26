namespace CashFlow.UnitTests.Shared;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CashFlow.Shared.Contracts.Security;
using Microsoft.Extensions.Options;
using Xunit;

public class JwtTokenServiceTests
{
    private readonly JwtOptions _options = new()
    {
        Issuer = "TestIssuer",
        Audience = "TestAudience",
        SecretKey = "SuperSecretKeyForCashFlowDevelopmentPurposes12345!",
        ExpirationMinutes = 30
    };

    [Fact]
    [Trait("Category", "Unit")]
    public void GenerateToken_WithValidClientId_ShouldReturnSignedJwtWithExpectedClaims()
    {
        // Arrange
        var service = new JwtTokenService(Options.Create(_options));
        const string clientId = "test-client-123";

        // Act
        var response = service.GenerateToken(clientId);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("Bearer", response.TokenType);
        Assert.Equal(1800, response.ExpiresInSeconds); // 30 min * 60s
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));

        // Valida o payload decodificado do JWT
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(response.AccessToken);

        Assert.Equal(_options.Issuer, jwt.Issuer);
        Assert.Contains(_options.Audience, jwt.Audiences);

        var subClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);
        Assert.NotNull(subClaim);
        Assert.Equal(clientId, subClaim.Value);

        var roleClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role || c.Type == "role");
        Assert.NotNull(roleClaim);
        Assert.Equal("Operator", roleClaim.Value);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GenerateToken_WithCustomRoles_ShouldIncludeAllRolesInClaims()
    {
        // Arrange
        var service = new JwtTokenService(Options.Create(_options));
        const string clientId = "admin-client";
        var roles = new[] { "Admin", "Auditor" };

        // Act
        var response = service.GenerateToken(clientId, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(response.AccessToken);

        var roleClaims = jwt.Claims.Where(c => c.Type == ClaimTypes.Role || c.Type == "role").Select(c => c.Value).ToList();
        Assert.Contains("Admin", roleClaims);
        Assert.Contains("Auditor", roleClaims);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Category", "Unit")]
    public void GenerateToken_WithEmptyOrWhitespaceClientId_ShouldThrowArgumentException(string invalidId)
    {
        // Arrange
        var service = new JwtTokenService(Options.Create(_options));

        // Act & Assert
        Assert.Throws<ArgumentException>(() => service.GenerateToken(invalidId));
    }
}
