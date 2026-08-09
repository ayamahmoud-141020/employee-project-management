using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EPM.Application.Abstractions;
using EPM.Domain.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EPM.Infrastructure.Identity;

public sealed class JwtTokenService(IOptions<JwtOptions> options, IDateTimeProvider clock) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public AccessToken CreateAccessToken(AppUser user)
    {
        var issuedAt = clock.UtcNow;
        var expiresAt = issuedAt.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email.Value),
            // A unique token id, so an individual token can be identified in logs or added to
            // a deny list later without invalidating everything the user holds.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(AppClaimTypes.UserId, user.Id.ToString()),
            new(AppClaimTypes.DisplayName, user.DisplayName),
            new(ClaimTypes.Role, RoleNames.From(user.Role)),
        };

        // Only present for accounts linked to an employee. Its absence is what makes
        // "my assignments" return nothing for a service admin rather than everything.
        if (user.EmployeeId.HasValue)
        {
            claims.Add(new Claim(AppClaimTypes.EmployeeId, user.EmployeeId.Value.ToString()));
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    // 256 bits of CSPRNG output. Base64Url so it survives being put in a JSON body, a header
    // or a cookie without escaping.
    public string CreateRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    public DateTime GetRefreshTokenExpiry(DateTime issuedAtUtc) => issuedAtUtc.AddDays(_options.RefreshTokenDays);
}
