using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using ZuLms.Infrastructure;
using ZuLms.Infrastructure.Persistence;
using ZuLms.Infrastructure.Persistence.Seed;
using ZuLms.Domain.Enums;
using ZuLms.Domain.Entities.Identity;
using ZuLms.Domain.Entities.Org;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Features.Auth;
using ZuLms.Application.Features.Permissions;
using ZuLms.Application.Features.Settings;
using ZuLms.Application.Features.Users;
using ZuLms.Application.Features.Credentials;
using ZuLms.Application.Features.Sync;
using ZuLms.Application.Features.Courses;
using ZuLms.Application.Features.Files;
using ZuLms.Application.Features.Needs;
using ZuLms.Application.Features.Vault;
using ZuLms.Domain.Entities.Sync;
using ZuLms.Domain.Entities.Training;
using ZuLms.Domain.Entities.Files;
using ZuLms.Domain.Entities.Certificates;
using ZuLms.Domain.Entities.Evaluation;
using ZuLms.Infrastructure.University;
using Xunit.Abstractions;

namespace ZuLms.Application.Tests;

public sealed class PostgresSeedTests(ITestOutputHelper output)
{
    [Fact]
    public async Task MigrationAndSeedAreIdempotentOnPostgres()
    {
        PostgreSqlContainer? container = null;
        string? connection = null;
        bool usedContainer = false;
        try
        {
            try
            {
                container = new PostgreSqlBuilder("postgres:17").Build();
                await container.StartAsync();
                connection = container.GetConnectionString();
                usedContainer = true;
            }
            catch (Exception) when (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ZU_TEST_PG")) &&
                Environment.GetEnvironmentVariable("ZU_REQUIRE_PG17_CONTAINER") != "1")
            {
                if (container is not null) await container.DisposeAsync();
                container = null;
                connection = Environment.GetEnvironmentVariable("ZU_TEST_PG");
                output.WriteLine("Docker unavailable; using ZU_TEST_PG fallback. PostgreSQL 17 container gate remains pending.");
            }
            Assert.False(string.IsNullOrWhiteSpace(connection));
            if (Environment.GetEnvironmentVariable("ZU_REQUIRE_PG17_CONTAINER") == "1") Assert.True(usedContainer);

            var connectionBuilder = new NpgsqlConnectionStringBuilder(connection)
            {
                SearchPath = AppDbContext.Schema
            };
            await using (var probe = new NpgsqlConnection(connectionBuilder.ConnectionString))
            {
                await probe.OpenAsync();
                await using var command = new NpgsqlCommand("SELECT current_setting('server_version_num')", probe);
                int version = int.Parse((string)(await command.ExecuteScalarAsync())!);
                Assert.True(version >= 170000);
                if (usedContainer) Assert.InRange(version, 170000, 179999);
            }

            string? oldConnection = Environment.GetEnvironmentVariable("ZU_DB_CONNECTION");
            string? oldKeys = Environment.GetEnvironmentVariable("ZU_ENC_KEYS");
            try
            {
                Environment.SetEnvironmentVariable("ZU_DB_CONNECTION", connectionBuilder.ConnectionString);
                Environment.SetEnvironmentVariable("ZU_ENC_KEYS", JsonSerializer.Serialize(new
                {
                    current = "test",
                    keys = new Dictionary<string, string>
                    {
                        ["test"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                    }
                }));
                var registration = new ServiceCollection().AddLogging().AddInfrastructure();
                registration.AddScoped<IPermissionService, PermissionService>();
                registration.AddScoped<IActiveModeAccessor, NoActiveModeAccessor>();
                registration.AddScoped<ISettingsService, SettingsService>();
                registration.AddScoped<IEligibilitySnapshotService, EligibilitySnapshotService>();
                registration.AddScoped<IEligibilityRecalculationService, EligibilityRecalculationService>();
                registration.AddScoped<ZuLms.Application.Features.Certificates.ICertificateService,
                    FakeCertificateService>();
                registration.AddScoped<IAuthService, AuthService>();
                registration.AddScoped<IUserService, UserService>();
                registration.AddScoped<ICredentialService, CredentialService>();
                registration.AddScoped<IPasswordPolicyService, PasswordPolicyService>();
                registration.AddScoped<ICurrentUser, FakeCurrentUser>();
                registration.AddSingleton<ITokenService, FakeTokenService>();
                registration.AddSingleton<IUserStateCache, FakeUserStateCache>();
                registration.AddSingleton<IEmailContentBuilder, FakeEmailContentBuilder>();
                var services = registration.BuildServiceProvider();
                await using var scope = services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync();
                var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
                await seeder.SeedAsync(CancellationToken.None);
                int firstPermissions = await db.Permissions.CountAsync();
                int firstRoles = await db.Roles.CountAsync();
                await seeder.SeedAsync(CancellationToken.None);
                Assert.Equal(firstPermissions, await db.Permissions.CountAsync());
                Assert.Equal(firstRoles, await db.Roles.CountAsync());
                Assert.Equal(138, firstPermissions);
                Assert.Equal(6, firstRoles);

                await using var reviewTransaction = await db.Database.BeginTransactionAsync();
                string reviewKey = Guid.NewGuid().ToString("N");
                await db.InsertSharedEmailReviewIfAbsentAsync(reviewKey,
                    "{\"email\":\"shared@example.test\"}", CancellationToken.None);
                await db.InsertSharedEmailReviewIfAbsentAsync(reviewKey,
                    "{\"email\":\"shared@example.test\"}", CancellationToken.None);
                var review = await db.SyncReviewItems.SingleAsync(x => x.EntityKey == reviewKey);
                Assert.Null(review.FirstSeenRunId);
                Assert.Equal(SyncReviewOrigin.ForgotPassword, review.Origin);
                Assert.Equal(1, await db.SyncReviewItems.CountAsync(x => x.EntityKey == reviewKey));
                await reviewTransaction.RollbackAsync();
                db.ChangeTracker.Clear();

                await using var authTransaction = await db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);
                int sideCode = RandomNumberGenerator.GetInt32(700000, 800000);
                db.UniversitySides.Add(new UniversitySide { SideCode = sideCode, NameAr = "جهة تجريبية" });
                await db.SaveChangesAsync();
                string sharedEmail = $"shared-{Guid.NewGuid():N}@example.test";
                int employeeCode = RandomNumberGenerator.GetInt32(2000000, 3000000);
                var firstEmployee = new Employee
                {
                    SideCode = sideCode,
                    EmployeeCode = employeeCode,
                    FullNameAr = "موظف تجريبي واحد",
                    EmpStatus = 13,
                    SourceEmail = sharedEmail,
                    SearchText = "موظف تجريبي واحد"
                };
                var secondEmployee = new Employee
                {
                    SideCode = sideCode,
                    EmployeeCode = employeeCode + 1,
                    FullNameAr = "موظف تجريبي اثنان",
                    EmpStatus = 13,
                    SourceEmail = sharedEmail,
                    SearchText = "موظف تجريبي اثنان"
                };
                db.Employees.AddRange(firstEmployee, secondEmployee);
                await db.SaveChangesAsync();
                var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
                var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
                const string initialPassword = "InitialPassword123!";
                var firstUser = new User
                {
                    AccountType = AccountType.Employee,
                    LoginName = employeeCode.ToString(),
                    EmployeeId = firstEmployee.Id,
                    FullNameAr = firstEmployee.FullNameAr,
                    SearchText = firstEmployee.SearchText,
                    PasswordHash = hasher.Hash(initialPassword),
                    PasswordEnc = encryption.Encrypt(initialPassword),
                    InitialPasswordEnc = encryption.Encrypt(initialPassword),
                    MustChangePassword = true,
                    SecurityStamp = Guid.NewGuid(),
                    IsActive = true
                };
                var secondUser = new User
                {
                    AccountType = AccountType.Employee,
                    LoginName = (employeeCode + 1).ToString(),
                    EmployeeId = secondEmployee.Id,
                    FullNameAr = secondEmployee.FullNameAr,
                    SearchText = secondEmployee.SearchText,
                    PasswordHash = hasher.Hash(initialPassword),
                    PasswordEnc = encryption.Encrypt(initialPassword),
                    InitialPasswordEnc = encryption.Encrypt(initialPassword),
                    MustChangePassword = true,
                    SecurityStamp = Guid.NewGuid(),
                    IsActive = true
                };
                db.Users.AddRange(firstUser, secondUser);
                await db.SaveChangesAsync();
                var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
                var limited = await auth.LoginAsync(new LoginRequest(firstUser.LoginName,
                    initialPassword, true), CancellationToken.None);
                Assert.True(limited.Ok);
                Assert.True(limited.Value!.PasswordChangeOnly);
                Assert.Null(limited.Value.RefreshToken);
                Assert.Null(limited.Value.RefreshExpiresAt);
                var sharedSetting = await db.SystemSettings.SingleAsync(x =>
                    x.SettingKey == "auth.shared_initial_password");
                const string sharedInitial = "SharedInitialPassword246!";
                sharedSetting.Value = encryption.Encrypt(sharedInitial);
                await db.SaveChangesAsync();
                scope.ServiceProvider.GetRequiredService<ISettingsService>()
                    .Invalidate("auth.shared_initial_password");
                var sharedLimited = await auth.LoginAsync(new LoginRequest(secondUser.LoginName,
                    sharedInitial, true), CancellationToken.None);
                Assert.True(sharedLimited.Ok);
                Assert.True(sharedLimited.Value!.PasswordChangeOnly);
                Assert.Null(sharedLimited.Value.RefreshToken);

                await auth.ForgotPasswordAsync(firstUser.LoginName, sharedEmail, "127.0.0.1", CancellationToken.None);
                Assert.Equal("SharedEmail", (await db.ForgotPasswordAttempts.OrderByDescending(x => x.Id)
                    .FirstAsync()).Outcome);
                Assert.False(await db.EmailOutboxs.AnyAsync(x => x.RelatedUserId == firstUser.Id));
                string sharedKey = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(sharedEmail))).ToLowerInvariant();
                var sharedReview = await db.SyncReviewItems.SingleAsync(x => x.EntityKey == sharedKey);
                Assert.Equal(SyncReviewOrigin.ForgotPassword, sharedReview.Origin);
                Assert.Null(sharedReview.FirstSeenRunId);

                secondEmployee.SourceEmail = $"other-{Guid.NewGuid():N}@example.test";
                await db.SaveChangesAsync();
                await auth.ForgotPasswordAsync(firstUser.LoginName, sharedEmail, "127.0.0.1", CancellationToken.None);
                Assert.True(await db.EmailOutboxs.AnyAsync(x => x.RelatedUserId == firstUser.Id &&
                    x.Kind == EmailKind.ForgotPassword));
                int queued = await db.EmailOutboxs.CountAsync(x => x.RelatedUserId == firstUser.Id);
                var mismatch = await auth.ForgotPasswordAsync(firstUser.LoginName,
                    "wrong@example.test", "127.0.0.1", CancellationToken.None);
                Assert.True(mismatch.Ok);
                Assert.Equal("NoMatch", (await db.ForgotPasswordAttempts.OrderByDescending(x => x.Id)
                    .FirstAsync()).Outcome);
                Assert.Equal(queued, await db.EmailOutboxs.CountAsync(x => x.RelatedUserId == firstUser.Id));
                var rateLimited = await auth.ForgotPasswordAsync(firstUser.LoginName,
                    sharedEmail, "127.0.0.1", CancellationToken.None);
                Assert.True(rateLimited.Ok);
                Assert.Equal("RateLimited", (await db.ForgotPasswordAttempts.OrderByDescending(x => x.Id)
                    .FirstAsync()).Outcome);
                Assert.Equal(queued, await db.EmailOutboxs.CountAsync(x => x.RelatedUserId == firstUser.Id));

                const string replacement = "AnotherSecurePassword987!";
                var changed = await auth.ChangePasswordAsync(firstUser.Id, replacement, replacement,
                    CancellationToken.None);
                Assert.True(changed.Ok);
                Assert.Equal(replacement, encryption.Decrypt(firstUser.PasswordEnc));
                Assert.NotNull(changed.Value!.RefreshToken);
                Assert.False((await auth.LoginAsync(new LoginRequest(firstUser.LoginName,
                    initialPassword, false), CancellationToken.None)).Ok);
                var full = await auth.LoginAsync(new LoginRequest(firstUser.LoginName,
                    replacement, false), CancellationToken.None);
                Assert.True(full.Ok);
                Assert.False(full.Value!.PasswordChangeOnly);
                Assert.NotNull(full.Value.RefreshToken);
                Assert.False((await auth.LoginAsync(new LoginRequest(firstUser.LoginName,
                    sharedInitial, false), CancellationToken.None)).Ok);

                var policy = await db.PasswordPolicys.SingleAsync();
                policy.RotationDays = 1;
                firstUser.PasswordChangedAt = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow.AddDays(-2);
                await db.SaveChangesAsync();
                var due = await auth.LoginAsync(new LoginRequest(firstUser.LoginName,
                    replacement, true), CancellationToken.None);
                Assert.True(due.Ok);
                Assert.True(due.Value!.PasswordChangeOnly);
                Assert.Null(due.Value.RefreshToken);
                const string rotatedPassword = "ThirdSecurePassword753!";
                var rotated = await auth.ChangePasswordAsync(firstUser.Id, rotatedPassword,
                    rotatedPassword, CancellationToken.None);
                Assert.True(rotated.Ok);
                Assert.False(rotated.Value!.PasswordChangeOnly);
                Assert.NotNull(rotated.Value.RefreshToken);

                long adminRoleId = await db.Roles.Where(x => x.Code == RoleCode.Admin)
                    .Select(x => x.Id).SingleAsync();
                db.UserRoles.Add(new UserRole
                {
                    UserId = firstUser.Id,
                    RoleId = adminRoleId,
                    IsActive = true,
                    CreatedAt = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow
                });
                await db.SaveChangesAsync();
                scope.ServiceProvider.GetRequiredService<IPermissionService>().Invalidate(firstUser.Id);
                var currentUser = (FakeCurrentUser)scope.ServiceProvider.GetRequiredService<ICurrentUser>();
                currentUser.Id = firstUser.Id;
                var users = scope.ServiceProvider.GetRequiredService<IUserService>();
                var details = await users.GetDetailsAsync(firstUser.Id, secondUser.Id, CancellationToken.None);
                Assert.True(details.Ok);
                Assert.Equal(secondUser.Version, details.Value!.Version);
                Assert.Equal(secondEmployee.SourceEmail, details.Value.EffectiveEmail);
                Assert.Equal("REASON_REQUIRED", (await users.EditAsync(firstUser.Id, secondUser.Id,
                    secondUser.Version, "Test Name", "override@example.test", UiLanguage.En,
                    true, null, CancellationToken.None)).Error?.Code);
                Assert.True((await users.EditAsync(firstUser.Id, secondUser.Id,
                    secondUser.Version, "Test Name", "override@example.test", UiLanguage.En,
                    true, "اختبار", CancellationToken.None)).Ok);
                Assert.Equal("override@example.test", secondUser.EmailOverride);
                Assert.True(secondUser.AccessOverrideAllow);
                Assert.Equal(UiLanguage.En, secondUser.PreferredLanguage);
                Assert.True((await users.EditAsync(firstUser.Id, secondUser.Id,
                    secondUser.Version, null, null, UiLanguage.Ar,
                    false, null, CancellationToken.None)).Ok);
                var lastAdminDisabled = await users.DeactivateAsync(firstUser.Id, firstUser.Id,
                    firstUser.Version, "اختبار", CancellationToken.None);
                Assert.Equal("LAST_ADMIN", lastAdminDisabled.Error?.Code);
                var lastAdminRevoked = await users.AssignRoleAsync(firstUser.Id, firstUser.Id,
                    RoleCode.Admin, null, false, firstUser.Version, CancellationToken.None);
                Assert.Equal("LAST_ADMIN", lastAdminRevoked.Error?.Code);
                var grantQuality = await users.AssignRoleAsync(firstUser.Id, secondUser.Id,
                    RoleCode.Quality, null, true, secondUser.Version, CancellationToken.None);
                Assert.True(grantQuality.Ok);
                var disabled = await users.DeactivateAsync(firstUser.Id, secondUser.Id,
                    secondUser.Version, "اختبار", CancellationToken.None);
                Assert.True(disabled.Ok);
                Assert.False(secondUser.IsActive);
                var restored = await users.RestoreAsync(firstUser.Id, secondUser.Id,
                    secondUser.Version, CancellationToken.None);
                Assert.True(restored.Ok);
                Assert.True(secondUser.IsActive);
                var selfDeny = await users.SetPermissionOverrideAsync(firstUser.Id, firstUser.Id,
                    "page.admin.users", OverrideMode.Deny, firstUser.Version, CancellationToken.None);
                Assert.Equal("LAST_ADMIN_GUARD", selfDeny.Error?.Code);
                var permissionService = scope.ServiceProvider.GetRequiredService<IPermissionService>();
                Assert.False(await permissionService.HasAsync(secondUser.Id, "page.admin.users", CancellationToken.None));
                var allowed = await users.SetPermissionOverrideAsync(firstUser.Id, secondUser.Id,
                    "page.admin.users", OverrideMode.Allow, secondUser.Version, CancellationToken.None);
                Assert.True(allowed.Ok);
                Assert.False(await permissionService.HasAsync(secondUser.Id, "page.admin.users", CancellationToken.None));
                var permissionRows = await users.ListPermissionsAsync(firstUser.Id, secondUser.Id,
                    CancellationToken.None);
                Assert.True(permissionRows.Ok);
                var usersPermission = Assert.Single(permissionRows.Value!, x => x.Key == "page.admin.users");
                Assert.Equal(OverrideMode.Allow, usersPermission.Mode);
                Assert.False(usersPermission.Effective);
                var denied = await users.SetPermissionOverrideAsync(firstUser.Id, secondUser.Id,
                    "page.admin.users", OverrideMode.Deny, secondUser.Version, CancellationToken.None);
                Assert.True(denied.Ok);
                Assert.False(await permissionService.HasAsync(secondUser.Id, "page.admin.users", CancellationToken.None));
                var inherited = await users.SetPermissionOverrideAsync(firstUser.Id, secondUser.Id,
                    "page.admin.users", null, secondUser.Version, CancellationToken.None);
                Assert.True(inherited.Ok);
                Assert.False(await permissionService.HasAsync(secondUser.Id, "page.admin.users", CancellationToken.None));
                var credentials = scope.ServiceProvider.GetRequiredService<ICredentialService>();
                Assert.Equal("REAUTH_REQUIRED", (await credentials.RevealAsync(firstUser.Id,
                    secondUser.Id, CancellationToken.None)).Error?.Code);
                Assert.Equal("AUTH_INVALID", (await credentials.ReauthenticateAsync(firstUser.Id,
                    "incorrect", CancellationToken.None)).Error?.Code);
                Assert.True((await credentials.ReauthenticateAsync(firstUser.Id,
                    rotatedPassword, CancellationToken.None)).Ok);
                var revealed = await credentials.RevealAsync(firstUser.Id, secondUser.Id,
                    CancellationToken.None);
                Assert.True(revealed.Ok);
                Assert.Equal(initialPassword, revealed.Value!.InitialPassword);
                Assert.Equal(initialPassword, revealed.Value.CurrentPassword);
                const string adminChangedPassword = "AdminChangedPassword864!";
                Assert.True((await credentials.ChangeAsync(firstUser.Id, secondUser.Id,
                    secondUser.Version, adminChangedPassword, true, CancellationToken.None)).Ok);
                var revealedAfterChange = await credentials.RevealAsync(firstUser.Id,
                    secondUser.Id, CancellationToken.None);
                Assert.Equal(adminChangedPassword, revealedAfterChange.Value!.CurrentPassword);
                var afterAdminChange = await auth.LoginAsync(new LoginRequest(secondUser.LoginName,
                    adminChangedPassword, false), CancellationToken.None);
                Assert.True(afterAdminChange.Ok);
                Assert.True(afterAdminChange.Value!.PasswordChangeOnly);
                Assert.Null(afterAdminChange.Value.RefreshToken);
                var preview = await credentials.PreviewBulkInitialAsync(firstUser.Id, CancellationToken.None);
                Assert.True(preview.Ok);
                Assert.Equal(1, preview.Value!.Eligible);
                Assert.True((await credentials.QueueInitialAsync(firstUser.Id, secondUser.Id,
                    false, CancellationToken.None)).Ok);
                Assert.Equal("ALREADY_PENDING", (await credentials.QueueInitialAsync(firstUser.Id,
                    secondUser.Id, false, CancellationToken.None)).Error?.Code);
                var initialMessage = await db.EmailOutboxs.SingleAsync(x =>
                    x.RelatedUserId == secondUser.Id && x.Kind == EmailKind.InitialCredentials);
                Assert.Contains(secondUser.LoginName, initialMessage.BodyHtml);
                Assert.Contains(adminChangedPassword, initialMessage.BodyHtml);
                Assert.Equal(0, (await credentials.PreviewBulkInitialAsync(firstUser.Id,
                    CancellationToken.None)).Value!.Eligible);
                initialMessage.Status = OutboxStatus.Uncertain;
                secondUser.CredentialsEmailStatus = CredentialsEmailStatus.Uncertain;
                await db.SaveChangesAsync();
                Assert.Equal("UNCERTAIN_DELIVERY", (await credentials.QueueInitialAsync(firstUser.Id,
                    secondUser.Id, true, CancellationToken.None)).Error?.Code);
                Assert.True((await credentials.ResolveUncertainAsync(firstUser.Id, secondUser.Id,
                    false, CancellationToken.None)).Ok);
                Assert.Equal(CredentialsEmailStatus.Failed, secondUser.CredentialsEmailStatus);
                Assert.Equal(1, (await credentials.PreviewBulkInitialAsync(firstUser.Id,
                    CancellationToken.None)).Value!.Eligible);
                var bulk = await credentials.QueueBulkInitialAsync(firstUser.Id, CancellationToken.None);
                Assert.True(bulk.Ok);
                Assert.Equal(1, bulk.Value!.Eligible);
                Assert.Equal(2, await db.EmailOutboxs.CountAsync(x =>
                    x.RelatedUserId == secondUser.Id && x.Kind == EmailKind.InitialCredentials));
                Assert.Equal(0, (await credentials.QueueBulkInitialAsync(firstUser.Id,
                    CancellationToken.None)).Value!.Eligible);
                Assert.Equal("LOGIN_INVALID", (await users.CreateLocalAsync(firstUser.Id,
                    "bad login", "مدير محلي", null, "LocalPass864!Alpha", UiLanguage.Ar,
                    CancellationToken.None)).Error?.Code);
                string localLogin = $"local_{Guid.NewGuid():N}"[..20];
                var localCreated = await users.CreateLocalAsync(firstUser.Id, localLogin,
                    "مدير محلي", "Local Administrator", "LocalPass864!Alpha", UiLanguage.Ar,
                    CancellationToken.None);
                Assert.True(localCreated.Ok, localCreated.Error?.Code);
                Assert.True(await db.UserRoles.AnyAsync(x => x.UserId == localCreated.Value &&
                    x.Role!.Code == RoleCode.Admin && x.IsActive));
                Assert.Equal("LOGIN_EXISTS", (await users.CreateLocalAsync(firstUser.Id,
                    localLogin, "مدير محلي", null, "LocalPass864!Alpha", UiLanguage.Ar,
                    CancellationToken.None)).Error?.Code);
                var directory = await users.SearchCredentialsAsync(firstUser.Id, localLogin,
                    CredentialsDirectoryFilter.All, 1, CancellationToken.None);
                Assert.True(directory.Ok);
                Assert.Single(directory.Value!.Items);
                Assert.Equal(localLogin, directory.Value.Items[0].LoginName);
                var failedDirectory = await users.SearchCredentialsAsync(firstUser.Id,
                    secondUser.LoginName, CredentialsDirectoryFilter.Failed, 1, CancellationToken.None);
                Assert.Equal(1, failedDirectory.Value!.Total);
                var policies = scope.ServiceProvider.GetRequiredService<IPasswordPolicyService>();
                var oldPolicy = await policies.GetAsync(firstUser.Id, CancellationToken.None);
                Assert.True(oldPolicy.Ok);
                var invalidPolicy = oldPolicy.Value! with { MaxLength = 1 };
                Assert.Equal("POLICY_INVALID", (await policies.UpdateAsync(firstUser.Id,
                    invalidPolicy, CancellationToken.None)).Error?.Code);
                var updatedPolicy = oldPolicy.Value with { RequireUpper = true };
                Assert.True((await policies.UpdateAsync(firstUser.Id, updatedPolicy,
                    CancellationToken.None)).Ok);
                Assert.True((await policies.GetAsync(firstUser.Id, CancellationToken.None)).Value!.RequireUpper);
                string blockedWord = $"sample-{Guid.NewGuid():N}";
                Assert.True((await policies.AddBlockedAsync(firstUser.Id, blockedWord,
                    CancellationToken.None)).Ok);
                var blockedPage = await policies.ListBlockedAsync(firstUser.Id, blockedWord,
                    1, CancellationToken.None);
                Assert.Equal(1, blockedPage.Value!.Total);
                Assert.Single(blockedPage.Value.Items);
                Assert.Equal("BLOCKED_PASSWORD_EXISTS", (await policies.AddBlockedAsync(firstUser.Id,
                    blockedWord.ToUpperInvariant(), CancellationToken.None)).Error?.Code);
                long blockedId = await db.BlockedPasswords.Where(x => x.PasswordNormalized == blockedWord)
                    .Select(x => x.Id).SingleAsync();
                Assert.True((await policies.RemoveBlockedAsync(firstUser.Id, blockedId,
                    CancellationToken.None)).Ok);
                var systemSettings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
                var settingsList = await systemSettings.ListAsync(firstUser.Id, CancellationToken.None);
                Assert.True(settingsList.Ok);
                var sharedInitialSetting = Assert.Single(settingsList.Value!, x =>
                    x.Key == "auth.shared_initial_password");
                Assert.Null(sharedInitialSetting.Value);
                Assert.Equal("USE_CREDENTIAL_SERVICE", (await systemSettings.SetAsync(
                    "auth.shared_initial_password", "test", sharedInitialSetting.Version,
                    firstUser.Id, CancellationToken.None)).Error?.Code);
                var run = new SyncRun
                {
                    StartedAt = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow,
                    Status = SyncStatus.Partial,
                    StatsJson = "{}"
                };
                db.SyncRuns.Add(run);
                await db.SaveChangesAsync();
                var merge = new UniversityMergeService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(), systemSettings,
                    scope.ServiceProvider.GetRequiredService<IUserStateCache>());
                int centerCode = RandomNumberGenerator.GetInt32(5000, 8000);
                var centerSource = new CenterSource(320, centerCode, "كلية تجريبية", 8, null, null);
                Assert.Equal(1, (await merge.ApplyCentersAsync([centerSource], run.Id,
                    CancellationToken.None)).Added);
                var center = await db.Centers.SingleAsync(x => x.SideCode == 320 &&
                    x.CenterCode == centerCode);
                center.IsFaculty = false;
                center.ShortCode = "ZZ";
                await db.SaveChangesAsync();
                Assert.Equal(1, (await merge.ApplyCentersAsync(
                    [centerSource with { NameAr = "كلية تجريبية محدّثة" }], run.Id,
                    CancellationToken.None)).Updated);
                Assert.False(center.IsFaculty);
                Assert.Equal("ZZ", center.ShortCode);
                var anotherCenter = centerSource with { CenterCode = centerCode + 1 };
                Assert.Equal(1, (await merge.ApplyCentersAsync([anotherCenter], run.Id,
                    CancellationToken.None)).Missing);
                Assert.True(center.SourceMissing);
                Assert.True(center.IsActive);
                Assert.True(await db.SyncReviewItems.AnyAsync(x => x.EntityType == "Center" &&
                    x.EntityKey == $"320:{centerCode}" && x.ResolvedAt == null));
                await merge.ApplyCentersAsync([centerSource, anotherCenter], run.Id,
                    CancellationToken.None);
                Assert.False(center.SourceMissing);
                Assert.True(await db.SyncReviewItems.AnyAsync(x => x.EntityType == "Center" &&
                    x.EntityKey == $"320:{centerCode}" && x.Resolution == SyncReviewResolution.Ignore));
                int departmentCode = centerCode + 10000;
                var departmentSource = new DepartmentSource(320, departmentCode,
                    "قسم تجريبي", centerCode, null, null);
                Assert.Equal(1, (await merge.ApplyDepartmentsAsync([departmentSource], run.Id,
                    CancellationToken.None)).Added);
                Assert.True(await db.Departments.AnyAsync(x => x.SideCode == 320 &&
                    x.DepartmentCode == departmentCode && x.CenterId == center.Id));
                int importedEmployeeCode = RandomNumberGenerator.GetInt32(4000000, 5000000);
                var importedEmployee = new EmployeeSource(320, importedEmployeeCode,
                    "سارة", "محمد", null, "حسن", "سارة محمد حسن", "Sara Hasan",
                    "جامعة الزرقاء", centerCode, departmentCode, 1, "تدريسي",
                    null, null, 13, "على رأس العمل", null,
                    new DateOnly(2020, 1, 2), null);
                Assert.Equal(1, (await merge.ApplyEmployeesAsync([importedEmployee], run.Id,
                    CancellationToken.None)).Added);
                var importedRow = await db.Employees.SingleAsync(x => x.SideCode == 320 &&
                    x.EmployeeCode == importedEmployeeCode);
                Assert.True(importedRow.IsAcademic);
                Assert.Equal(center.Id, importedRow.CenterId);
                Assert.Equal("Sara Hasan", importedRow.FullNameEn);
                Assert.Equal(1, (await merge.ApplyEmailsAsync(
                    [new EmailSource(320, importedEmployeeCode, null),
                     new EmailSource(320, importedEmployeeCode, "SARA@EXAMPLE.TEST")],
                    run.Id, CancellationToken.None)).Updated);
                Assert.Equal("sara@example.test", importedRow.SourceEmail);
                Assert.True(await db.SyncReviewItems.AnyAsync(x => x.EntityType == "Employee" &&
                    x.EntityKey == $"320:{importedEmployeeCode}" &&
                    x.Reason == SyncReviewReason.DuplicateSourceKey));
                Assert.Equal(1, (await merge.ApplyEmployeesAsync(
                    [importedEmployee with { EmpStatus = 2 }], run.Id,
                    CancellationToken.None)).Updated);
                Assert.Equal(2, importedRow.EmpStatus);
                Assert.True(await db.SyncReviewItems.AnyAsync(x => x.EntityType == "Employee" &&
                    x.EntityKey == $"320:{importedEmployeeCode}" &&
                    x.Reason == SyncReviewReason.StatusChanged && x.ResolvedAt == null));
                Assert.Equal("sara@example.test", importedRow.SourceEmail);
                var provisioning = new UniversityProvisioningService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(), hasher,
                    scope.ServiceProvider.GetRequiredService<IEncryptionService>(), systemSettings);
                Assert.Equal(1, (await provisioning.ApplyAsync(run.Id, CancellationToken.None)).Created);
                var provisionedUser = await db.Users.SingleAsync(x => x.EmployeeId == importedRow.Id);
                Assert.Equal(CredentialsEmailStatus.NotSent, provisionedUser.CredentialsEmailStatus);
                Assert.True(provisionedUser.MustChangePassword);
                Assert.Null(provisionedUser.FirstLoginCompletedAt);
                Assert.True(await db.UserRoles.AnyAsync(x => x.UserId == provisionedUser.Id &&
                    x.Role!.Code == RoleCode.Member));
                Assert.False(await db.EmailOutboxs.AnyAsync(x => x.RelatedUserId == provisionedUser.Id));
                Assert.Equal(0, (await provisioning.ApplyAsync(run.Id, CancellationToken.None)).Created);
                string duplicatedEmail = $"duplicate-{Guid.NewGuid():N}@example.test";
                firstUser.EmailOverride = duplicatedEmail;
                provisionedUser.EmailOverride = duplicatedEmail;
                await db.SaveChangesAsync();
                Assert.Equal(1, await merge.DetectSharedEmailsAsync(run.Id, CancellationToken.None));
                Assert.True(await db.SyncReviewItems.AnyAsync(x => x.EntityType == "Email" &&
                    x.EntityKey == duplicatedEmail && x.Reason == SyncReviewReason.SharedEmail &&
                    x.ResolvedAt == null));
                provisionedUser.EmailOverride = null;
                await db.SaveChangesAsync();
                Assert.Equal(0, await merge.DetectSharedEmailsAsync(run.Id, CancellationToken.None));
                Assert.True(await db.SyncReviewItems.AnyAsync(x => x.EntityType == "Email" &&
                    x.EntityKey == duplicatedEmail && x.Reason == SyncReviewReason.SharedEmail &&
                    x.Resolution == SyncReviewResolution.Ignore));
                var centerAdmin = new CenterAdminService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    systemSettings, scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                var centerPage = await centerAdmin.ListAsync(firstUser.Id,
                    center.NameAr, 1, CancellationToken.None);
                Assert.True(centerPage.Ok);
                Assert.Contains(centerPage.Value!.Items, x => x.Id == center.Id);
                Assert.Equal("SHORT_CODE_INVALID", (await centerAdmin.UpdateAsync(firstUser.Id,
                    center.Id, center.Version, true, "123", false,
                    CancellationToken.None)).Error?.Code);
                Assert.True((await centerAdmin.UpdateAsync(firstUser.Id, center.Id,
                    center.Version, true, "abc", false, CancellationToken.None)).Ok);
                Assert.Equal("ABC", center.ShortCode);
                Assert.True(center.IsFaculty);
                Assert.False(center.IsActive);
                Assert.Equal("CONFLICT", (await centerAdmin.UpdateAsync(firstUser.Id,
                    center.Id, center.Version - 1, false, null, true,
                    CancellationToken.None)).Error?.Code);
                var syncAdmin = new UniversitySyncAdminService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    systemSettings, users, scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                Assert.True((await syncAdmin.ListSidesAsync(firstUser.Id, CancellationToken.None)).Ok);
                Assert.True((await syncAdmin.SetSideImportedAsync(firstUser.Id, sideCode,
                    true, CancellationToken.None)).Ok);
                Assert.True(await db.UniversitySides.AnyAsync(x => x.SideCode == sideCode && x.IsImported));
                Assert.True((await syncAdmin.SetSideImportedAsync(firstUser.Id, sideCode,
                    false, CancellationToken.None)).Ok);
                Assert.True((await syncAdmin.ListRunsAsync(firstUser.Id, CancellationToken.None)).Ok);
                var openReviews = await syncAdmin.ListReviewsAsync(firstUser.Id,
                    SyncReviewReason.DuplicateSourceKey, 1, CancellationToken.None);
                Assert.True(openReviews.Ok);
                var duplicateReview = Assert.Single(openReviews.Value!.Items, x =>
                    x.EntityKey == $"320:{importedEmployeeCode}");
                Assert.True((await syncAdmin.ResolveReviewAsync(firstUser.Id, duplicateReview.Id,
                    SyncReviewResolution.Ignore, "تم الفحص", CancellationToken.None)).Ok);
                Assert.Equal("REVIEW_ALREADY_RESOLVED", (await syncAdmin.ResolveReviewAsync(
                    firstUser.Id, duplicateReview.Id, SyncReviewResolution.Keep, null,
                    CancellationToken.None)).Error?.Code);
                var course = new Course
                {
                    CourseNo = $"TEST-{Guid.NewGuid():N}"[..20],
                    TitleAr = "دورة تجريبية",
                    TrackId = await db.TrainingTracks.Select(x => x.Id).FirstAsync(),
                    DeliveryMode = DeliveryMode.InPerson,
                    ObjectivesAr = "أهداف تجريبية",
                    TargetAudienceAr = "الموظفون",
                    TotalHours = 2,
                    PlannedStartDate = new DateOnly(2027, 1, 1),
                    PlannedEndDate = new DateOnly(2027, 1, 2),
                    Origin = CourseOrigin.QualityCreated,
                    AudienceType = AudienceType.General,
                    Status = CourseStatus.Approved,
                    StatusChangedAt = DateTimeOffset.UtcNow,
                    InstructorKind = InstructorKind.Internal,
                    InstructorUserId = secondUser.Id,
                    InstructorNameAr = secondUser.FullNameAr,
                    MaxParticipants = 1,
                    SearchText = "دورة تجريبية",
                    IsActive = true
                };
                db.Courses.Add(course);
                await db.SaveChangesAsync();
                var selfRegistration = new SelfRegistrationService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    systemSettings,
                    scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                Assert.Contains((await selfRegistration.ListAvailableAsync(firstUser.Id,
                    1, CancellationToken.None)).Value!.Items, x => x.Id == course.Id);
                Assert.True((await selfRegistration.RegisterAsync(firstUser.Id, course.Id,
                    CancellationToken.None)).Ok);
                Assert.Equal("REG_FULL", (await selfRegistration.RegisterAsync(firstUser.Id,
                    course.Id, CancellationToken.None)).Error?.Code);
                Assert.Equal("REG_IS_INSTRUCTOR", (await selfRegistration.RegisterAsync(secondUser.Id,
                    course.Id, CancellationToken.None)).Error?.Code);
                Assert.True((await centerAdmin.UpdateAsync(firstUser.Id, center.Id,
                    center.Version, true, "ABC", true, CancellationToken.None)).Ok);
                Assert.True((await users.AssignRoleAsync(firstUser.Id, firstUser.Id,
                    RoleCode.Dean, center.Id, true, firstUser.Version, CancellationToken.None)).Ok);
                var proposalService = new CourseProposalService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    systemSettings, scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                var proposalInput = new CourseProposalInput("دورة مقترحة", null,
                    course.TrackId, DeliveryMode.Blended, "تطوير مهارات", null,
                    "أعضاء الكلية", null, 3.5m, new DateOnly(2027, 3, 1),
                    new DateOnly(2027, 3, 2), AudienceType.Faculty, null, 20,
                    false, provisionedUser.Id, null, null, null, null, null, null, null);
                var proposed = await proposalService.ProposeAsync(firstUser.Id, center.Id,
                    proposalInput, CancellationToken.None);
                Assert.True(proposed.Ok, proposed.Error?.Code);
                var proposal = await db.Courses.SingleAsync(x => x.Id == proposed.Value);
                Assert.StartsWith("C-", proposal.CourseNo);
                Assert.Equal(CourseStatus.Proposed, proposal.Status);
                Assert.Equal(1, await db.CourseProposalRevisions.CountAsync(x => x.CourseId == proposal.Id));
                var proposalQueries = new CourseProposalQueryService(db,
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(), systemSettings);
                Assert.Contains((await proposalQueries.ListMineAsync(firstUser.Id, 1,
                    CancellationToken.None)).Value!.Items, x => x.Id == proposal.Id);
                Assert.Contains((await proposalQueries.ListPendingAsync(secondUser.Id, 1,
                    CancellationToken.None)).Value!.Items, x => x.Id == proposal.Id);
                Assert.Contains((await proposalQueries.DeanLookupsAsync(firstUser.Id,
                    CancellationToken.None)).Value!.Centers, x => x.Id == center.Id);
                Assert.Contains((await proposalQueries.SearchFacultyMembersAsync(firstUser.Id,
                    center.Id, provisionedUser.LoginName, CancellationToken.None)).Value!,
                    x => x.UserId == provisionedUser.Id);
                var returned = await proposalService.DecideAsync(secondUser.Id, proposal.Id,
                    proposal.Version, new CourseProposalDecisionInput(ProposalDecision.Returned,
                        "يرجى تعديل الأهداف", null, null, null, null, null, null, null, null),
                    CancellationToken.None);
                Assert.True(returned.Ok, returned.Error?.Code);
                Assert.Equal(CourseStatus.Returned, proposal.Status);
                var resubmitted = await proposalService.ResubmitAsync(firstUser.Id,
                    proposal.Id, proposal.Version,
                    proposalInput with { ObjectivesAr = "أهداف معدّلة" }, CancellationToken.None);
                Assert.True(resubmitted.Ok, resubmitted.Error?.Code);
                Assert.Equal(2, proposal.CurrentRevisionNo);
                Assert.Equal(2, await db.CourseProposalRevisions.CountAsync(x => x.CourseId == proposal.Id));
                var approved = await proposalService.DecideAsync(secondUser.Id, proposal.Id,
                    proposal.Version, new CourseProposalDecisionInput(ProposalDecision.Approved,
                        null, new DateOnly(2027, 3, 15), AudienceType.Faculty,
                        center.Id, null, 20, false, null, null), CancellationToken.None);
                Assert.True(approved.Ok, approved.Error?.Code);
                Assert.Equal(CourseStatus.Approved, proposal.Status);
                Assert.Equal(4, await db.CourseStatusHistorys.CountAsync(x => x.CourseId == proposal.Id));
                Assert.Equal(2, (await proposalQueries.GetAsync(firstUser.Id, proposal.Id,
                    CancellationToken.None)).Value!.Revisions.Count);
                var externalInput = proposalInput with
                {
                    InternalInstructorUserId = null,
                    ExternalNameAr = "مدرب خارجي",
                    ExternalOrgAr = "مؤسسة تدريب",
                    ExternalEmail = "trainer@example.test"
                };
                Assert.Equal("EXTERNAL_FILES_REQUIRED", (await proposalService.ProposeAsync(
                    firstUser.Id, center.Id, externalInput, CancellationToken.None)).Error?.Code);
                var letterFile = new StoredFile
                {
                    Kind = FileKind.ProposalCouncilLetter,
                    OriginalName = "letter.pdf",
                    StoredName = "letter.pdf",
                    RelativePath = "test/letter.pdf",
                    MimeType = "application/pdf",
                    SizeBytes = 100,
                    Sha256 = new string('a', 64),
                    UploadedBy = firstUser.Id,
                    UploadedAt = DateTimeOffset.UtcNow,
                    IsActive = true
                };
                var cvFile = new StoredFile
                {
                    Kind = FileKind.InstructorCv,
                    OriginalName = "cv.pdf",
                    StoredName = "cv.pdf",
                    RelativePath = "test/cv.pdf",
                    MimeType = "application/pdf",
                    SizeBytes = 100,
                    Sha256 = new string('b', 64),
                    UploadedBy = firstUser.Id,
                    UploadedAt = DateTimeOffset.UtcNow,
                    IsActive = true
                };
                db.StoredFiles.AddRange(letterFile, cvFile);
                await db.SaveChangesAsync();
                externalInput = externalInput with
                {
                    CouncilLetterFileId = letterFile.Id,
                    InstructorCvFileId = cvFile.Id
                };
                var externalProposal = await proposalService.ProposeAsync(firstUser.Id,
                    center.Id, externalInput, CancellationToken.None);
                Assert.True(externalProposal.Ok, externalProposal.Error?.Code);
                var externalCourse = await db.Courses.SingleAsync(x => x.Id == externalProposal.Value);
                Assert.Equal(2, await db.CourseFiles.CountAsync(x => x.CourseId == externalCourse.Id));
                var externalApproved = await proposalService.DecideAsync(secondUser.Id,
                    externalCourse.Id, externalCourse.Version,
                    new CourseProposalDecisionInput(ProposalDecision.Approved, null,
                        new DateOnly(2027, 3, 15), AudienceType.Named, null, null,
                        20, false, ExternalInstructorDecision.RequestNewAccount, null),
                    CancellationToken.None);
                Assert.True(externalApproved.Ok, externalApproved.Error?.Code);
                Assert.True(await db.ExternalInstructorAccountRequests.AnyAsync(x =>
                    x.CourseId == externalCourse.Id &&
                    x.Status == ExternalAccountRequestStatus.PendingAdmin));
                var externalAccountService = new ExternalInstructorAccountService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    systemSettings, hasher, encryption,
                    scope.ServiceProvider.GetRequiredService<IEmailContentBuilder>(),
                    scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                var pendingExternal = await externalAccountService.ListPendingAsync(firstUser.Id,
                    1, CancellationToken.None);
                Assert.True(pendingExternal.Ok);
                var accountRequest = Assert.Single(pendingExternal.Value!.Items, x =>
                    x.CourseId == externalCourse.Id);
                var suggestedLogin = await externalAccountService.SuggestLoginAsync(firstUser.Id,
                    CancellationToken.None);
                Assert.True(suggestedLogin.Ok);
                var completedExternal = await externalAccountService.CompleteAsync(firstUser.Id,
                    accountRequest.Id, suggestedLogin.Value!, null, CancellationToken.None);
                Assert.True(completedExternal.Ok, completedExternal.Error?.Code);
                Assert.Equal(InstructorKind.ExternalAccount, externalCourse.InstructorKind);
                Assert.False(externalCourse.ManagedByQuality);
                Assert.True(await db.ExternalInstructorProfiles.AnyAsync(x =>
                    x.UserId == completedExternal.Value!.UserId &&
                    x.CreatedFromRequestId == accountRequest.Id && x.CvFileId == cvFile.Id));
                Assert.True(await db.EmailOutboxs.AnyAsync(x =>
                    x.RelatedUserId == completedExternal.Value!.UserId &&
                    x.Kind == EmailKind.ExternalInstructorAccount &&
                    x.Status == OutboxStatus.Pending));
                var fileStorage = new MemoryFileStorage();
                var fileUploads = new FileUploadService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    systemSettings, fileStorage,
                    scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                byte[] pdf = "%PDF-1.4\n%%EOF"u8.ToArray();
                Assert.Equal("FILE_EXTENSION_INVALID", (await fileUploads.UploadAsync(
                    firstUser.Id, FileKind.ProposalCouncilLetter, "letter.exe",
                    new MemoryStream(pdf), pdf.Length, CancellationToken.None)).Error?.Code);
                Assert.Equal("FILE_TYPE_INVALID", (await fileUploads.UploadAsync(
                    firstUser.Id, FileKind.ProposalCouncilLetter, "letter.pdf",
                    new MemoryStream("not pdf"u8.ToArray()), 7,
                    CancellationToken.None)).Error?.Code);
                var uploaded = await fileUploads.UploadAsync(firstUser.Id,
                    FileKind.ProposalCouncilLetter, "../letter.pdf", new MemoryStream(pdf),
                    pdf.Length, CancellationToken.None);
                Assert.True(uploaded.Ok, uploaded.Error?.Code);
                Assert.Equal("letter.pdf", uploaded.Value!.OriginalName);
                Assert.True(await db.VaultDocuments.AnyAsync(x => x.FileId == uploaded.Value.Id));
                Assert.Equal("FORBIDDEN", (await fileUploads.DownloadAsync(provisionedUser.Id,
                    uploaded.Value.Id, CancellationToken.None)).Error?.Code);
                db.CourseFiles.Add(new CourseFile
                {
                    CourseId = proposal.Id,
                    FileId = uploaded.Value.Id,
                    Purpose = "ProposalCouncilLetter",
                    RevisionNo = 1,
                    CreatedAt = DateTimeOffset.UtcNow,
                    IsActive = true
                });
                await db.SaveChangesAsync();
                var qualityDownload = await fileUploads.DownloadAsync(secondUser.Id,
                    uploaded.Value.Id, CancellationToken.None);
                Assert.True(qualityDownload.Ok);
                await qualityDownload.Value!.Content.DisposeAsync();
                var downloaded = await fileUploads.DownloadAsync(firstUser.Id,
                    uploaded.Value.Id, CancellationToken.None);
                Assert.True(downloaded.Ok);
                await using (downloaded.Value!.Content)
                {
                    using var contents = new MemoryStream();
                    await downloaded.Value.Content.CopyToAsync(contents);
                    Assert.Equal(pdf, contents.ToArray());
                }
                var directCourses = new DirectCourseService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    systemSettings, scope.ServiceProvider.GetRequiredService<IAuditLogger>(),
                    scope.ServiceProvider.GetRequiredService<IEligibilityRecalculationService>());
                var directInput = new DirectCourseInput("دورة مباشرة", null,
                    course.TrackId, DeliveryMode.Online, "تعلم", null,
                    "كل الموظفين", null, 2.5m, new DateOnly(2027, 5, 1),
                    new DateOnly(2027, 5, 2), AudienceType.General, null,
                    center.Id, null, 30, false, new DateOnly(2027, 5, 15),
                    DirectInstructorChoice.Internal, provisionedUser.Id,
                    null, null, null, null, null, null, null, false, null);
                var directCreated = await directCourses.CreateAsync(firstUser.Id,
                    directInput, CancellationToken.None);
                Assert.True(directCreated.Ok, directCreated.Error?.Code);
                var directCourse = await db.Courses.SingleAsync(x => x.Id == directCreated.Value);
                Assert.Equal(CourseOrigin.AdminCreated, directCourse.Origin);
                Assert.Equal(CourseStatus.Approved, directCourse.Status);
                Assert.True(await db.CourseStatusHistorys.AnyAsync(x =>
                    x.CourseId == directCourse.Id && x.FromStatus == null &&
                    x.ToStatus == CourseStatus.Approved));
                var lectures = new LectureService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    systemSettings, scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                var lectureInput = new LectureInput("محاضرة أولى", null,
                    LectureMode.Online,
                    new DateTime(2027, 5, 1, 9, 0, 0),
                    new DateTime(2027, 5, 1, 11, 0, 0),
                    null, null, "https://teams.example.test/meeting", null);
                Assert.Equal("LECTURE_MODE_INVALID", (await lectures.CreateAsync(
                    firstUser.Id, directCourse.Id,
                    lectureInput with { Mode = LectureMode.InPerson },
                    CancellationToken.None)).Error?.Code);
                var lectureCreated = await lectures.CreateAsync(firstUser.Id,
                    directCourse.Id, lectureInput, CancellationToken.None);
                Assert.True(lectureCreated.Ok, lectureCreated.Error?.Code);
                var lecture = await db.Lectures.SingleAsync(x => x.Id == lectureCreated.Value);
                Assert.Matches("^[0-9]{4,8}$", lecture.AttendanceCode);
                Assert.Equal(TimeSpan.Zero, lecture.StartsAt.Offset);
                string originalCode = lecture.AttendanceCode;
                Assert.True((await lectures.UpdateAsync(firstUser.Id, lecture.Id,
                    new LectureScheduleInput(new DateTime(2027, 5, 1, 10, 0, 0),
                        new DateTime(2027, 5, 1, 12, 0, 0), null, null,
                        "https://teams.example.test/updated"),
                    CancellationToken.None)).Ok);
                Assert.Equal(originalCode, lecture.AttendanceCode);
                Assert.Equal("https://teams.example.test/updated", lecture.TeamsUrl);
                Assert.Contains((await lectures.ListAsync(firstUser.Id, directCourse.Id,
                    CancellationToken.None)).Value!, x => x.Id == lecture.Id);
                var activatedCode = await lectures.ActivateCodeAsync(firstUser.Id,
                    lecture.Id, CancellationToken.None);
                Assert.True(activatedCode.Ok, activatedCode.Error?.Code);
                Assert.Equal(lecture.AttendanceCode, activatedCode.Value!.Code);
                Assert.Equal("ATT_CODE_ACTIVE", (await lectures.ActivateCodeAsync(firstUser.Id,
                    lecture.Id, CancellationToken.None)).Error?.Code);
                var historicInput = directInput with
                {
                    PlannedStartDate = new DateOnly(2020, 1, 1),
                    PlannedEndDate = new DateOnly(2020, 1, 2),
                    EvaluationEndDate = new DateOnly(2020, 1, 10)
                };
                Assert.Equal("COURSE_DATE_PAST", (await directCourses.CreateAsync(firstUser.Id,
                    historicInput, CancellationToken.None)).Error?.Code);
                var historicCreated = await directCourses.CreateAsync(firstUser.Id,
                    historicInput with
                    {
                        IsHistorical = true,
                        HistoricalNotes = "قبل النظام",
                        AudienceType = AudienceType.Administrative,
                        HistoricalParticipants = [new HistoricalParticipantInput(secondUser.Id, true)]
                    },
                    CancellationToken.None);
                Assert.True(historicCreated.Ok, historicCreated.Error?.Code);
                Assert.True(await db.Courses.AnyAsync(x => x.Id == historicCreated.Value &&
                    x.IsHistorical));
                var directExternal = await directCourses.CreateAsync(firstUser.Id,
                    directInput with
                    {
                        InstructorChoice = DirectInstructorChoice.NewExternalAccount,
                        InstructorUserId = null,
                        ExternalNameAr = "خبير جديد",
                        ExternalOrgAr = "مركز تدريب",
                        ExternalEmail = "new@example.test",
                        CouncilLetterFileId = letterFile.Id,
                        InstructorCvFileId = cvFile.Id
                    }, CancellationToken.None);
                Assert.True(directExternal.Ok, directExternal.Error?.Code);
                Assert.True(await db.ExternalInstructorAccountRequests.AnyAsync(x =>
                    x.CourseId == directExternal.Value &&
                    x.Status == ExternalAccountRequestStatus.PendingAdmin));
                var manualParticipants = new ManualParticipantService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                Assert.Equal("PARTICIPANTS_INVALID", (await manualParticipants.AddAsync(
                    firstUser.Id, directCourse.Id, [provisionedUser.Id], false,
                    CancellationToken.None)).Error?.Code);
                Assert.True((await manualParticipants.AddAsync(firstUser.Id,
                    directCourse.Id, [secondUser.Id], false, CancellationToken.None)).Ok);
                var codeEntry = new AttendanceCodeService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    systemSettings, scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                string wrongCode = (lecture.AttendanceCode[0] == '0' ? "1" : "0") +
                    lecture.AttendanceCode[1..];
                Assert.Equal("ATT_CODE_WRONG", (await codeEntry.EnterAsync(secondUser.Id,
                    lecture.Id, wrongCode, CancellationToken.None)).Error?.Code);
                for (int attemptNo = 1; attemptNo < 5; attemptNo++)
                    Assert.Equal("ATT_CODE_WRONG", (await codeEntry.EnterAsync(
                        secondUser.Id, lecture.Id, wrongCode,
                        CancellationToken.None)).Error?.Code);
                Assert.Equal("ATT_LOCKED", (await codeEntry.EnterAsync(secondUser.Id,
                    lecture.Id, lecture.AttendanceCode,
                    CancellationToken.None)).Error?.Code);
                var codeAttempt = await db.AttendanceCodeAttempts.SingleAsync(x =>
                    x.LectureId == lecture.Id && x.UserId == secondUser.Id);
                codeAttempt.LockedUntil = DateTimeOffset.UtcNow.AddSeconds(-1);
                await db.SaveChangesAsync();
                Assert.True((await codeEntry.EnterAsync(secondUser.Id,
                    lecture.Id, lecture.AttendanceCode, CancellationToken.None)).Ok);
                Assert.True(await db.AttendanceRecords.AnyAsync(x =>
                    x.LectureId == lecture.Id && x.IsPresent &&
                    x.Participant!.UserId == secondUser.Id));
                var recalculation = new EligibilityRecalculationService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    new EligibilitySnapshotService(db, systemSettings),
                    scope.ServiceProvider.GetRequiredService<IAuditLogger>(),
                    new FakeCertificateService(db,
                        scope.ServiceProvider.GetRequiredService<IClock>()));
                var manualAttendance = new ManualAttendanceService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    systemSettings,
                    recalculation,
                    scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                var roster = await manualAttendance.RosterAsync(firstUser.Id,
                    lecture.Id, CancellationToken.None);
                Assert.True(roster.Ok, roster.Error?.Code);
                var attendanceRow = Assert.Single(roster.Value!, x =>
                    x.UserId == secondUser.Id && x.IsPresent);
                Assert.Equal("FORBIDDEN", (await manualAttendance.RosterAsync(
                    secondUser.Id, lecture.Id, CancellationToken.None)).Error?.Code);
                Assert.True((await manualAttendance.SetAsync(firstUser.Id,
                    lecture.Id, attendanceRow.ParticipantId, false,
                    "تصحيح الحضور", CancellationToken.None)).Ok);
                Assert.False(await db.AttendanceRecords.AnyAsync(x =>
                    x.LectureId == lecture.Id &&
                    x.ParticipantId == attendanceRow.ParticipantId && x.IsPresent));
                Assert.True(await db.AttendanceChanges.AnyAsync(x =>
                    x.LectureId == lecture.Id &&
                    x.ParticipantId == attendanceRow.ParticipantId &&
                    x.Source == AttendanceSource.Manual && !x.NewPresent));
                Assert.True((await manualAttendance.SetAsync(firstUser.Id,
                    lecture.Id, attendanceRow.ParticipantId, true,
                    null, CancellationToken.None)).Ok);
                var materialUpload = await fileUploads.UploadAsync(firstUser.Id,
                    FileKind.TrainingMaterial, "slides.pdf",
                    new MemoryStream(pdf), pdf.Length, CancellationToken.None);
                Assert.True(materialUpload.Ok, materialUpload.Error?.Code);
                var materials = new LectureMaterialService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                Assert.True((await materials.AttachAsync(firstUser.Id,
                    lecture.Id, materialUpload.Value!.Id,
                    "شرائح المحاضرة", CancellationToken.None)).Ok);
                Assert.Contains((await materials.ListAsync(secondUser.Id,
                    lecture.Id, CancellationToken.None)).Value!, x =>
                    x.FileId == materialUpload.Value.Id);
                var materialDownload = await fileUploads.DownloadAsync(secondUser.Id,
                    materialUpload.Value.Id, CancellationToken.None);
                Assert.True(materialDownload.Ok, materialDownload.Error?.Code);
                await materialDownload.Value!.Content.DisposeAsync();
                var lectureDeactivation = new LectureDeactivationService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    recalculation,
                    scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                Assert.Equal("LECTURE_HAS_ATTENDANCE",
                    (await lectureDeactivation.DeactivateAsync(
                        provisionedUser.Id, lecture.Id, null,
                        CancellationToken.None)).Error?.Code);
                var spareLecture = await lectures.CreateAsync(firstUser.Id,
                    directCourse.Id,
                    lectureInput with { TitleAr = "محاضرة بلا حضور" },
                    CancellationToken.None);
                Assert.True(spareLecture.Ok, spareLecture.Error?.Code);
                Assert.True((await lectureDeactivation.DeactivateAsync(
                    provisionedUser.Id, spareLecture.Value, null,
                    CancellationToken.None)).Ok);
                Assert.False((await db.Lectures.IgnoreQueryFilters()
                    .SingleAsync(x => x.Id == spareLecture.Value)).IsActive);
                var examCourseCreated = await directCourses.CreateAsync(firstUser.Id,
                    directInput with { TitleAr = "دورة مع امتحان", HasExam = true },
                    CancellationToken.None);
                Assert.True(examCourseCreated.Ok, examCourseCreated.Error?.Code);
                long examCourseId = examCourseCreated.Value;
                Assert.True((await manualParticipants.AddAsync(firstUser.Id,
                    examCourseId, [secondUser.Id], false, CancellationToken.None)).Ok);
                var exams = new ExamService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    recalculation,
                    scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                var examCreated = await exams.CreateAsync(firstUser.Id,
                    examCourseId, "امتحان نهائي", CancellationToken.None);
                Assert.True(examCreated.Ok, examCreated.Error?.Code);
                Assert.Equal(examCreated.Value, (await exams.CreateAsync(firstUser.Id,
                    examCourseId, "امتحان نهائي", CancellationToken.None)).Value);
                var examRoster = await exams.GetAsync(firstUser.Id,
                    examCourseId, CancellationToken.None);
                Assert.True(examRoster.Ok, examRoster.Error?.Code);
                var examParticipant = Assert.Single(examRoster.Value!.Participants,
                    x => x.LoginName == secondUser.LoginName);
                Assert.Null(examParticipant.Outcome);
                Assert.True((await exams.RecordAsync(firstUser.Id, examCourseId,
                    examParticipant.ParticipantId, ExamOutcome.Passed,
                    CancellationToken.None)).Ok);
                Assert.True((await exams.RecordAsync(firstUser.Id, examCourseId,
                    examParticipant.ParticipantId, ExamOutcome.Failed,
                    CancellationToken.None)).Ok);
                Assert.True(await db.ExamResultHistorys.AnyAsync(x =>
                    x.OldOutcome == ExamOutcome.Passed &&
                    x.NewOutcome == ExamOutcome.Failed &&
                    x.ExamResult!.ParticipantId == examParticipant.ParticipantId));
                var requirements = new ExamRequirementService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    recalculation,
                    scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                Assert.Equal("EXAM_RESULTS_EXIST", (await requirements.ChangeAsync(
                    secondUser.Id, examCourseId, false, null,
                    CancellationToken.None)).Error?.Code);
                Assert.Equal("DISABLE_REASON_REQUIRED", (await requirements.ChangeAsync(
                    firstUser.Id, examCourseId, false, null,
                    CancellationToken.None)).Error?.Code);
                Assert.True((await requirements.ChangeAsync(firstUser.Id,
                    examCourseId, false, "إلغاء الشرط", CancellationToken.None)).Ok);
                Assert.False(await db.Courses.Where(x => x.Id == examCourseId)
                    .Select(x => x.HasExam).SingleAsync());
                Assert.False(await db.ExamResults.AnyAsync(x =>
                    x.ParticipantId == examParticipant.ParticipantId && x.IsActive));
                Assert.True((await requirements.ChangeAsync(firstUser.Id,
                    examCourseId, true, null, CancellationToken.None)).Ok);
                Assert.True((await exams.CreateAsync(firstUser.Id, examCourseId,
                    "امتحان جديد", CancellationToken.None)).Ok);
                Assert.Null(Assert.Single((await exams.GetAsync(firstUser.Id,
                    examCourseId, CancellationToken.None)).Value!.Participants,
                    x => x.ParticipantId == examParticipant.ParticipantId).Outcome);
                var finishCourseCreated = await directCourses.CreateAsync(firstUser.Id,
                    directInput with { TitleAr = "دورة للإنهاء" },
                    CancellationToken.None);
                Assert.True(finishCourseCreated.Ok, finishCourseCreated.Error?.Code);
                long finishCourseId = finishCourseCreated.Value;
                Assert.True((await manualParticipants.AddAsync(firstUser.Id,
                    finishCourseId, [secondUser.Id], false, CancellationToken.None)).Ok);
                var finishing = new CourseFinishService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    systemSettings,
                    recalculation,
                    scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                Assert.Equal("FINISH_NO_LECTURES", (await finishing.FinishAsync(
                    firstUser.Id, finishCourseId, CancellationToken.None)).Error?.Code);
                var finishLecture = await lectures.CreateAsync(firstUser.Id,
                    finishCourseId, lectureInput, CancellationToken.None);
                Assert.True(finishLecture.Ok, finishLecture.Error?.Code);
                var finished = await finishing.FinishAsync(firstUser.Id,
                    finishCourseId, CancellationToken.None);
                Assert.True(finished.Ok, finished.Error?.Code);
                Assert.Equal(1, finished.Value);
                Assert.True(await db.Courses.AnyAsync(x => x.Id == finishCourseId &&
                    x.Status == CourseStatus.Finished && x.FinalLecturesCount == 1));
                Assert.Equal("COURSE_FINISHED", (await lectures.CreateAsync(firstUser.Id,
                    finishCourseId, lectureInput, CancellationToken.None)).Error?.Code);
                Assert.Equal("COURSE_FINISHED", (await lectures.UpdateAsync(
                    firstUser.Id, finishLecture.Value,
                    new LectureScheduleInput(lectureInput.StartsAt,
                        lectureInput.EndsAt, null, null,
                        "https://teams.example.test/new"),
                    CancellationToken.None)).Error?.Code);
                Assert.True(await db.Notifications.AnyAsync(x =>
                    x.UserId == secondUser.Id &&
                    x.RelatedEntityId == finishCourseId &&
                    x.Type == NotificationType.CourseFinished));
                long finishParticipantId = await db.CourseParticipants
                    .Where(x => x.CourseId == finishCourseId &&
                        x.UserId == secondUser.Id)
                    .Select(x => x.Id).SingleAsync();
                Assert.True((await manualAttendance.SetAsync(firstUser.Id,
                    finishLecture.Value, finishParticipantId, true,
                    "تصحيح بعد الإنهاء", CancellationToken.None)).Ok);
                int nextVersion = (await db.EvaluationFormVersions
                    .Select(x => (int?)x.VersionNo).MaxAsync() ?? 0) + 1;
                var eligibilityVersion = new EvaluationFormVersion
                {
                    VersionNo = nextVersion,
                    Status = FormVersionStatus.Published,
                    TitleAr = "تقييم اختبار الاستحقاق",
                    PublishedAt = DateTimeOffset.UtcNow,
                    PublishedBy = firstUser.Id,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.EvaluationFormVersions.Add(eligibilityVersion);
                await db.SaveChangesAsync();
                var evaluationQuestion = new EvaluationQuestion
                {
                    VersionId = eligibilityVersion.Id,
                    QuestionKey = Guid.NewGuid(),
                    SortOrder = 1,
                    TextAr = "كيف كان التدريب؟"
                };
                db.EvaluationQuestions.Add(evaluationQuestion);
                await db.SaveChangesAsync();
                var evaluationOption = new EvaluationOption
                {
                    QuestionId = evaluationQuestion.Id,
                    SortOrder = 1,
                    LabelAr = "جيد",
                    Score = 4
                };
                db.EvaluationOptions.Add(evaluationOption);
                await db.SaveChangesAsync();
                var finishCourse = await db.Courses.SingleAsync(x =>
                    x.Id == finishCourseId);
                Assert.Null(finishCourse.EvaluationFormVersionId);
                var evaluations = new EvaluationService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    new EligibilitySnapshotService(db, systemSettings),
                    recalculation,
                    scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                var evaluationForm = await evaluations.GetFormAsync(secondUser.Id,
                    finishCourseId, CancellationToken.None);
                Assert.True(evaluationForm.Ok, evaluationForm.Error?.Code);
                Assert.Equal(eligibilityVersion.Id, evaluationForm.Value!.VersionId);
                Assert.Equal("EVAL_ANSWERS_INVALID", (await evaluations.SubmitAsync(
                    secondUser.Id, finishCourseId,
                    [new EvaluationAnswerInput(evaluationQuestion.Id, 0)],
                    CancellationToken.None)).Error?.Code);
                Assert.True((await evaluations.SubmitAsync(secondUser.Id,
                    finishCourseId,
                    [new EvaluationAnswerInput(evaluationQuestion.Id,
                        evaluationOption.Id)], CancellationToken.None)).Ok);
                Assert.Equal("EVAL_ALREADY_SUBMITTED", (await evaluations.GetFormAsync(
                    secondUser.Id, finishCourseId,
                    CancellationToken.None)).Error?.Code);
                var evaluationPinning = new EvaluationPinningService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    new EligibilitySnapshotService(db, systemSettings),
                    scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                var evaluationQueries = new EvaluationQueryService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    scope.ServiceProvider.GetRequiredService<IAuditLogger>(),
                    evaluationPinning);
                var courseResults = await evaluationQueries.CourseResultsAsync(
                    provisionedUser.Id, finishCourseId, CancellationToken.None);
                Assert.True(courseResults.Ok, courseResults.Error?.Code);
                Assert.Equal(finishCourse.InstructorNameAr,
                    courseResults.Value!.InstructorNameAr);
                Assert.Equal(1, courseResults.Value.Responses);
                var questionResult = Assert.Single(courseResults.Value.Questions);
                Assert.Equal(4, questionResult.AverageScore);
                Assert.Equal(100, Assert.Single(questionResult.Options).SharePercent);
                var evaluationAggregate = new EvaluationAggregateService(db,
                    scope.ServiceProvider.GetRequiredService<IPermissionService>());
                Assert.Equal("FORBIDDEN", (await evaluationAggregate.GetAsync(
                    provisionedUser.Id, new EvaluationAggregateFilter(finishCourseId,
                        null, null, null, null, null),
                    CancellationToken.None)).Error?.Code);
                var aggregateResult = await evaluationAggregate.GetAsync(firstUser.Id,
                    new EvaluationAggregateFilter(finishCourseId, null, null,
                        null, null, eligibilityVersion.Id), CancellationToken.None);
                Assert.True(aggregateResult.Ok, aggregateResult.Error?.Code);
                Assert.Equal(1, aggregateResult.Value!.Courses);
                Assert.Equal(1, aggregateResult.Value.Responses);
                var aggregateQuestion = Assert.Single(aggregateResult.Value.Questions);
                Assert.Equal(4, aggregateQuestion.AverageScore);
                Assert.Equal(1, Assert.Single(aggregateQuestion.Options).Count);
                var impacts = new ImpactService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    new EligibilitySnapshotService(db, systemSettings),
                    recalculation,
                    scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                var firstImpactFile = await fileUploads.UploadAsync(secondUser.Id,
                    FileKind.ImpactFile, "impact.pdf", new MemoryStream(pdf),
                    pdf.Length, CancellationToken.None);
                Assert.True(firstImpactFile.Ok, firstImpactFile.Error?.Code);
                var firstImpact = await impacts.SubmitAsync(secondUser.Id,
                    finishCourseId, ImpactType.StudyPlan,
                    firstImpactFile.Value!.Id, CancellationToken.None);
                Assert.True(firstImpact.Ok, firstImpact.Error?.Code);
                Assert.Contains((await impacts.PendingAsync(firstUser.Id,
                    finishCourseId, CancellationToken.None)).Value!, x =>
                    x.Id == firstImpact.Value);
                var instructorDownload = await fileUploads.DownloadAsync(
                    provisionedUser.Id, firstImpactFile.Value.Id,
                    CancellationToken.None);
                Assert.Equal("FORBIDDEN", instructorDownload.Error?.Code);
                var adminImpactDownload = await fileUploads.DownloadAsync(
                    firstUser.Id, firstImpactFile.Value.Id,
                    CancellationToken.None);
                Assert.True(adminImpactDownload.Ok,
                    adminImpactDownload.Error?.Code);
                await adminImpactDownload.Value!.Content.DisposeAsync();
                Assert.Equal("IMPACT_NOTES_REQUIRED", (await impacts.ReviewAsync(
                    firstUser.Id, finishCourseId, firstImpact.Value,
                    false, null, CancellationToken.None)).Error?.Code);
                Assert.True((await impacts.ReviewAsync(firstUser.Id,
                    finishCourseId, firstImpact.Value, false,
                    "يرجى تعديل الملف", CancellationToken.None)).Ok);
                Assert.Equal("IMPACT_SAME_FILE", (await impacts.SubmitAsync(
                    secondUser.Id, finishCourseId, ImpactType.StudyPlan,
                    firstImpactFile.Value.Id, CancellationToken.None)).Error?.Code);
                byte[] revisedImpactPdf = [.. pdf, .. System.Text.Encoding.ASCII
                    .GetBytes("\n% revised")];
                var revisedImpactFile = await fileUploads.UploadAsync(
                    secondUser.Id, FileKind.ImpactFile, "impact-revised.pdf",
                    new MemoryStream(revisedImpactPdf), revisedImpactPdf.Length,
                    CancellationToken.None);
                Assert.True(revisedImpactFile.Ok, revisedImpactFile.Error?.Code);
                var revisedImpact = await impacts.SubmitAsync(secondUser.Id,
                    finishCourseId, ImpactType.StudyPlan,
                    revisedImpactFile.Value!.Id, CancellationToken.None);
                Assert.True(revisedImpact.Ok, revisedImpact.Error?.Code);
                Assert.True((await impacts.ReviewAsync(firstUser.Id,
                    finishCourseId, revisedImpact.Value, true, null,
                    CancellationToken.None)).Ok);
                Assert.False(await db.CertificateTemplates.AnyAsync(x =>
                    x.IsCurrent));
                var pendingCertificate = await recalculation
                    .RecalculateParticipantAsync(finishCourseId,
                        finishParticipantId, null, firstUser.Id,
                        CancellationToken.None);
                Assert.True(pendingCertificate.Ok, pendingCertificate.Error?.Code);
                Assert.True(pendingCertificate.Value!.Eligible);
                Assert.True(pendingCertificate.Value.CertificatePending);
                Assert.Equal("CERT_TEMPLATE_MISSING",
                    pendingCertificate.Value.PendingReason);
                Assert.False(await db.Certificates.AnyAsync(x =>
                    x.ParticipantId == finishParticipantId));
                var progress = new CourseProgressService(db, systemSettings,
                    new EligibilitySnapshotService(db, systemSettings),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>());
                var memberProgress = await progress.GetMineAsync(secondUser.Id,
                    finishCourseId, CancellationToken.None);
                Assert.True(memberProgress.Ok, memberProgress.Error?.Code);
                Assert.True(memberProgress.Value!.IsFinal);
                Assert.Equal(1, memberProgress.Value.AttendedLectures);
                Assert.Equal(1, memberProgress.Value.RequiredLectures);
                Assert.True(memberProgress.Value.Eligible);
                Assert.Equal("FORBIDDEN", (await progress.GetMineAsync(firstUser.Id,
                    finishCourseId, CancellationToken.None)).Error?.Code);
                var instructorProgress = await progress.GetRosterAsync(
                    provisionedUser.Id, finishCourseId, CancellationToken.None);
                Assert.True(instructorProgress.Ok, instructorProgress.Error?.Code);
                Assert.Contains(instructorProgress.Value!, x =>
                    x.ParticipantId == finishParticipantId &&
                    x.Progress.Eligible);
                Assert.Equal("FORBIDDEN", (await progress.GetRosterAsync(
                    secondUser.Id, finishCourseId,
                    CancellationToken.None)).Error?.Code);
                finishCourse.EvaluationEndDate = scope.ServiceProvider
                    .GetRequiredService<IClock>().TodayAmman.AddDays(-1);
                await db.SaveChangesAsync();
                var ammanToday = scope.ServiceProvider
                    .GetRequiredService<IClock>().TodayAmman;
                Assert.Equal("EVAL_PERIOD_NOT_LATER",
                    (await evaluationQueries.ExtendPeriodAsync(secondUser.Id,
                        finishCourseId, ammanToday.AddDays(-1),
                        CancellationToken.None)).Error?.Code);
                Assert.True((await evaluationQueries.ExtendPeriodAsync(secondUser.Id,
                    finishCourseId, ammanToday.AddDays(1),
                    CancellationToken.None)).Ok);
                Assert.Equal(ammanToday.AddDays(1), finishCourse.EvaluationEndDate);
                Assert.True((await evaluationQueries.ExtendPeriodAsync(firstUser.Id,
                    finishCourseId, ammanToday,
                    CancellationToken.None)).Ok);
                Assert.Equal(ammanToday, finishCourse.EvaluationEndDate);
                finishCourse.EvaluationEndDate = ammanToday.AddDays(-1);
                await db.SaveChangesAsync();
                var pinning = new EvaluationPinningService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    new EligibilitySnapshotService(db, systemSettings),
                    scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                Assert.True(await pinning.PinCompletedAsync(CancellationToken.None) >= 1);
                Assert.Equal(eligibilityVersion.Id,
                    finishCourse.EvaluationFormVersionId);
                var formAdmin = new EvaluationFormAdminService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    new EligibilitySnapshotService(db, systemSettings),
                    scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                var draft = await formAdmin.OpenDraftAsync(firstUser.Id,
                    CancellationToken.None);
                Assert.True(draft.Ok, draft.Error?.Code);
                var copiedQuestion = Assert.Single(draft.Value!.Questions);
                Assert.Equal(evaluationQuestion.QuestionKey,
                    copiedQuestion.QuestionKey);
                Assert.True((await formAdmin.SaveDraftAsync(firstUser.Id,
                    draft.Value.Id, new EvaluationDraftInput("نموذج جديد",
                    [new EvaluationDraftQuestionInput(copiedQuestion.Id,
                        false, "سؤال جديد",
                        [new EvaluationDraftOptionInput("ممتاز", 5)])]),
                    CancellationToken.None)).Ok);
                var publishedForm = await formAdmin.PublishAsync(firstUser.Id,
                    draft.Value.Id, CancellationToken.None);
                Assert.True(publishedForm.Ok, publishedForm.Error?.Code);
                Assert.NotEqual(evaluationQuestion.QuestionKey,
                    await db.EvaluationQuestions.Where(x =>
                        x.VersionId == draft.Value.Id)
                        .Select(x => x.QuestionKey).SingleAsync());
                Assert.Equal(eligibilityVersion.Id,
                    finishCourse.EvaluationFormVersionId);
                var version2Question = await db.EvaluationQuestions.SingleAsync(x =>
                    x.VersionId == draft.Value.Id);
                var version3Draft = await formAdmin.OpenDraftAsync(firstUser.Id,
                    CancellationToken.None);
                Assert.True(version3Draft.Ok, version3Draft.Error?.Code);
                Assert.True((await formAdmin.PublishAsync(firstUser.Id,
                    version3Draft.Value!.Id, CancellationToken.None)).Ok);
                var version3Question = await db.EvaluationQuestions.SingleAsync(x =>
                    x.VersionId == version3Draft.Value.Id);
                Assert.Equal(version2Question.QuestionKey,
                    version3Question.QuestionKey);
                var version4Draft = await formAdmin.OpenDraftAsync(firstUser.Id,
                    CancellationToken.None);
                Assert.True(version4Draft.Ok, version4Draft.Error?.Code);
                var version4QuestionInput = Assert.Single(version4Draft.Value!.Questions);
                Assert.True((await formAdmin.SaveDraftAsync(firstUser.Id,
                    version4Draft.Value.Id, new EvaluationDraftInput("نسخة خيار مختلف",
                    [new EvaluationDraftQuestionInput(version4QuestionInput.Id,
                        true, version4QuestionInput.TextAr,
                        [new EvaluationDraftOptionInput("ضعيف", 1)])]),
                    CancellationToken.None)).Ok);
                Assert.True((await formAdmin.PublishAsync(firstUser.Id,
                    version4Draft.Value.Id, CancellationToken.None)).Ok);
                var version4Question = await db.EvaluationQuestions.SingleAsync(x =>
                    x.VersionId == version4Draft.Value.Id);
                Assert.Equal(version2Question.QuestionKey,
                    version4Question.QuestionKey);
                var reportVersions = new[]
                {
                    (VersionId: draft.Value.Id, QuestionId: version2Question.Id),
                    (VersionId: version3Draft.Value.Id, QuestionId: version3Question.Id),
                    (VersionId: version4Draft.Value.Id, QuestionId: version4Question.Id)
                };
                for (int i = 0; i < reportVersions.Length; i++)
                {
                    var planned = new DateOnly(2027, 6, 1).AddDays(i);
                    var reportCourseInput = directInput with
                    {
                        TitleAr = $"تقرير مقارنة {i + 1}",
                        PlannedStartDate = planned,
                        PlannedEndDate = planned,
                        EvaluationEndDate = new DateOnly(2027, 6, 30)
                    };
                    var reportCreated = await directCourses.CreateAsync(firstUser.Id,
                        reportCourseInput, CancellationToken.None);
                    Assert.True(reportCreated.Ok, reportCreated.Error?.Code);
                    Assert.True((await manualParticipants.AddAsync(firstUser.Id,
                        reportCreated.Value, [secondUser.Id], false,
                        CancellationToken.None)).Ok);
                    var reportCourse = await db.Courses.SingleAsync(x =>
                        x.Id == reportCreated.Value);
                    reportCourse.Status = CourseStatus.Finished;
                    reportCourse.FinishedAt = DateTimeOffset.UtcNow;
                    reportCourse.EvaluationFormVersionId = reportVersions[i].VersionId;
                    var reportParticipant = await db.CourseParticipants.SingleAsync(x =>
                        x.CourseId == reportCreated.Value && x.UserId == secondUser.Id);
                    var response = new EvaluationResponse
                    {
                        CourseId = reportCreated.Value,
                        ParticipantId = reportParticipant.Id,
                        VersionId = reportVersions[i].VersionId,
                        SubmittedAt = DateTimeOffset.UtcNow
                    };
                    db.EvaluationResponses.Add(response);
                    await db.SaveChangesAsync();
                    long optionId = await db.EvaluationOptions.Where(x =>
                        x.QuestionId == reportVersions[i].QuestionId)
                        .Select(x => x.Id).SingleAsync();
                    db.EvaluationAnswers.Add(new EvaluationAnswer
                    {
                        ResponseId = response.Id,
                        QuestionId = reportVersions[i].QuestionId,
                        OptionId = optionId
                    });
                    await db.SaveChangesAsync();
                }
                var comparable = await evaluationAggregate.GetAsync(firstUser.Id,
                    new EvaluationAggregateFilter(null, null, directInput.TrackId,
                        new DateOnly(2027, 6, 1), new DateOnly(2027, 6, 3),
                        null), CancellationToken.None);
                Assert.True(comparable.Ok, comparable.Error?.Code);
                Assert.Equal(3, comparable.Value!.Courses);
                Assert.Equal(3, comparable.Value.Responses);
                Assert.Equal(2, comparable.Value.Questions.Count);
                Assert.Contains(comparable.Value.Questions, x => x.Answers == 2 &&
                    x.VersionNumbers.Count == 2);
                Assert.Contains(comparable.Value.Questions, x => x.Answers == 1 &&
                    x.VersionNumbers.Count == 1);
                var participantPage = await manualParticipants.ListAsync(firstUser.Id,
                    directCourse.Id, CancellationToken.None);
                Assert.True(participantPage.Ok, participantPage.Error?.Code);
                Assert.Contains(participantPage.Value!.Participants, x =>
                    x.UserId == secondUser.Id);
                Assert.Equal("REG_ALREADY", (await manualParticipants.AddAsync(firstUser.Id,
                    directCourse.Id, [secondUser.Id], false,
                    CancellationToken.None)).Error?.Code);
                Assert.Equal("REMOVAL_REASON_REQUIRED", (await manualParticipants.RemoveAsync(
                    firstUser.Id, directCourse.Id, secondUser.Id, "", false,
                    CancellationToken.None)).Error?.Code);
                Assert.True((await manualParticipants.RemoveAsync(firstUser.Id,
                    directCourse.Id, secondUser.Id, "اختبار", false,
                    CancellationToken.None)).Ok);
                Assert.True(await db.CourseParticipants.AnyAsync(x => x.CourseId ==
                    directCourse.Id && x.UserId == secondUser.Id && !x.IsActive &&
                    x.RemovalReason == "اختبار"));
                var courseDirectory = new CourseDirectoryService(db,
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    systemSettings);
                var allCourses = await courseDirectory.ListAsync(firstUser.Id,
                    new CourseDirectoryFilter(CourseDirectoryView.All, "دورة مباشرة",
                        CourseStatus.Approved, null, null, null, null, null,
                        true, false), CancellationToken.None);
                Assert.True(allCourses.Ok, allCourses.Error?.Code);
                Assert.Contains(allCourses.Value!.Items, x => x.Id == directCourse.Id);
                var deanCourses = await courseDirectory.ListAsync(firstUser.Id,
                    new CourseDirectoryFilter(CourseDirectoryView.Dean, null,
                        null, null, null, null, null, null, null, null),
                    CancellationToken.None);
                Assert.True(deanCourses.Ok, deanCourses.Error?.Code);
                Assert.Contains(deanCourses.Value!.Items, x => x.Id == proposal.Id);
                long memberRoleId = await db.Roles.Where(x => x.Code == RoleCode.Member)
                    .Select(x => x.Id).SingleAsync();
                db.UserRoles.Add(new UserRole
                {
                    UserId = firstUser.Id,
                    RoleId = memberRoleId,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync();
                scope.ServiceProvider.GetRequiredService<IPermissionService>()
                    .Invalidate(firstUser.Id);
                var enrolledCourses = await courseDirectory.ListAsync(firstUser.Id,
                    new CourseDirectoryFilter(CourseDirectoryView.Member, null,
                        null, null, null, null, null, null, null, null),
                    CancellationToken.None);
                Assert.True(enrolledCourses.Ok, enrolledCourses.Error?.Code);
                Assert.Contains(enrolledCourses.Value!.Items, x => x.Id == course.Id);
                Assert.DoesNotContain(enrolledCourses.Value.Items, x => x.Id == directCourse.Id);
                scope.ServiceProvider.GetRequiredService<IPermissionService>()
                    .Invalidate(provisionedUser.Id);
                var instructorCourses = await courseDirectory.ListAsync(provisionedUser.Id,
                    new CourseDirectoryFilter(CourseDirectoryView.Instructor, null,
                        null, null, null, null, null, null, null, null),
                    CancellationToken.None);
                Assert.True(instructorCourses.Ok, instructorCourses.Error?.Code);
                Assert.Contains(instructorCourses.Value!.Items, x => x.Id == directCourse.Id);
                Assert.Equal("FORBIDDEN", (await courseDirectory.ListAsync(
                    provisionedUser.Id,
                    new CourseDirectoryFilter(CourseDirectoryView.All, null,
                        null, null, null, null, null, null, null, null),
                    CancellationToken.None)).Error?.Code);
                var courseDetails = await courseDirectory.GetAsync(firstUser.Id,
                    directCourse.Id, CourseDirectoryView.All, CancellationToken.None);
                Assert.True(courseDetails.Ok, courseDetails.Error?.Code);
                Assert.Equal("دورة مباشرة", courseDetails.Value!.Course.TitleAr);
                var commonEdit = new CourseCommonEditService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                var editInput = new CourseCommonEditInput(directCourse.Version,
                    directCourse.TitleAr, directCourse.TitleEn,
                    "أهداف محدّثة", directCourse.ObjectivesEn,
                    directCourse.TargetAudienceAr, directCourse.TargetAudienceEn,
                    directCourse.TotalHours, directCourse.PlannedStartDate,
                    directCourse.PlannedEndDate, directCourse.MaxParticipants,
                    directCourse.TrackId, directCourse.DeliveryMode,
                    directCourse.AudienceType, directCourse.TargetCenterId,
                    directCourse.RegistrationDeadline);
                Assert.Equal("FORBIDDEN", (await commonEdit.EditAsync(
                    provisionedUser.Id, directCourse.Id, editInput,
                    CancellationToken.None)).Error?.Code);
                Assert.True((await commonEdit.EditAsync(firstUser.Id,
                    directCourse.Id, editInput, CancellationToken.None)).Ok);
                Assert.Equal("أهداف محدّثة", directCourse.ObjectivesAr);
                Assert.Equal("CONFLICT", (await commonEdit.EditAsync(firstUser.Id,
                    directCourse.Id, editInput, CancellationToken.None)).Error?.Code);
                Assert.True(await db.AuditLogs.AnyAsync(x => x.ActionCode == "course.edit" &&
                    x.EntityId == directCourse.Id));
                var qualityEdit = editInput with
                {
                    ExpectedVersion = directCourse.Version,
                    TrackId = directCourse.TrackId + 999
                };
                Assert.Equal("FORBIDDEN", (await commonEdit.EditAsync(secondUser.Id,
                    directCourse.Id, qualityEdit, CancellationToken.None)).Error?.Code);
                qualityEdit = editInput with
                {
                    ExpectedVersion = directCourse.Version,
                    AudienceType = AudienceType.Faculty,
                    TargetCenterId = center.Id,
                    RegistrationDeadline = new DateTimeOffset(2027, 4, 30,
                        12, 0, 0, TimeSpan.Zero)
                };
                Assert.True((await commonEdit.EditAsync(secondUser.Id,
                    directCourse.Id, qualityEdit, CancellationToken.None)).Ok);
                Assert.Equal(AudienceType.Faculty, directCourse.AudienceType);
                Assert.Equal(center.Id, directCourse.TargetCenterId);
                Assert.Equal(new DateTimeOffset(2027, 4, 30, 12, 0, 0,
                    TimeSpan.Zero), directCourse.RegistrationDeadline);
                var lifecycle = new AdminCourseLifecycleService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    new EligibilitySnapshotService(db, systemSettings),
                    scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                Assert.True((await lifecycle.ArchiveAsync(firstUser.Id,
                    directCourse.Id, directCourse.Version, true,
                    CancellationToken.None)).Ok);
                Assert.True(directCourse.IsArchived);
                Assert.Equal("CONFLICT", (await lifecycle.ArchiveAsync(firstUser.Id,
                    directCourse.Id, 0, false, CancellationToken.None)).Error?.Code);
                Assert.True((await lifecycle.ArchiveAsync(firstUser.Id,
                    directCourse.Id, directCourse.Version, false,
                    CancellationToken.None)).Ok);
                var template = new CertificateTemplate
                {
                    VersionNo = 1,
                    FileId = uploaded.Value.Id,
                    PageWidthPt = 595,
                    PageHeightPt = 842,
                    LayoutJson = "{}",
                    IsCurrent = true,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.CertificateTemplates.Add(template);
                await db.SaveChangesAsync();
                var certificateService = new ZuLms.Application.Features.Certificates.CertificateService(
                    db, scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    systemSettings, new FakeCertificatePdfRenderer(),
                    new FakeQrCodeService(), fileStorage,
                    new EligibilitySnapshotService(db, systemSettings),
                    scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                var forceStatus = new CourseForceStatusService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    new EligibilityRecalculationService(db,
                        scope.ServiceProvider.GetRequiredService<IClock>(),
                        new EligibilitySnapshotService(db, systemSettings),
                        scope.ServiceProvider.GetRequiredService<IAuditLogger>(),
                        certificateService),
                    scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                var thresholdService = new CourseAttendanceThresholdService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    new EligibilityRecalculationService(db,
                        scope.ServiceProvider.GetRequiredService<IClock>(),
                        new EligibilitySnapshotService(db, systemSettings),
                        scope.ServiceProvider.GetRequiredService<IAuditLogger>(),
                        certificateService),
                    scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                Assert.Equal("FORBIDDEN", (await thresholdService.ChangeAsync(
                    provisionedUser.Id, directCourse.Id, directCourse.Version,
                    90, CancellationToken.None)).Error?.Code);
                Assert.Equal("THRESHOLD_INVALID", (await thresholdService.ChangeAsync(
                    firstUser.Id, directCourse.Id, directCourse.Version,
                    101, CancellationToken.None)).Error?.Code);
                Assert.True((await thresholdService.ChangeAsync(secondUser.Id,
                    directCourse.Id, directCourse.Version,
                    90, CancellationToken.None)).Ok);
                Assert.Equal((short)90, directCourse.AttendanceThresholdPercentOverride);
                Assert.Equal("CONFLICT", (await thresholdService.ChangeAsync(
                    firstUser.Id, directCourse.Id, 0, null,
                    CancellationToken.None)).Error?.Code);
                Assert.True((await thresholdService.ChangeAsync(firstUser.Id,
                    directCourse.Id, directCourse.Version,
                    null, CancellationToken.None)).Ok);
                Assert.Null(directCourse.AttendanceThresholdPercentOverride);
                var historicCourse = await db.Courses.SingleAsync(x =>
                    x.Id == historicCreated.Value);
                var instructorEdit = new CourseInstructorEditService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                var instructorInput = new CourseInstructorEditInput(
                    directCourse.Version, CourseInstructorChoice.Internal,
                    firstUser.Id, null, null, null, null);
                Assert.Equal("FORBIDDEN", (await instructorEdit.EditAsync(
                    provisionedUser.Id, directCourse.Id, instructorInput,
                    CancellationToken.None)).Error?.Code);
                Assert.Contains((await instructorEdit.SearchAsync(secondUser.Id,
                    directCourse.Id, false, firstUser.LoginName,
                    CancellationToken.None)).Value!, x => x.UserId == firstUser.Id);
                var instructorEdited = await instructorEdit.EditAsync(secondUser.Id,
                    directCourse.Id, instructorInput, CancellationToken.None);
                Assert.True(instructorEdited.Ok, instructorEdited.Error?.Code);
                Assert.Equal(firstUser.Id, directCourse.InstructorUserId);
                Assert.Equal("CONFLICT", (await instructorEdit.EditAsync(
                    secondUser.Id, directCourse.Id, instructorInput,
                    CancellationToken.None)).Error?.Code);
                Assert.Equal("FORBIDDEN", (await forceStatus.ChangeAsync(
                    provisionedUser.Id, historicCourse.Id, historicCourse.Version,
                    CourseStatus.Rejected, "تصحيح", true,
                    CancellationToken.None)).Error?.Code);
                Assert.Equal("REASON_REQUIRED", (await forceStatus.ChangeAsync(
                    firstUser.Id, historicCourse.Id, historicCourse.Version,
                    CourseStatus.Rejected, "", true,
                    CancellationToken.None)).Error?.Code);
                Assert.Equal("CONFIRM_REQUIRED", (await forceStatus.ChangeAsync(
                    firstUser.Id, historicCourse.Id, historicCourse.Version,
                    CourseStatus.Rejected, "تصحيح", false,
                    CancellationToken.None)).Error?.Code);
                Assert.True((await forceStatus.ChangeAsync(firstUser.Id,
                    historicCourse.Id, historicCourse.Version,
                    CourseStatus.Rejected, "تصحيح خطأ إداري", true,
                    CancellationToken.None)).Ok);
                Assert.Equal(CourseStatus.Rejected, historicCourse.Status);
                Assert.Equal("CONFLICT", (await forceStatus.ChangeAsync(
                    firstUser.Id, historicCourse.Id, 0, CourseStatus.Approved,
                    "تصحيح", true, CancellationToken.None)).Error?.Code);
                Assert.True((await forceStatus.ChangeAsync(firstUser.Id,
                    historicCourse.Id, historicCourse.Version,
                    CourseStatus.Approved, "استعادة الاعتماد", true,
                    CancellationToken.None)).Ok);
                Assert.Contains(await db.CourseStatusHistorys.Where(x =>
                    x.CourseId == historicCourse.Id).ToListAsync(), x =>
                    x.FromStatus == CourseStatus.Rejected &&
                    x.ToStatus == CourseStatus.Approved &&
                    x.Reason!.Contains("استعادة الاعتماد"));
                var forceParticipant = await db.CourseParticipants.SingleAsync(x =>
                    x.CourseId == historicCourse.Id && x.UserId == secondUser.Id);
                var forceCertificate = new Certificate
                {
                    ReferenceNo = $"FORCE-{Guid.NewGuid():N}"[..35],
                    VerificationToken = Guid.NewGuid().ToString("N"),
                    CourseId = historicCourse.Id,
                    ParticipantId = forceParticipant.Id,
                    UserId = secondUser.Id,
                    Status = CertificateStatus.Issued,
                    IssuedAt = DateTimeOffset.UtcNow,
                    TemplateId = template.Id,
                    PdfFileId = uploaded.Value.Id,
                    HolderNameAr = secondUser.FullNameAr,
                    CourseTitleAr = historicCourse.TitleAr,
                    CoursePeriodText = "01/01/2020 – 02/01/2020",
                    CourseHours = historicCourse.TotalHours,
                    InstructorNameAr = historicCourse.InstructorNameAr,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.Certificates.Add(forceCertificate);
                await db.SaveChangesAsync();
                Assert.True((await forceStatus.ChangeAsync(firstUser.Id,
                    historicCourse.Id, historicCourse.Version,
                    CourseStatus.Cancelled, "إلغاء لتصحيح السجل", true,
                    CancellationToken.None)).Ok);
                Assert.Equal(CertificateStatus.Revoked, forceCertificate.Status);
                Assert.Equal(CertificateRevokeReason.CourseCancelled,
                    forceCertificate.RevokedReason);
                Assert.True(historicCourse.IsArchived);
                Assert.Equal("إلغاء لتصحيح السجل", historicCourse.CancellationReason);
                Assert.True(await db.Notifications.AnyAsync(x =>
                    x.RelatedEntityType == "Certificate" &&
                    x.RelatedEntityId == forceCertificate.Id &&
                    x.Type == NotificationType.CertificateRevoked));
                Assert.True((await forceStatus.ChangeAsync(firstUser.Id,
                    historicCourse.Id, historicCourse.Version,
                    CourseStatus.Finished, "تصحيح تاريخ الانتهاء", true,
                    CancellationToken.None)).Ok);
                Assert.NotNull(historicCourse.FinishedAt);
                Assert.Equal(0, historicCourse.FinalLecturesCount);
                Assert.Null(historicCourse.CancelledAt);
                Assert.Equal(CertificateStatus.Revoked, forceCertificate.Status);
                Assert.True((await thresholdService.ChangeAsync(firstUser.Id,
                    historicCourse.Id, historicCourse.Version,
                    80, CancellationToken.None)).Ok);
                Assert.Equal((short)80,
                    historicCourse.AttendanceThresholdPercentOverride);
                instructorInput = new CourseInstructorEditInput(historicCourse.Version,
                    CourseInstructorChoice.ExternalNameOnly, null,
                    "مدرب مصحح", null, "جهة خارجية", null);
                Assert.Equal("INSTRUCTOR_EDIT_FINISHED", (await instructorEdit.EditAsync(
                    secondUser.Id, historicCourse.Id, instructorInput,
                    CancellationToken.None)).Error?.Code);
                Assert.True((await instructorEdit.EditAsync(firstUser.Id,
                    historicCourse.Id, instructorInput,
                    CancellationToken.None)).Ok);
                Assert.Equal("مدرب مصحح", historicCourse.InstructorNameAr);
                Assert.Null(historicCourse.InstructorUserId);
                Assert.True(historicCourse.ManagedByQuality);
                instructorInput = new CourseInstructorEditInput(historicCourse.Version,
                    CourseInstructorChoice.ExistingExternal,
                    completedExternal.Value!.UserId, null, null, null, null);
                Assert.True((await instructorEdit.EditAsync(firstUser.Id,
                    historicCourse.Id, instructorInput,
                    CancellationToken.None)).Ok);
                Assert.Equal(InstructorKind.ExternalAccount, historicCourse.InstructorKind);
                Assert.Equal(completedExternal.Value.UserId,
                    historicCourse.InstructorUserId);
                Assert.False(historicCourse.ManagedByQuality);
                var pendingInstructorCourse = await db.Courses.SingleAsync(x =>
                    x.Id == directExternal.Value);
                Assert.Equal("INSTRUCTOR_REQUEST_PENDING", (await instructorEdit.EditAsync(
                    firstUser.Id, pendingInstructorCourse.Id,
                    new CourseInstructorEditInput(pendingInstructorCourse.Version,
                        CourseInstructorChoice.Internal, firstUser.Id,
                        null, null, null, null), CancellationToken.None)).Error?.Code);
                var generatedCertificate = await certificateService.IssueAsync(
                    finishCourseId, finishParticipantId, firstUser.Id,
                    CancellationToken.None);
                Assert.True(generatedCertificate.Ok, generatedCertificate.Error?.Code);
                var attendanceCertificate = await db.Certificates.SingleAsync(x =>
                    x.Id == generatedCertificate.Value!.Id);
                Assert.StartsWith("ZU-", attendanceCertificate.ReferenceNo);
                Assert.True(await db.Notifications.AnyAsync(x =>
                    x.RelatedEntityType == "Certificate" &&
                    x.RelatedEntityId == attendanceCertificate.Id));
                Assert.True((await manualAttendance.SetAsync(firstUser.Id,
                    finishLecture.Value, finishParticipantId, false,
                    "تصحيح بعد الشهادة", CancellationToken.None)).Ok);
                Assert.Equal(CertificateStatus.Revoked,
                    attendanceCertificate.Status);
                Assert.Equal(CertificateRevokeReason.AttendanceCorrection,
                    attendanceCertificate.RevokedReason);
                Assert.True((await lectureDeactivation.DeactivateAsync(firstUser.Id,
                    finishLecture.Value, "تصحيح إداري", CancellationToken.None)).Ok);
                Assert.Equal(0, finishCourse.FinalLecturesCount);
                var removedParticipant = await db.CourseParticipants.SingleAsync(x =>
                    x.CourseId == directCourse.Id && x.UserId == secondUser.Id);
                var issuedCertificate = new Certificate
                {
                    ReferenceNo = $"TEST-{Guid.NewGuid():N}"[..35],
                    VerificationToken = Guid.NewGuid().ToString("N"),
                    CourseId = directCourse.Id,
                    ParticipantId = removedParticipant.Id,
                    UserId = secondUser.Id,
                    Status = CertificateStatus.Issued,
                    IssuedAt = DateTimeOffset.UtcNow,
                    TemplateId = template.Id,
                    PdfFileId = uploaded.Value.Id,
                    HolderNameAr = secondUser.FullNameAr,
                    CourseTitleAr = directCourse.TitleAr,
                    CoursePeriodText = "01/05/2027 – 02/05/2027",
                    CourseHours = directCourse.TotalHours,
                    InstructorNameAr = directCourse.InstructorNameAr,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.Certificates.Add(issuedCertificate);
                await db.SaveChangesAsync();
                var deactivationPreview = await lifecycle.PreviewDeactivationAsync(firstUser.Id,
                    directCourse.Id, CancellationToken.None);
                Assert.True(deactivationPreview.Ok, deactivationPreview.Error?.Code);
                Assert.Equal(1, deactivationPreview.Value!.IssuedCertificates);
                Assert.Equal("CERTIFICATE_ACTION_REQUIRED", (await lifecycle.DeactivateAsync(
                    firstUser.Id, directCourse.Id, directCourse.Version,
                    "اختبار التعطيل", null, CancellationToken.None)).Error?.Code);
                Assert.True((await lifecycle.DeactivateAsync(firstUser.Id,
                    directCourse.Id, directCourse.Version, "اختبار التعطيل",
                    CertificateDeactivationAction.Hide,
                    CancellationToken.None)).Ok);
                Assert.False(directCourse.IsActive);
                Assert.False(issuedCertificate.IsActive);
                Assert.True(issuedCertificate.HiddenByCourseDisable);
                var disabledCourses = await courseDirectory.ListAsync(firstUser.Id,
                    new CourseDirectoryFilter(CourseDirectoryView.All, null,
                        null, null, null, null, null, null, false, null),
                    CancellationToken.None);
                Assert.True(disabledCourses.Ok, disabledCourses.Error?.Code);
                Assert.Contains(disabledCourses.Value!.Items, x => x.Id == directCourse.Id);
                Assert.True((await lifecycle.RestoreAsync(firstUser.Id,
                    directCourse.Id, directCourse.Version, CancellationToken.None)).Ok);
                Assert.True(directCourse.IsActive);
                Assert.Equal(CertificateStatus.Revoked, issuedCertificate.Status);
                Assert.Equal(CertificateRevokeReason.CourseDisabledRestoreIneligible,
                    issuedCertificate.RevokedReason);
                var historicalCourse = await db.Courses.SingleAsync(x =>
                    x.Id == historicCreated.Value);
                historicalCourse.Status = CourseStatus.Finished;
                historicalCourse.FinalLecturesCount = 1;
                historicalCourse.FinishedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
                Assert.Equal("CONFIRM_REQUIRED", (await lifecycle.ReopenAsync(firstUser.Id,
                    historicalCourse.Id, historicalCourse.Version, false,
                    CancellationToken.None)).Error?.Code);
                Assert.True((await lifecycle.ReopenAsync(firstUser.Id,
                    historicalCourse.Id, historicalCourse.Version, true,
                    CancellationToken.None)).Ok);
                Assert.Equal(CourseStatus.Approved, historicalCourse.Status);
                Assert.Null(historicalCourse.FinalLecturesCount);
                var cancellationLetter = await fileUploads.UploadAsync(firstUser.Id,
                    FileKind.CancellationLetter, "cancel.pdf", new MemoryStream(pdf),
                    pdf.Length, CancellationToken.None);
                Assert.True(cancellationLetter.Ok, cancellationLetter.Error?.Code);
                var cancellations = new CourseCancellationService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    systemSettings, scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                Assert.Equal("CANCELLATION_LETTER_REQUIRED", (await cancellations.RequestAsync(
                    firstUser.Id, proposal.Id, "سبب", null,
                    CancellationToken.None)).Error?.Code);
                var requestedCancellation = await cancellations.RequestAsync(firstUser.Id,
                    proposal.Id, "سبب رسمي", cancellationLetter.Value!.Id,
                    CancellationToken.None);
                Assert.True(requestedCancellation.Ok, requestedCancellation.Error?.Code);
                Assert.Equal("CANCELLATION_ALREADY_PENDING", (await cancellations.RequestAsync(
                    firstUser.Id, proposal.Id, "سبب رسمي", cancellationLetter.Value.Id,
                    CancellationToken.None)).Error?.Code);
                Assert.False(proposal.ManagedByQuality);
                Assert.Contains((await cancellations.PendingAsync(secondUser.Id,
                    CancellationToken.None)).Value!, x => x.Id == requestedCancellation.Value);
                Assert.Equal("DECISION_NOTES_REQUIRED", (await cancellations.DecideAsync(
                    secondUser.Id, requestedCancellation.Value, false, null,
                    CancellationToken.None)).Error?.Code);
                Assert.True((await cancellations.DecideAsync(secondUser.Id,
                    requestedCancellation.Value, true, null, CancellationToken.None)).Ok);
                Assert.Equal(CourseStatus.Cancelled, proposal.Status);
                Assert.True(proposal.IsArchived);
                Assert.True(await db.CourseStatusHistorys.AnyAsync(x =>
                    x.CourseId == proposal.Id && x.ToStatus == CourseStatus.Cancelled));
                var directCancellationLetter = await fileUploads.UploadAsync(secondUser.Id,
                    FileKind.CancellationLetter, "direct-cancel.pdf", new MemoryStream(pdf),
                    pdf.Length, CancellationToken.None);
                Assert.True(directCancellationLetter.Ok, directCancellationLetter.Error?.Code);
                Assert.True((await cancellations.CancelDirectAsync(secondUser.Id,
                    directCourse.Id, "سبب مباشر", directCancellationLetter.Value!.Id,
                    CancellationToken.None)).Ok);
                Assert.Equal(CourseStatus.Cancelled, directCourse.Status);
                var needLetter = await fileUploads.UploadAsync(firstUser.Id,
                    FileKind.NeedLetter, "need.pdf", new MemoryStream(pdf),
                    pdf.Length, CancellationToken.None);
                Assert.True(needLetter.Ok, needLetter.Error?.Code);
                var needService = new NeedService(db,
                    scope.ServiceProvider.GetRequiredService<IClock>(),
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    systemSettings, scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                var needInput = new NeedInput("احتياج تجريبي", null,
                    "وصف احتياج التدريب", null, directCourse.TrackId, 20,
                    "الفصل القادم", null, null, needLetter.Value!.Id);
                var createdNeed = await needService.CreateAsync(firstUser.Id,
                    needInput, CancellationToken.None);
                Assert.True(createdNeed.Ok, createdNeed.Error?.Code);
                var qualityLetter = await fileUploads.DownloadAsync(secondUser.Id,
                    needLetter.Value.Id, CancellationToken.None);
                Assert.True(qualityLetter.Ok, qualityLetter.Error?.Code);
                await qualityLetter.Value!.Content.DisposeAsync();
                Assert.Equal("FORBIDDEN", (await needService.DecideAsync(firstUser.Id,
                    createdNeed.Value, true, null, CancellationToken.None)).Error?.Code);
                Assert.Equal("REASON_REQUIRED", (await needService.DecideAsync(
                    secondUser.Id, createdNeed.Value, false, null,
                    CancellationToken.None)).Error?.Code);
                Assert.True((await needService.DecideAsync(secondUser.Id,
                    createdNeed.Value, false, "يرجى التفصيل",
                    CancellationToken.None)).Ok);
                Assert.True((await needService.ResubmitAsync(firstUser.Id,
                    createdNeed.Value, needInput with { DescriptionAr = "وصف مفصل" },
                    CancellationToken.None)).Ok);
                Assert.True((await needService.DecideAsync(secondUser.Id,
                    createdNeed.Value, true, null, CancellationToken.None)).Ok);
                var needDetails = await needService.GetAsync(firstUser.Id,
                    createdNeed.Value, CancellationToken.None);
                Assert.True(needDetails.Ok, needDetails.Error?.Code);
                Assert.Equal(2, needDetails.Value!.CurrentRevision);
                Assert.Equal(4, needDetails.Value.Events.Count);
                var qualityMinutesFile = await fileUploads.UploadAsync(firstUser.Id,
                    FileKind.QualityMinutes, "minutes.pdf", new MemoryStream(pdf),
                    pdf.Length, CancellationToken.None);
                Assert.True(qualityMinutesFile.Ok, qualityMinutesFile.Error?.Code);
                Assert.False(await db.VaultDocuments.AnyAsync(x =>
                    x.FileId == qualityMinutesFile.Value!.Id));
                var vault = new VaultService(db,
                    scope.ServiceProvider.GetRequiredService<IPermissionService>(),
                    systemSettings, fileUploads,
                    scope.ServiceProvider.GetRequiredService<IAuditLogger>());
                Assert.True((await vault.LinkAsync(firstUser.Id,
                    qualityMinutesFile.Value!.Id, FileKind.QualityMinutes,
                    "محضر جودة تجريبي", null, null, CancellationToken.None)).Ok);
                Assert.Equal("VAULT_ALREADY_LINKED", (await vault.LinkAsync(firstUser.Id,
                    qualityMinutesFile.Value.Id, FileKind.QualityMinutes,
                    "محضر مكرر", null, null, CancellationToken.None)).Error?.Code);
                var vaultDocument = await db.VaultDocuments.SingleAsync(x =>
                    x.FileId == qualityMinutesFile.Value.Id);
                var vaultDownload = await vault.GetFileAsync(secondUser.Id,
                    vaultDocument.Id, CancellationToken.None);
                Assert.True(vaultDownload.Ok, vaultDownload.Error?.Code);
                await vaultDownload.Value!.Content.DisposeAsync();
                var syncLock = new UniversitySyncLock();
                var lease = await syncLock.TryAcquireAsync(CancellationToken.None);
                Assert.NotNull(lease);
                Assert.Null(await syncLock.TryAcquireAsync(CancellationToken.None));
                await lease!.DisposeAsync();
                var secondLease = await syncLock.TryAcquireAsync(CancellationToken.None);
                Assert.NotNull(secondLease);
                await secondLease!.DisposeAsync();
                await authTransaction.RollbackAsync();
            }
            finally
            {
                Environment.SetEnvironmentVariable("ZU_DB_CONNECTION", oldConnection);
                Environment.SetEnvironmentVariable("ZU_ENC_KEYS", oldKeys);
            }
        }
        finally
        {
            if (container is not null) await container.DisposeAsync();
        }
    }

    private sealed class FakeTokenService : ITokenService
    {
        public string CreateAccessToken(User user, AccountPermissionSet permissions,
            bool passwordChangeOnly, DateTimeOffset expiresAt) =>
            $"test-token-{user.Id}-{passwordChangeOnly}-{expiresAt.ToUnixTimeSeconds()}";
    }

    private sealed class NoActiveModeAccessor : IActiveModeAccessor
    {
        public RoleCode? SelectedMode => null;
    }

    private sealed class FakeUserStateCache : IUserStateCache
    {
        public void Invalidate(long userId) { }
    }

    private sealed class FakeEmailContentBuilder : IEmailContentBuilder
    {
        public (string Subject, string BodyHtml) ForgotPassword(string name,
            string password, UiLanguage language) => ("Test", $"<p>{password}</p>");
        public (string Subject, string BodyHtml) InitialCredentials(string name, string login,
            string password, UiLanguage language) => ("Test", $"<p>{login}:{password}</p>");
        public (string Subject, string BodyHtml) ExternalInstructorAccount(string name,
            string login, string password, UiLanguage language) =>
            ("Test external", $"<p>{login}:{password}</p>");
    }

    private sealed class MemoryFileStorage : IFileStorage
    {
        private readonly Dictionary<string, byte[]> files = new();
        public Task<string> WriteAsync(string extension, ReadOnlyMemory<byte> content,
            DateTimeOffset now, CancellationToken ct)
        {
            string path = $"{now:yyyy/MM}/{Guid.NewGuid():N}{extension}";
            files[path] = content.ToArray();
            return Task.FromResult(path);
        }
        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct) =>
            Task.FromResult<Stream>(new MemoryStream(files[relativePath], writable: false));
        public Task DeleteUncommittedAsync(string relativePath)
        {
            files.Remove(relativePath);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCertificatePdfRenderer : ICertificatePdfRenderer
    {
        public byte[] Render(byte[] templatePdf, CertificateTextLayerInput input, byte[] qrPng) =>
            "%PDF-1.4\n%zu-test-certificate\n%%EOF\n"u8.ToArray();
    }

    private sealed class FakeQrCodeService : IQrCodeService
    {
        public byte[] GeneratePng(string content, int targetPixels) =>
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    }

    private sealed class FakeCertificateService(IAppDbContext db, IClock clock)
        : ZuLms.Application.Features.Certificates.ICertificateService
    {
        public async Task<ZuLms.Application.Common.Results.Result<ZuLms.Application.Features.Certificates.CertificateIssueDto>> IssueAsync(
            long courseId, long participantId, long? actorUserId, CancellationToken ct)
        {
            bool template = await db.CertificateTemplates.AsNoTracking()
                .AnyAsync(x => x.IsCurrent, ct);
            if (!template)
                return ZuLms.Application.Common.Results.Result<ZuLms.Application.Features.Certificates.CertificateIssueDto>.Failure("CERT_TEMPLATE_MISSING");
            bool issued = await db.Certificates.IgnoreQueryFilters()
                .AnyAsync(x => x.ParticipantId == participantId &&
                    x.Status == CertificateStatus.Issued, ct);
            if (issued)
                return ZuLms.Application.Common.Results.Result<ZuLms.Application.Features.Certificates.CertificateIssueDto>.Failure("CERTIFICATE_ALREADY_ISSUED");
            var participant = await db.CourseParticipants.AsNoTracking()
                .Where(x => x.Id == participantId)
                .Select(x => new { x.UserId }).SingleAsync(ct);
            var course = await db.Courses.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.Id == courseId)
                .Select(x => new { x.TitleAr, x.TotalHours, x.InstructorNameAr })
                .SingleAsync(ct);
            var holder = await db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.Id == participant.UserId)
                .Select(x => new { x.FullNameAr }).SingleAsync(ct);
            var templateRow = await db.CertificateTemplates.AsNoTracking()
                .Where(x => x.IsCurrent).Select(x => x.Id).FirstAsync(ct);
            var anyFile = await db.StoredFiles.AsNoTracking()
                .Select(x => x.Id).FirstOrDefaultAsync(ct);
            var certificate = new Certificate
            {
                ReferenceNo = $"TEST-{Guid.NewGuid():N}"[..35],
                VerificationToken = Guid.NewGuid().ToString("N"),
                CourseId = courseId,
                ParticipantId = participantId,
                UserId = participant.UserId,
                Status = CertificateStatus.Issued,
                IssuedAt = clock.UtcNow,
                TemplateId = templateRow,
                PdfFileId = anyFile,
                HolderNameAr = holder.FullNameAr,
                CourseTitleAr = course.TitleAr,
                CoursePeriodText = "01/05/2027 – 02/05/2027",
                CourseHours = course.TotalHours,
                InstructorNameAr = course.InstructorNameAr,
                CreatedAt = clock.UtcNow
            };
            db.Certificates.Add(certificate);
            await db.SaveChangesAsync(ct);
            return ZuLms.Application.Common.Results.Result<ZuLms.Application.Features.Certificates.CertificateIssueDto>.Success(
                new(certificate.Id, certificate.ReferenceNo));
        }

        public Task<ZuLms.Application.Common.Results.Result> RevokeAsync(long actorUserId,
            long certificateId, string reason, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ZuLms.Application.Common.Results.Result<int>> IssuePendingAsync(
            long actorUserId, long? courseId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ZuLms.Application.Common.Results.Result<ZuLms.Application.Features.Certificates.AdministrativeCertificateResultDto>> ForceIssueAsync(
            long actorUserId, long courseId, int employeeCode, bool evaluationExempt,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<ZuLms.Application.Common.Results.Result<ZuLms.Application.Features.Certificates.CertificatePageDto>> ListAsync(
            long actorUserId, string? search, long? courseId, CertificateStatus? status,
            int page, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ZuLms.Application.Common.Results.Result<IReadOnlyList<ZuLms.Application.Features.Certificates.CertificateCardDto>>> MineAsync(
            long actorUserId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ZuLms.Application.Common.Results.Result<ZuLms.Application.Features.Files.FileDownloadDto>> GetPdfAsync(
            long actorUserId, long certificateId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ZuLms.Application.Common.Results.Result<ZuLms.Application.Features.Certificates.CertificateVerifyDto?>> VerifyAsync(
            string referenceNo, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public long? Id { get; set; }
        public long? UserId => Id;
        public string? LoginName => null;
        public string? SessionId => "test-session";
        public IReadOnlyCollection<RoleCode> Roles => [];
        public IReadOnlyCollection<long> DeanScopeCenterIds => [];
        public string Ip => "127.0.0.1";
        public string UserAgent => "test";
        public UiLanguage Language => UiLanguage.Ar;
    }
}
