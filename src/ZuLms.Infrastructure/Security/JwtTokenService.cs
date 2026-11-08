using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Domain.Entities.Identity;

namespace ZuLms.Infrastructure.Security;

public sealed class JwtTokenService : ITokenService
{
    public const string Issuer = "zu-lms";
    private readonly SigningCredentials credentials;
    private readonly IClock clock;

    public JwtTokenService(IClock clock)
    {
        this.clock = clock;
        string encoded = Environment.GetEnvironmentVariable("ZU_JWT_KEY")
            ?? throw new InvalidOperationException("ZU_JWT_KEY is required.");
        byte[] key;
        try { key = Convert.FromBase64String(encoded); }
        catch (FormatException ex) { throw new InvalidOperationException("ZU_JWT_KEY must be base64.", ex); }
        if (key.Length < 64)
            throw new InvalidOperationException("ZU_JWT_KEY must contain at least 64 bytes.");
        credentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);
    }

    public string CreateAccessToken(User user, AccountPermissionSet permissions,
        bool passwordChangeOnly, DateTimeOffset expiresAt)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("login", user.LoginName),
            new("name", user.FullNameAr),
            new("stamp", user.SecurityStamp.ToString("D")),
            new("lang", user.PreferredLanguage.ToString().ToLowerInvariant()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D"))
        };
        foreach (var role in permissions.Roles.Order())
            claims.Add(new Claim("roles", role.ToString()));
        // JWT claims are account-wide hints only; authorization always uses the active mode set.
        if (passwordChangeOnly) claims.Add(new Claim("pwd_change", "true"));
        var token = new JwtSecurityToken(Issuer, Issuer, claims,
            notBefore: clock.UtcNow.UtcDateTime, expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static SymmetricSecurityKey ReadValidationKey()
    {
        string encoded = Environment.GetEnvironmentVariable("ZU_JWT_KEY")
            ?? throw new InvalidOperationException("ZU_JWT_KEY is required.");
        byte[] bytes = Convert.FromBase64String(encoded);
        if (bytes.Length < 64) throw new InvalidOperationException("ZU_JWT_KEY must contain at least 64 bytes.");
        return new SymmetricSecurityKey(bytes);
    }
}
