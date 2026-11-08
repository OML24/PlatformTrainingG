using ZuLms.Application.Common.Results;

namespace ZuLms.Application.Features.Auth;

public interface IAuthService
{
    Task<Result<AuthSession>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<Result<AuthSession>> ChangePasswordAsync(long userId, string newPassword, string confirmation,
        CancellationToken cancellationToken);
    Task<Result<AuthSession>> ChangeOwnPasswordAsync(long userId, string currentPassword,
        string newPassword, string confirmation, CancellationToken cancellationToken);
    Task<Result<AuthSession>> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task<Result> LogoutAsync(string? refreshToken, CancellationToken cancellationToken);
    Task<Result> ForgotPasswordAsync(string loginName, string email, string ip,
        CancellationToken cancellationToken);
    Task<Result> SetLanguageAsync(long userId, ZuLms.Domain.Enums.UiLanguage language,
        CancellationToken cancellationToken);
}

public sealed record LoginRequest(string LoginName, string Password, bool RememberMe);
public sealed record AuthSession(string AccessToken, string? RefreshToken, DateTimeOffset AccessExpiresAt,
    DateTimeOffset? RefreshExpiresAt, bool RememberMe, bool PasswordChangeOnly, string RedirectUrl);
