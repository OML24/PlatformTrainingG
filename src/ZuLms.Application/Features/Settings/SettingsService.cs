using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Domain.Enums;

namespace ZuLms.Application.Features.Settings;

public sealed class SettingsService(
    IAppDbContext db, IClock clock, IPermissionService permissions,
    IEncryptionService encryption, IAuditLogger audit) : ISettingsService
{
    private static readonly ConcurrentDictionary<string, (DateTimeOffset Expires, string Value)> cache = new(StringComparer.Ordinal);

    public async Task<Result<IReadOnlyList<SystemSettingDto>>> ListAsync(long actorUserId,
        CancellationToken cancellationToken)
    {
        if (!await permissions.HasAsync(actorUserId, "page.admin.settings", cancellationToken))
            return Result<IReadOnlyList<SystemSettingDto>>.Failure("FORBIDDEN");
        var settings = await db.SystemSettings.AsNoTracking()
            .OrderBy(x => x.GroupKey).ThenBy(x => x.SortOrder).ToListAsync(cancellationToken);
        IReadOnlyList<SystemSettingDto> rows = settings.Select(x => new SystemSettingDto(
            x.SettingKey, x.GroupKey, x.ValueType.ToString(), x.IsSecret ? null : x.Value,
            x.IsSecret ? null : x.DefaultValue, x.MinValue, x.MaxValue,
            x.LabelAr, x.LabelEn, x.DescriptionAr, x.DescriptionEn,
            x.IsSecret, x.Version, x.SortOrder)).ToList();
        return Result<IReadOnlyList<SystemSettingDto>>.Success(rows);
    }

    public async Task<T> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        if (!cache.TryGetValue(key, out var saved) || saved.Expires <= clock.UtcNow)
        {
            var setting = await db.SystemSettings.AsNoTracking().SingleOrDefaultAsync(x => x.SettingKey == key, cancellationToken)
                ?? throw new InvalidOperationException($"Missing setting: {key}");
            string plain = setting.IsSecret && !string.IsNullOrEmpty(setting.Value)
                ? encryption.Decrypt(setting.Value) : setting.Value;
            saved = (clock.UtcNow.AddSeconds(30), plain);
            cache[key] = saved;
        }
        return ConvertValue<T>(saved.Value);
    }

    public async Task<Result> SetAsync(string key, string value, uint expectedVersion, long actorUserId,
        CancellationToken cancellationToken)
    {
        if (!await permissions.HasAsync(actorUserId, "settings.edit", cancellationToken))
            return Result.Failure("FORBIDDEN");
        if (key == "auth.shared_initial_password") return Result.Failure("USE_CREDENTIAL_SERVICE");
        var setting = await db.SystemSettings.SingleOrDefaultAsync(x => x.SettingKey == key, cancellationToken);
        if (setting is null) return Result.Failure("SETTING_NOT_FOUND");
        if (setting.Version != expectedVersion) return Result.Failure("CONFLICT");
        if (!IsValid(value, setting)) return Result.Failure("SETTING_INVALID");
        string oldValue = setting.IsSecret ? "[محذوف]" : setting.Value;
        setting.Value = setting.IsSecret && !string.IsNullOrEmpty(value) ? encryption.Encrypt(value) : value;
        setting.UpdatedBy = actorUserId;
        setting.UpdatedAt = clock.UtcNow;
        await audit.LogAsync(new AuditEntry(AuditCategory.Settings, "settings.edit",
            "تعديل إعداد النظام", "System setting updated", "SystemSetting", null, key,
            BeforeJson: JsonSerializer.Serialize(new { value = oldValue }),
            AfterJson: JsonSerializer.Serialize(new { value = setting.IsSecret ? "[محذوف]" : value })), cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            Invalidate(key);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure("CONFLICT");
        }
    }

    public void Invalidate(string key) => cache.TryRemove(key, out _);

    private static bool IsValid(string raw, ZuLms.Domain.Entities.Config.SystemSetting setting)
    {
        switch (setting.ValueType)
        {
            case SettingValueType.Int:
                if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)) return false;
                return (!setting.MinValue.HasValue || number >= setting.MinValue) &&
                    (!setting.MaxValue.HasValue || number <= setting.MaxValue);
            case SettingValueType.Decimal:
                if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount)) return false;
                return (!setting.MinValue.HasValue || amount >= setting.MinValue) &&
                    (!setting.MaxValue.HasValue || amount <= setting.MaxValue);
            case SettingValueType.Bool:
                return bool.TryParse(raw, out _);
            case SettingValueType.Json:
                try { using var _ = JsonDocument.Parse(raw); return true; }
                catch (JsonException) { return false; }
            case SettingValueType.String:
                return true;
            default:
                return false;
        }
    }

    private static T ConvertValue<T>(string value)
    {
        object result = typeof(T) == typeof(string) ? value
            : typeof(T) == typeof(int) ? int.Parse(value, CultureInfo.InvariantCulture)
            : typeof(T) == typeof(decimal) ? decimal.Parse(value, CultureInfo.InvariantCulture)
            : typeof(T) == typeof(bool) ? bool.Parse(value)
            : JsonSerializer.Deserialize<T>(value) ?? throw new InvalidOperationException("Invalid JSON setting.");
        return (T)result;
    }
}
