using ZuLms.Domain.Common;
using ZuLms.Domain.Enums;

namespace ZuLms.Domain.Entities.Identity;

public sealed class User : BaseEntity, ISoftDeletable
{
    public AccountType AccountType { get; set; }
    public string LoginName { get; set; } = string.Empty;
    public long? EmployeeId { get; set; }
    public string FullNameAr { get; set; } = string.Empty;
    public string? FullNameEn { get; set; }
    public string? EmailOverride { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordEnc { get; set; } = string.Empty;
    public string? InitialPasswordEnc { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTimeOffset? FirstLoginCompletedAt { get; set; }
    public DateTimeOffset? PasswordChangedAt { get; set; }
    public long? PasswordChangedBy { get; set; }
    public Guid SecurityStamp { get; set; }
    public CredentialsEmailStatus CredentialsEmailStatus { get; set; }
    public DateTimeOffset? CredentialsEmailSentAt { get; set; }
    public string? CredentialsEmailLastError { get; set; }
    public int CredentialsEmailAttempts { get; set; }
    public bool AccessOverrideAllow { get; set; }
    public long? AccessOverrideBy { get; set; }
    public DateTimeOffset? AccessOverrideAt { get; set; }
    public string? AccessOverrideReason { get; set; }
    public UiLanguage PreferredLanguage { get; set; }
    public string? UiSettings { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public string SearchText { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeactivatedAt { get; set; }
    public long? DeactivatedByUserId { get; set; }
    public string? DeactivationReason { get; set; }
    public Employee? Employee { get; set; }
    public User? PasswordChangedByUser { get; set; }
    public User? AccessOverrideByUser { get; set; }
}

