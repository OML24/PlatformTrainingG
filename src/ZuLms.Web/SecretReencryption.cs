using Microsoft.EntityFrameworkCore;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Infrastructure.Persistence;

internal static class SecretReencryption
{
    public static async Task RunAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Decrypt every value before writing any of them: a missing old key fails
        // safely, and the database transaction leaves all ciphertext unchanged.
        var users = await db.Users.IgnoreQueryFilters().ToListAsync();
        var settings = await db.SystemSettings.Where(x => x.IsSecret && x.Value != "").ToListAsync();
        var passwords = users.Select(x => (
            User: x,
            Current: string.IsNullOrEmpty(x.PasswordEnc) ? null : encryption.Decrypt(x.PasswordEnc),
            Initial: x.InitialPasswordEnc is null ? null : encryption.Decrypt(x.InitialPasswordEnc)))
            .ToList();
        var secretSettings = settings.Select(x => (Setting: x, Plain: encryption.Decrypt(x.Value)))
            .ToList();

        foreach (var item in passwords)
        {
            if (item.Current is not null)
                item.User.PasswordEnc = encryption.Encrypt(item.Current);
            if (item.Initial is not null)
                item.User.InitialPasswordEnc = encryption.Encrypt(item.Initial);
        }
        foreach (var item in secretSettings)
            item.Setting.Value = encryption.Encrypt(item.Plain);

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        Console.WriteLine($"Re-encrypted {passwords.Count} user records and {secretSettings.Count} secret settings.");
    }
}
