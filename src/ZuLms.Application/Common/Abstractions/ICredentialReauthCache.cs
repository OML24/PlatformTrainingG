namespace ZuLms.Application.Common.Abstractions;

public interface ICredentialReauthCache
{
    Task GrantAsync(long userId, string sessionId, DateTimeOffset expiresAt, CancellationToken ct);
    Task<bool> IsGrantedAsync(long userId, string sessionId, CancellationToken ct);
    Task InvalidateAsync(long userId, CancellationToken ct);
}
