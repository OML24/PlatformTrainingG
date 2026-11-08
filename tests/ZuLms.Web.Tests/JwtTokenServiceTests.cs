using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Domain.Entities.Identity;
using ZuLms.Domain.Enums;
using ZuLms.Infrastructure.Security;

namespace ZuLms.Web.Tests;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void LimitedTokenHasIdentityRolesAndPasswordChangeClaim()
    {
        string? previous = Environment.GetEnvironmentVariable("ZU_JWT_KEY");
        Environment.SetEnvironmentVariable("ZU_JWT_KEY", Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)));
        try
        {
            var now = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
            var clock = new FixedClock(now);
            var service = new JwtTokenService(clock);
            var user = new User
            {
                Id = 42,
                LoginName = "1234",
                FullNameAr = "موظف تجريبي 1",
                SecurityStamp = Guid.NewGuid(),
                PreferredLanguage = UiLanguage.Ar
            };
            var permissions = new AccountPermissionSet(42, AccountType.Employee,
                new HashSet<RoleCode> { RoleCode.Member, RoleCode.Dean }, new HashSet<long> { 7 }, true);
            string jwt = service.CreateAccessToken(user, permissions, true, now.AddMinutes(15));
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(jwt, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = JwtTokenService.Issuer,
                ValidateAudience = true,
                ValidAudience = JwtTokenService.Issuer,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = JwtTokenService.ReadValidationKey(),
                ValidateLifetime = false
            }, out var validated);
            Assert.Equal("42", principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
            Assert.Equal("true", principal.FindFirst("pwd_change")?.Value);
            Assert.Equal(user.SecurityStamp.ToString("D"), principal.FindFirst("stamp")?.Value);
            Assert.Contains(principal.Claims, c => c.Value == "Member");
            Assert.Contains(principal.Claims, c => c.Value == "Dean");
            Assert.True(principal.IsInRole("Member"));
            Assert.True(principal.IsInRole("Dean"));
            Assert.DoesNotContain(principal.Claims, c => c.Type == "dean_scope");
            Assert.Equal(now.AddMinutes(15).UtcDateTime, validated.ValidTo);
        }
        finally { Environment.SetEnvironmentVariable("ZU_JWT_KEY", previous); }
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
        public DateOnly TodayAmman => DateOnly.FromDateTime(now.UtcDateTime);
        public TimeZoneInfo Amman => TimeZoneInfo.Utc;
    }
}
