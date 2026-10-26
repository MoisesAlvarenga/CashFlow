namespace CashFlow.Shared.Contracts.Security;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

public interface IJwtTokenService
{
    TokenResponse GenerateToken(string clientId, string[]? roles = null);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options?.Value ?? new JwtOptions();
    }

    public TokenResponse GenerateToken(string clientId, string[]? roles = null)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new ArgumentException("O ClientId não pode ser nulo ou vazio.", nameof(clientId));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, clientId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, clientId)
        };

        if (roles != null && roles.Length > 0)
        {
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }
        else
        {
            claims.Add(new Claim(ClaimTypes.Role, "Operator"));
        }

        var expires = DateTime.UtcNow.AddMinutes(_options.ExpirationMinutes);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials
        );

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenString = tokenHandler.WriteToken(token);

        return new TokenResponse(
            AccessToken: tokenString,
            TokenType: "Bearer",
            ExpiresInSeconds: (int)TimeSpan.FromMinutes(_options.ExpirationMinutes).TotalSeconds
        );
    }
}
