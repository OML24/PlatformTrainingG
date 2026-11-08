using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Helpers;
using QuestPDF.Drawing;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Features.Sync;
using ZuLms.Infrastructure.Persistence;
using ZuLms.Infrastructure.Security;
using ZuLms.Infrastructure.Time;
using ZuLms.Infrastructure.Persistence.Seed;
using ZuLms.Infrastructure.Email;
using ZuLms.Infrastructure.Background;
using ZuLms.Infrastructure.University;
using ZuLms.Infrastructure.Files;
using ZuLms.Infrastructure.Pdf;
using ZuLms.Infrastructure.Qr;

namespace ZuLms.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        string connection = Environment.GetEnvironmentVariable("ZU_DB_CONNECTION")
            ?? throw new InvalidOperationException("ZU_DB_CONNECTION is required.");
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connection, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", AppDbContext.Schema))
                .UseSnakeCaseNamingConvention());
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        RegisterFonts();
        services.AddScoped<ICurrentUser, CurrentUserAccessor>();
        services.AddScoped<IAuditLogger, EfAuditLogger>();
        services.AddScoped<DbSeeder>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IEmailSender, MailKitEmailSender>();
        services.AddHostedService<EmailOutboxWorker>();
        services.AddHostedService<OrphanFileCleanupWorker>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<ICredentialReauthCache, PostgresCredentialReauthCache>();
        services.AddHttpClient<IUniversityApiClient, UniversityApiClient>(client =>
            client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddSingleton<UniversitySyncLock>();
        services.AddSingleton<IFileStorage, DiskFileStorage>();
        services.AddSingleton<IQrCodeService, QrCodeService>();
        services.AddSingleton<ICertificatePdfRenderer, QuestPdfCertificateRenderer>();
        services.AddScoped<UniversityMergeService>();
        services.AddScoped<UniversityProvisioningService>();
        services.AddSingleton<UniversitySyncCoordinator>();
        services.AddSingleton<IUniversitySyncService>(provider =>
            provider.GetRequiredService<UniversitySyncCoordinator>());
        services.AddHostedService(provider => provider.GetRequiredService<UniversitySyncCoordinator>());
        services.AddSingleton<IEncryptionService>(_ => new AesGcmEncryptionService(
            Environment.GetEnvironmentVariable("ZU_ENC_KEYS")
            ?? throw new InvalidOperationException("ZU_ENC_KEYS is required.")));
        return services;
    }

    /// <summary>Registers the embedded Tajawal faces used for Arabic certificate rendering.</summary>
    private static void RegisterFonts()
    {
        var assembly = typeof(DependencyInjection).Assembly;
        foreach (string name in assembly.GetManifestResourceNames()
            .Where(x => x.Contains("Tajawal", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is not null) FontManager.RegisterFontFromStream(stream);
        }
    }
}
