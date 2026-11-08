using ZuLms.Application.Common.Results;

namespace ZuLms.Application.Features.Credentials;

public interface ICredentialService
{
    Task<Result> ReauthenticateAsync(long actorUserId, string password, CancellationToken cancellationToken);
    Task<Result<RevealedCredentialsDto>> RevealAsync(long actorUserId, long targetUserId,
        CancellationToken cancellationToken);
    Task<Result> ChangeAsync(long actorUserId, long targetUserId, uint expectedVersion,
        string newPassword, bool requireChangeAtNextLogin, CancellationToken cancellationToken);
    Task<Result> SetSharedInitialAsync(long actorUserId, uint expectedVersion,
        string password, CancellationToken cancellationToken);
    Task<Result> QueueInitialAsync(long actorUserId, long targetUserId, bool confirmResend,
        CancellationToken cancellationToken);
    Task<Result<InitialCredentialsPreviewDto>> PreviewBulkInitialAsync(long actorUserId,
        CancellationToken cancellationToken);
    Task<Result<InitialCredentialsPreviewDto>> QueueBulkInitialAsync(long actorUserId,
        CancellationToken cancellationToken);
    Task<Result> ResolveUncertainAsync(long actorUserId, long targetUserId, bool received,
        CancellationToken cancellationToken);
}

public sealed record RevealedCredentialsDto(long UserId, string? InitialPassword,
    string? CurrentPassword);
public sealed record InitialCredentialsPreviewDto(int Eligible, int Sent, int Uncertain,
    int NoEmail, int Pending, int Ineligible);
