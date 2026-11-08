using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using System.Globalization;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Features.Auth;
using ZuLms.Application.Features.Permissions;
using ZuLms.Application.Features.Settings;
using ZuLms.Application.Features.Users;
using ZuLms.Application.Features.Credentials;
using ZuLms.Application.Common.Serialization;
using ZuLms.Infrastructure;
using ZuLms.Infrastructure.Persistence;
using ZuLms.Infrastructure.Persistence.Seed;
using ZuLms.Infrastructure.Security;
using ZuLms.Web.Middleware;
using ZuLms.Web.Security;
using ZuLms.Web.Localization;

string? maintenanceCommand = args.Length == 1 &&
    (args[0] == "--reencrypt-secrets" || args[0] == "--sync" ||
     args[0] == "--sync-organization" || args[0] == "--migrate" ||
     args[0] == "--check-files") ? args[0] : null;
var builder = WebApplication.CreateBuilder(maintenanceCommand is null ? args : []);
// Existing infrastructure reads these settings from the process environment.
// In Development, user secrets provide them without placing credentials in launchSettings.json.
if (builder.Environment.IsDevelopment())
{
    foreach (string key in new[] { "ZU_DB_CONNECTION", "ZU_JWT_KEY", "ZU_ENC_KEYS",
        "ZU_BOOTSTRAP_ADMIN_LOGIN", "ZU_BOOTSTRAP_ADMIN_PASSWORD", "ZU_BOOTSTRAP_ADMIN_NAME" })
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)) &&
            !string.IsNullOrEmpty(builder.Configuration[key]))
            Environment.SetEnvironmentVariable(key, builder.Configuration[key]);
    }
}
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
builder.Services.AddHsts(options => options.MaxAge = TimeSpan.FromDays(365));
string logRoot = Environment.GetEnvironmentVariable("ZU_LOG_ROOT")
    ?? (builder.Environment.IsDevelopment()
        ? Path.Combine(builder.Environment.ContentRootPath, "logs")
        : "/var/zu-lms/logs");
Directory.CreateDirectory(logRoot);
builder.Host.UseSerilog((_, configuration) => configuration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(logRoot, "zu-lms-.log"),
        rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30));
builder.Services.AddLocalization();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddAreaPageRoute("Public", "/Login", "/login");
    options.Conventions.AddAreaPageRoute("Public", "/ChangePassword", "/change-password");
    options.Conventions.AddAreaPageRoute("Public", "/ForgotPassword", "/forgot-password");
    options.Conventions.AddPageRoute("/Admin/Credentials", "/admin/users/{id:long}");
    options.Conventions.AddPageRoute("/Courses/Details", "/admin/courses/{courseId:long}");
    options.Conventions.AddPageRoute("/Courses/Details", "/quality/courses/{courseId:long}");
    options.Conventions.AddPageRoute("/Courses/Details", "/instructor/courses/{courseId:long}");
    options.Conventions.AddPageRoute("/Courses/Details", "/member/courses/{courseId:long}");
    options.Conventions.AddPageRoute("/Instructor/Lectures", "/instructor/courses/{courseId:long}/attendance");
    options.Conventions.AddPageRoute("/Instructor/Lectures", "/instructor/courses/{courseId:long}/exam");
    options.Conventions.AddPageRoute("/Courses/Details", "/instructor/courses/{courseId:long}/evaluation");
    options.Conventions.AddPageRoute("/Courses/Available", "/member/available");
    options.Conventions.AddPageRoute("/Member/Courses", "/member/enrolled");
    options.Conventions.AuthorizeAreaPage("Public", "/ChangePassword");
    options.Conventions.AuthorizePage("/Account/Password");
    options.Conventions.AuthorizePage("/Admin/PasswordPolicy", "permission:page.admin.password_policy");
    options.Conventions.AuthorizePage("/Admin/Roles", "permission:page.admin.roles");
    options.Conventions.AuthorizePage("/Admin/Stats", "permission:page.admin.stats");
    options.Conventions.AuthorizePage("/Dean/Stats", "permission:page.dean.stats");
    options.Conventions.AuthorizePage("/Quality/ExternalInstructors", "permission:page.quality.external_instructors");
    options.Conventions.AuthorizePage("/Quality/CoursesNewHistorical", "permission:course.create_historical");
    options.Conventions.AuthorizePage("/Account/Settings");
    options.Conventions.AuthorizePage("/Admin/Credentials", "permission:page.admin.credentials");
    options.Conventions.AuthorizePage("/Admin/Settings", "permission:page.admin.settings");
    options.Conventions.AuthorizePage("/Admin/Sync", "permission:page.admin.sync");
    options.Conventions.AuthorizePage("/Admin/Centers", "permission:page.admin.centers");
    options.Conventions.AuthorizePage("/Courses/Available", "permission:participants.self_register");
    options.Conventions.AuthorizePage("/Dean/Propose", "permission:page.dean.propose");
    options.Conventions.AuthorizePage("/Quality/Proposals", "permission:page.quality.proposals");
    options.Conventions.AuthorizePage("/Admin/Users", "permission:page.admin.users");
    options.Conventions.AuthorizePage("/Quality/CoursesNew", "permission:course.create_direct");
    options.Conventions.AuthorizePage("/Admin/CoursesNew", "permission:course.create_direct");
    options.Conventions.AuthorizePage("/Courses/Participants", "permission:participants.add");
    options.Conventions.AuthorizePage("/Courses/CancelRequest", "permission:cancellation.request");
    options.Conventions.AuthorizePage("/Quality/Cancellations", "permission:cancellation.decide");
    options.Conventions.AuthorizePage("/Admin/Dashboard", "permission:page.admin.dashboard");
    options.Conventions.AuthorizePage("/Admin/Hints", "permission:page.admin.hints");
    options.Conventions.AuthorizePage("/Quality/Dashboard", "permission:page.quality.dashboard");
    options.Conventions.AuthorizePage("/Dean/Dashboard", "permission:page.dean.dashboard");
    options.Conventions.AuthorizePage("/Member/Dashboard", "permission:page.member.dashboard");
    options.Conventions.AuthorizePage("/Instructor/Courses", "permission:page.instructor.courses");
    options.Conventions.AuthorizePage("/Instructor/Lectures", "permission:page.instructor.courses");
    options.Conventions.AuthorizePage("/Admin/Courses", "permission:page.admin.courses");
    options.Conventions.AuthorizePage("/Quality/Courses", "permission:page.quality.courses");
    options.Conventions.AuthorizePage("/Dean/Courses", "permission:page.dean.courses");
    options.Conventions.AuthorizePage("/Member/Courses", "permission:page.member.enrolled");
    options.Conventions.AuthorizePage("/Courses/Details");
    options.Conventions.AuthorizePage("/Admin/CertificateTemplate", "permission:cert_template.upload");
    options.Conventions.AuthorizePage("/Quality/Certificates", "permission:certificates.issue_pending");
    options.Conventions.AuthorizePage("/Member/Certificates", "permission:page.member.certificates");
    options.Conventions.AuthorizePage("/Dean/Needs", "permission:page.dean.needs");
    options.Conventions.AuthorizePage("/Quality/Needs", "permission:page.quality.needs");
    options.Conventions.AuthorizePage("/Admin/Needs", "permission:page.admin.needs");
    options.Conventions.AuthorizePage("/Quality/EvalForm", "permission:page.quality.eval_form");
    options.Conventions.AuthorizePage("/Quality/Evaluations", "permission:page.quality.evaluations");
    options.Conventions.AuthorizePage("/Member/Impact", "permission:page.member.impact");
    options.Conventions.AuthorizePage("/Instructor/Impact", "permission:impact.review");
    options.Conventions.AuthorizePage("/Member/ProCerts", "permission:page.member.pro_certs");
    options.Conventions.AuthorizePage("/Quality/ProCerts", "permission:page.quality.pro_certs");
    options.Conventions.AuthorizePage("/Quality/ProCertStats", "permission:page.quality.pro_cert_stats");
    options.Conventions.AuthorizePage("/Admin/Audit", "permission:page.admin.audit");
    options.Conventions.AuthorizePage("/Admin/LiveFeed", "permission:page.admin.live_feed");
    options.Conventions.AuthorizePage("/Vault/Index", "permission:page.vault");
    options.Conventions.AuthorizePage("/Shared/Directory", "permission:page.shared.directory");
    options.Conventions.AuthorizePage("/Shared/Profile", "permission:page.shared.profile");
    options.Conventions.AuthorizePage("/Quality/Tracks", "permission:page.quality.tracks");
    options.Conventions.AuthorizePage("/Member/Teaching", "permission:page.member.teaching");
    options.Conventions.AuthorizePage("/Dean/Members", "permission:page.dean.members");
    options.Conventions.AuthorizePage("/Member/EnterCode", "permission:attendance.enter_code");
});
builder.Services.AddControllers(options => options.Filters.Add<ApiAntiforgeryAttribute>())
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.JsonSerializerOptions.Converters.Add(new AmmanDateTimeOffsetConverter());
    });
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(new
    {
        code = "VALIDATION_FAILED",
        fieldErrors = context.ModelState.Where(x => x.Value?.Errors.Count > 0)
            .ToDictionary(x => x.Key, _ => new[] { "القيمة غير صالحة" })
    });
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter());
    options.SerializerOptions.Converters.Add(new AmmanDateTimeOffsetConverter());
});
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("ar");
    options.SupportedCultures = [new CultureInfo("ar"), new CultureInfo("en")];
    options.SupportedUICultures = [new CultureInfo("ar"), new CultureInfo("en")];
    options.RequestCultureProviders.Insert(0, new RequestCultureFromUserProvider());
});
builder.Services.AddInfrastructure();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<LoginRateGate>();
builder.Services.AddSingleton<IUserStateCache, UserStateCache>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IActiveModeAccessor, HttpActiveModeAccessor>();
builder.Services.AddScoped<ZuLms.Application.Features.Permissions.IRolePermissionAdminService,
    ZuLms.Application.Features.Permissions.RolePermissionAdminService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ZuLms.Application.Features.Stats.IStatsService,
    ZuLms.Application.Features.Stats.StatsService>();
builder.Services.AddScoped<ZuLms.Application.Features.Sync.IUniversitySyncAdminService,
    ZuLms.Application.Features.Sync.UniversitySyncAdminService>();
builder.Services.AddScoped<ZuLms.Application.Features.Sync.ICenterAdminService,
    ZuLms.Application.Features.Sync.CenterAdminService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.ISelfRegistrationService,
    ZuLms.Application.Features.Courses.SelfRegistrationService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.ICourseProposalService,
    ZuLms.Application.Features.Courses.CourseProposalService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.ICourseProposalQueryService,
    ZuLms.Application.Features.Courses.CourseProposalQueryService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.IExternalInstructorAccountService,
    ZuLms.Application.Features.Courses.ExternalInstructorAccountService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.IDirectCourseService,
    ZuLms.Application.Features.Courses.DirectCourseService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.IManualParticipantService,
    ZuLms.Application.Features.Courses.ManualParticipantService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.ICourseCancellationService,
    ZuLms.Application.Features.Courses.CourseCancellationService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.ICourseDirectoryService,
    ZuLms.Application.Features.Courses.CourseDirectoryService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.IEligibilitySnapshotService,
    ZuLms.Application.Features.Courses.EligibilitySnapshotService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.IAdminCourseLifecycleService,
    ZuLms.Application.Features.Courses.AdminCourseLifecycleService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.ICourseCommonEditService,
    ZuLms.Application.Features.Courses.CourseCommonEditService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.ICourseForceStatusService,
    ZuLms.Application.Features.Courses.CourseForceStatusService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.ICourseAttendanceThresholdService,
    ZuLms.Application.Features.Courses.CourseAttendanceThresholdService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.ICourseInstructorEditService,
    ZuLms.Application.Features.Courses.CourseInstructorEditService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.ILectureService,
    ZuLms.Application.Features.Courses.LectureService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.IAttendanceCodeService,
    ZuLms.Application.Features.Courses.AttendanceCodeService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.IManualAttendanceService,
    ZuLms.Application.Features.Courses.ManualAttendanceService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.IExamService,
    ZuLms.Application.Features.Courses.ExamService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.ICourseFinishService,
    ZuLms.Application.Features.Courses.CourseFinishService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.IExamRequirementService,
    ZuLms.Application.Features.Courses.ExamRequirementService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.IEligibilityRecalculationService,
    ZuLms.Application.Features.Courses.EligibilityRecalculationService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.ILectureDeactivationService,
    ZuLms.Application.Features.Courses.LectureDeactivationService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.ICourseProgressService,
    ZuLms.Application.Features.Courses.CourseProgressService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.ILectureMaterialService,
    ZuLms.Application.Features.Courses.LectureMaterialService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.IEvaluationService,
    ZuLms.Application.Features.Courses.EvaluationService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.IImpactService,
    ZuLms.Application.Features.Courses.ImpactService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.IEvaluationPinningService,
    ZuLms.Application.Features.Courses.EvaluationPinningService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.IEvaluationFormAdminService,
    ZuLms.Application.Features.Courses.EvaluationFormAdminService>();
builder.Services.AddHostedService<ZuLms.Web.Background.EvaluationPinningWorker>();
builder.Services.AddScoped<ZuLms.Application.Features.Certificates.ICertificateService,
    ZuLms.Application.Features.Certificates.CertificateService>();
builder.Services.AddScoped<ZuLms.Application.Features.Certificates.ICertificateTemplateService,
    ZuLms.Application.Features.Certificates.CertificateTemplateService>();
builder.Services.AddScoped<ZuLms.Application.Features.Needs.INeedService,
    ZuLms.Application.Features.Needs.NeedService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.IEvaluationQueryService,
    ZuLms.Application.Features.Courses.EvaluationQueryService>();
builder.Services.AddScoped<ZuLms.Application.Features.Courses.IEvaluationAggregateService,
    ZuLms.Application.Features.Courses.EvaluationAggregateService>();
builder.Services.AddScoped<ZuLms.Application.Features.ProCerts.IProCertService,
    ZuLms.Application.Features.ProCerts.ProCertService>();
builder.Services.AddScoped<ZuLms.Application.Features.Notifications.INotificationService,
    ZuLms.Application.Features.Notifications.NotificationService>();
builder.Services.AddScoped<ZuLms.Application.Features.Audit.IAuditQueryService,
    ZuLms.Application.Features.Audit.AuditQueryService>();
builder.Services.AddScoped<ZuLms.Application.Features.Vault.IVaultService,
    ZuLms.Application.Features.Vault.VaultService>();
builder.Services.AddScoped<ZuLms.Application.Features.Profiles.IProfileService,
    ZuLms.Application.Features.Profiles.ProfileService>();
builder.Services.AddScoped<ZuLms.Application.Features.Tracks.ITrackAdminService,
    ZuLms.Application.Features.Tracks.TrackAdminService>();
builder.Services.AddScoped<ZuLms.Application.Features.Files.IFileUploadService,
    ZuLms.Application.Features.Files.FileUploadService>();
builder.Services.AddScoped<ZuLms.Application.Features.Hints.IHintService,
    ZuLms.Application.Features.Hints.HintService>();
builder.Services.AddScoped<ICredentialService, CredentialService>();
builder.Services.AddScoped<IPasswordPolicyService, PasswordPolicyService>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddSingleton<IEmailContentBuilder, EmailContentBuilder>();
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = JwtTokenService.Issuer,
        ValidateAudience = true,
        ValidAudience = JwtTokenService.Issuer,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = JwtTokenService.ReadValidationKey(),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            context.Token = TabSession.Cookie(context.HttpContext, AuthHttp.AccessCookie);
            // Do not accept a global cookie or fallback Authorization header when
            // a tab has no selected session.
            if (string.IsNullOrEmpty(context.Token)) context.NoResult();
            return Task.CompletedTask;
        },
        OnTokenValidated = AuthHttp.ValidateUserAsync,
        OnChallenge = context =>
        {
            if (!context.Request.Path.StartsWithSegments("/api") &&
                context.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase))
            {
                context.HandleResponse();
                context.Response.Redirect(TabSession.Url(context.HttpContext, "/login"));
            }
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    string? proxies = Environment.GetEnvironmentVariable("ZU_KNOWN_PROXIES");
    if (string.IsNullOrWhiteSpace(proxies))
    {
        if (builder.Environment.IsProduction())
            throw new InvalidOperationException("ZU_KNOWN_PROXIES is required in Production.");
        return;
    }
    foreach (string raw in proxies.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
    {
        if (!System.Net.IPAddress.TryParse(raw, out var address))
            throw new InvalidOperationException("ZU_KNOWN_PROXIES contains an invalid IP address.");
        options.KnownProxies.Add(address);
    }
});

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.OpenConnectionAsync();
    try
    {
        // Session-level lock serializes migration and seed across application replicas.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_lock(926151001); ");
        try
        {
            if (app.Environment.IsDevelopment() || maintenanceCommand == "--migrate")
                await db.Database.MigrateAsync();
            else if ((await db.Database.GetPendingMigrationsAsync()).Any())
                throw new InvalidOperationException("Database migrations are pending. Run the separate migration step.");
            await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync(CancellationToken.None);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock(926151001); ");
        }
    }
    finally
    {
        await db.Database.CloseConnectionAsync();
    }
}
app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseHttpsRedirection();
app.UseRouting();
app.UseMiddleware<TabSessionMiddleware>();
app.UseMiddleware<SilentRefreshMiddleware>();
// A signed-in user who opens a page outside their permissions must see the localized
// access-denied page, not a blank 403 document. APIs keep their status and envelope.
app.UseStatusCodePages(async status =>
{
    var request = status.HttpContext.Request;
    var response = status.HttpContext.Response;
    if (response.StatusCode is not (StatusCodes.Status403Forbidden or StatusCodes.Status404NotFound) ||
        !HttpMethods.IsGet(request.Method) ||
        !request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase)) return;
    if (response.StatusCode == StatusCodes.Status403Forbidden)
    {
        response.Redirect(TabSession.Url(status.HttpContext, "/access-denied"));
        return;
    }
    // Re-execute the Razor page in the same request so an unknown URL stays HTTP 404.
    var originalPath = request.Path;
    var originalEndpoint = status.HttpContext.GetEndpoint();
    request.Path = "/not-found";
    status.HttpContext.SetEndpoint(null);
    try { await status.Next(status.HttpContext); }
    finally
    {
        request.Path = originalPath;
        status.HttpContext.SetEndpoint(originalEndpoint);
    }
});
app.UseAuthentication();
app.UseRequestLocalization();
app.UseMiddleware<ApiEnvelopeMiddleware>();
app.UseMiddleware<ForcedPasswordChangeMiddleware>();
app.UseAuthorization();
app.MapAuthEndpoints();
app.MapContextEndpoints();
app.MapVerifyEndpoints();
app.MapHealthEndpoints();
app.MapControllers();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
if (maintenanceCommand == "--reencrypt-secrets")
{
    await SecretReencryption.RunAsync(app.Services);
    return;
}
if (maintenanceCommand == "--migrate") return;
if (maintenanceCommand == "--check-files")
{
    await FileIntegrityCheck.RunAsync(app.Services);
    return;
}
if (maintenanceCommand == "--sync")
{
    await CommandLineSync.RunAsync(app);
    return;
}
if (maintenanceCommand == "--sync-organization")
{
    await CommandLineOrganizationSync.RunAsync(app);
    return;
}
app.Run();

public partial class Program;
