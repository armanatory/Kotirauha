using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Kotirauha.Core.Abstractions;
using Kotirauha.Core.Domain;
using Microsoft.IdentityModel.Tokens;

namespace Kotirauha.Infrastructure.Services;

public class JwtSettings
{
    public string Secret { get; init; } = string.Empty;
    public string Issuer { get; init; } = "kotirauha";
    public string Audience { get; init; } = "kotirauha-users";
    public int ExpiryDays { get; init; } = 7;
}

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;

    public JwtTokenService(JwtSettings settings) => _settings = settings;

    public string CreateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("name", user.DisplayName),
        };

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(_settings.ExpiryDays),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
