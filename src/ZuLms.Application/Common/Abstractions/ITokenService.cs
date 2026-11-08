using ZuLms.Domain.Entities.Identity;

namespace ZuLms.Application.Common.Abstractions;

public interface ITokenService
{
    string CreateAccessToken(User user, AccountPermissionSet permissions,
        bool passwordChangeOnly, DateTimeOffset expiresAt);
}
