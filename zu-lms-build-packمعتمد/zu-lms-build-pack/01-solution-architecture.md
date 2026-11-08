# 01 — بنية الحل (Clean Architecture) وما يحتويه كل مجلد وملف

## 1. المكدّس (لا تغيّر شيئاً)
| الجانب | القرار |
|---|---|
| منصة | .NET 10، ASP.NET Core، **Razor Pages + Areas** + Controllers لواجهة JSON |
| بيانات | EF Core + `Npgsql.EntityFrameworkCore.PostgreSQL` + `EFCore.NamingConventions` (snake_case)، PostgreSQL 17 |
| واجهة | Bootstrap 5.3 (RTL/LTR)، jQuery 3.7، AJAX، jquery-validation، Chart.js (محلي) |
| دخول | JWT داخل كوكي HttpOnly (تفاصيل `03`) |
| تحقق | FluentValidation |
| سجلات تشغيل | Serilog (ملفات + Console) |
| بريد | MailKit (SMTP) |
| PDF | QuestPDF (طبقة النصوص) + دمج فوق قالب PDF (`DocumentOperation` في QuestPDF) — **يجب التحقق من رخصة QuestPDF للجامعة قبل الإنتاج** (سجّل في `QUESTIONS.md` إن لم تتأكد) |
| QR | QRCoder (MIT) |
| اختبارات | xUnit، FluentAssertions، Testcontainers.PostgreSql، NetArchTest.Rules، Microsoft.AspNetCore.Mvc.Testing |
| نشر | Docker + docker-compose على Ubuntu، GitHub Actions عبر SSH (`11`) |

**ملاحظات إلزامية:** إدارة الحزم مركزية (`Directory.Packages.props`). اختر أحدث إصدار مستقر متوافق مع .NET 10 **عمره أسبوعان على الأقل**. أصول الواجهة (Bootstrap, jQuery, Chart.js, الخط Tajawal, منتقي التاريخ) **تُحفظ محلياً داخل `wwwroot/lib`** ولا تُحمَّل من CDN.

## 2. شجرة الحل
```
zu-lms/
├─ ZuLms.sln
├─ global.json                      # يثبّت SDK 10.x
├─ Directory.Build.props            # Nullable, ImplicitUsings, TreatWarningsAsErrors, LangVersion
├─ Directory.Packages.props         # الإصدارات المركزية
├─ .editorconfig
├─ .gitignore
├─ QUESTIONS.md                     # أسئلة المنفّذ (يبدأ فارغاً)
├─ docs/                            # template-inventory.md وغيره
├─ prototype/                       # (مُعطى) مرجع الشكل
├─ template/                        # (مُعطى) مرجع المكوّنات
├─ deploy/                          # Dockerfile, docker-compose.yml, nginx.conf.sample, .env.sample
├─ src/
│  ├─ ZuLms.Domain/
│  ├─ ZuLms.Application/
│  ├─ ZuLms.Infrastructure/
│  └─ ZuLms.Web/
└─ tests/
   ├─ ZuLms.Domain.Tests/
   ├─ ZuLms.Application.Tests/
   ├─ ZuLms.Infrastructure.Tests/
   ├─ ZuLms.Web.Tests/
   └─ ZuLms.Architecture.Tests/
```

### قاعدة الاعتماد (تُفرض باختبار معماري)
`Domain` ← لا يعتمد على شيء. `Application` ← Domain + `Microsoft.EntityFrameworkCore` (التجريدات فقط، **بدون Npgsql**) + FluentValidation. `Infrastructure` ← Application + Domain. `Web` ← Application + Infrastructure (للتسجيل في DI فقط). ممنوع أن يستدعي Web أو Application أي نوع محدد من Infrastructure مباشرة.

---

## 3. `ZuLms.Domain` — لا اعتماديات، لا EF، لا ASP.NET
### 3.1 `Common/`
| ملف | المحتوى |
|---|---|
| `BaseEntity.cs` | `long Id`, `DateTimeOffset CreatedAt`, `long? CreatedByUserId`, `DateTimeOffset? UpdatedAt`, `long? UpdatedByUserId`, `uint Version` (يربط بـ `xmin` في Infrastructure). |
| `ISoftDeletable.cs` | `bool IsActive`, `DateTimeOffset? DeactivatedAt`, `long? DeactivatedByUserId`, `string? DeactivationReason`. + دالتا `Deactivate(userId, reason, now)` و`Restore()` في كلاس مساعد `SoftDeleteExtensions`. |
| `IArchivable.cs` | `bool IsArchived`, `DateTimeOffset? ArchivedAt`, `long? ArchivedByUserId` + `Archive(...)`, `Unarchive()`. |
| `DomainException.cs` | استثناء بقاعدة `Code` للانتهاكات غير المتوقعة فقط. |

### 3.2 `Enums/` (كل enum في ملف، ويُخزَّن في قاعدة البيانات كنص عبر `HasConversion<string>()` مع قيد CHECK)
| Enum | القيم بالترتيب |
|---|---|
| `AccountType` | `Employee`, `ExternalInstructor`, `Local` |
| `RoleCode` | `Admin`, `Quality`, `Dean`, `Instructor`, `Member`, `ExternalInstructor` |
| `PermissionKind` | `PageView`, `Action` |
| `PermissionScope` | `Global`, `OwnFaculty`, `InstructedCourse`, `Self` |
| `OverrideMode` | `Allow`, `Deny` |
| `CredentialsEmailStatus` | `NotSent`, `Sent`, `Failed`, `Uncertain` |
| `SettingValueType` | `Int`, `Decimal`, `Bool`, `String`, `Json` |
| `DeliveryMode` | `InPerson`, `Online`, `Blended` |
| `LectureMode` | `InPerson`, `Online` |
| `CourseOrigin` | `DeanProposal`, `QualityCreated`, `AdminCreated`, `Historical` |
| `AudienceType` | `General`, `Faculty`, `Named` |
| `CourseStatus` | `Proposed`, `Returned`, `Rejected`, `Approved`, `Finished`, `Cancelled` |
| `InstructorKind` | `Internal`, `ExternalAccount`, `ExternalNameOnly` |
| `ParticipantSource` | `SelfRegistered`, `AddedByQuality`, `AddedByAdmin`, `HistoricalEntry` |
| `AttendanceSource` | `Code`, `Manual` |
| `ExamOutcome` | `Passed`, `Failed` |
| `ImpactType` | `StudyPlan`, `ExamModel`, `ClassActivity` |
| `ImpactStatus` | `Submitted`, `Returned`, `Approved` |
| `ProposalDecision` | `Pending`, `Returned`, `Approved`, `Rejected` |
| `RequestStatus` | `Pending`, `Approved`, `Rejected` |
| `ExternalAccountRequestStatus` | `PendingAdmin`, `Completed`, `Rejected` |
| `FormVersionStatus` | `Draft`, `Published` |
| `CertificateStatus` | `Issued`, `Revoked` |
| `CertificateRevokeReason` | `AttendanceCorrection`, `ExamResultChanged`, `ExamRequirementChanged`, `CourseCancelled`, `CourseReopened`, `ParticipantRemoved`, `CourseDisabledRestoreIneligible`, `ManualByAdmin` |
| `NeedStatus` | `Submitted`, `Returned`, `Approved` |
| `FileKind` | `ProposalCouncilLetter`, `InstructorCv`, `CancellationLetter`, `NeedLetter`, `TrainingMaterial`, `ImpactFile`, `CertificateTemplate`, `CertificatePdf`, `ProfilePhoto`, `ProfessionalCertificate`, `QualityMinutes`, `Recommendation`, `Other` |
| `NotificationType` | انظر `09` |
| `EmailKind` | `InitialCredentials`, `ForgotPassword`, `ExternalInstructorAccount` |
| `OutboxStatus` | `Pending`, `Sent`, `Failed`, `Uncertain` |
| `AuditCategory` | `Auth`, `User`, `Permission`, `Settings`, `Course`, `Lecture`, `Attendance`, `Exam`, `Impact`, `Evaluation`, `Certificate`, `Document`, `Sync`, `Need`, `System`, `Security` |
| `SyncStatus` | `Success`, `Partial`, `Failed` |
| `SyncReviewReason` | `MissingFromSource`, `UnresolvedReference`, `DuplicateLoginName`, `DuplicateSourceKey`, `SharedEmail`, `StatusChanged` |
| `SyncReviewResolution` | `Keep`, `Disable`, `Ignore` |
| `UiLanguage` | `Ar`, `En` |
| `ProCertStatus` | `Submitted`, `Approved`, `Rejected` |

### 3.3 `Entities/` — مجلد فرعي لكل مجال، كلاس لكل جدول
**الخصائص = أعمدة الجدول في `02` بصيغة PascalCase، والعلاقات = المفاتيح الأجنبية في `02` كخصائص تنقّل.** لا منطق في الكيانات سوى دوال حالة بسيطة (`Deactivate`, `Archive`, `MarkSent`...).
| المجلد | الكيانات |
|---|---|
| `Org/` | `UniversitySide`, `Center`, `Department`, `Employee` |
| `Identity/` | `User`, `Role`, `Permission`, `RolePermission`, `UserRole`, `UserPermissionOverride`, `RefreshToken`, `ForgotPasswordAttempt`, `PasswordPolicy`, `BlockedPassword`, `ExternalInstructorProfile` |
| `Config/` | `SystemSetting`, `FieldHint`, `NumberSequence` |
| `Audit/` | `AuditLog` |
| `Files/` | `StoredFile`, `VaultDocument` |
| `Notify/` | `Notification`, `EmailOutbox` |
| `Training/` | `TrainingTrack`, `Course`, `CourseStatusHistory`, `CourseProposalRevision`, `CourseFile`, `CourseParticipant`, `Lecture`, `AttendanceRecord`, `AttendanceChange`, `AttendanceCodeAttempt`, `CourseExam`, `ExamResult`, `ExamResultHistory`, `ImpactSubmission`, `CourseCancellationRequest`, `ExternalInstructorAccountRequest` |
| `Evaluation/` | `EvaluationFormVersion`, `EvaluationQuestion`, `EvaluationOption`, `EvaluationResponse`, `EvaluationAnswer` |
| `Certificates/` | `CertificateTemplate`, `Certificate` |
| `Needs/` | `TrainingNeed`, `TrainingNeedEvent` |
| `Profiles/` | `MemberProfile`, `ProfessionalCertCategory`, `ProfessionalCertification`, `ProfessionalCertEvent` |
| `Sync/` | `SyncRun`, `SyncReviewItem` |

### 3.4 `Rules/` — دوال نقية (static) بلا I/O، وهي **المصدر الوحيد** للقواعد الحسابية
| ملف | الدوال (توقيع حرفي) |
|---|---|
| `AttendanceRules.cs` | `static int MinLecturesRequired(int totalLectures, int thresholdPercent)` = `totalLectures<=0 ? 0 : (totalLectures*thresholdPercent+99)/100` (حساب صحيح بلا أعداد عشرية). `static bool IsSatisfied(int attended, int totalLectures, int thresholdPercent)` = `totalLectures>0 && attended >= MinLecturesRequired(...)`. `static decimal Percent(int attended, int total)` للعرض فقط. |
| `EligibilityRules.cs` | `static EligibilityResult Evaluate(EligibilityInput i)` — التفاصيل الكاملة في `06`. |
| `CourseStateMachine.cs` | `static bool CanTransition(CourseStatus from, CourseStatus to)` + `static IReadOnlyList<CourseStatus> Next(CourseStatus s)` — الجدول في `05`. |
| `PasswordPolicyRules.cs` | `static IReadOnlyList<string> Validate(string candidate, PasswordPolicy policy, IReadOnlyCollection<string> blockedNormalized, IEnumerable<string> contextWords)` تعيد رموز أخطاء (`PWD_TOO_SHORT`, `PWD_TOO_LONG`, `PWD_BLOCKED`, `PWD_CONTAINS_CONTEXT`, `PWD_NO_SPACES`, ...). |
| `CertificateReference.cs` | `static string Format(string pattern, int year, string code, int seq, int padding)`. |
| `NameRules.cs` | `static string Join(params string?[] parts)` = تقليم كل جزء وإسقاط الفارغ وضغط المسافات المتعددة وفصل بمسافة واحدة. |
| `LoginNameRules.cs` | `static bool IsExternalLogin(string s)` (4 أرقام بالضبط) و`NormalizeLogin(string s)` (تقليم + تحويل الأرقام الهندية إلى لاتينية). |
| `FileRules.cs` | التحقق من الامتداد **والنوع الحقيقي (magic bytes)** لـ: PDF، حزم OOXML (docx/pptx/xlsx كـ ZIP مع فحص `[Content_Types].xml`)، ppt (OLE2)، zip، JPEG، PNG. الامتدادات المسموحة لكل نوع ملف من الإعدادات `files.ext.*`. |

---

## 4. `ZuLms.Application`
### 4.1 `Common/`
| مجلد/ملف | المحتوى |
|---|---|
| `Abstractions/IAppDbContext.cs` | `DbSet<T>` لكل كيان في 3.3، و`Task<int> SaveChangesAsync(ct)`, و`Task<T> InTransactionAsync<T>(Func<Task<T>>, ct)` (معاملة Serializable/ReadCommitted حسب الحاجة). |
| `Abstractions/IClock.cs` | `DateTimeOffset UtcNow`, `DateOnly TodayAmman`, `TimeZoneInfo Amman`. |
| `Abstractions/ICurrentUser.cs` | `long? UserId`, `string? LoginName`, `IReadOnlyCollection<RoleCode> Roles`, `IReadOnlyCollection<long> DeanScopeCenterIds`, `string Ip`, `string UserAgent`, `UiLanguage Language`. |
| `Abstractions/IPermissionService.cs` | `Task<bool> HasAsync(long userId, string key, ct)`, `Task<EffectivePermissionSet> GetEffectiveAsync(userId, ct)` (مخزَّن في ذاكرة مؤقتة 30 ثانية ويُبطَل عند أي تعديل). |
| `Abstractions/ISettingsService.cs` | `Task<T> GetAsync<T>(string key, ct)` (من الذاكرة المؤقتة)، `SetAsync`. |
| `Abstractions/IEncryptionService.cs` | `string Encrypt(string plain)`, `string Decrypt(string cipher)` (AES-256-GCM، `03`). |
| `Abstractions/IPasswordHasher.cs` | `string Hash(string)`, `bool Verify(string, string)`. |
| `Abstractions/IEmailSender.cs` | `Task<EmailSendOutcome> SendAsync(EmailMessage, ct)` (النتيجة: `Sent`/`Failed`/`Uncertain`). |
| `Abstractions/IFileStorage.cs` | `SaveAsync(stream, name, kind) → StoredFile`, `OpenReadAsync(fileId)`, لا حذف فعلي. |
| `Abstractions/ICertificatePdfRenderer.cs` | `Task<byte[]> RenderAsync(CertificateRenderData, templatePdfBytes, layout, ct)`. |
| `Abstractions/IQrCodeService.cs` | `byte[] Png(string content, int pixels)`. |
| `Abstractions/IUniversityApiClient.cs` | `Task<IReadOnlyList<EmployeeDto>> GetEmployeesAsync(ct)`, `GetDepartmentsAsync`, `GetCentersAsync`, `GetEmailsAsync` (تتبع ترقيم ORDS، `04`). |
| `Abstractions/IAuditLogger.cs` | `Task LogAsync(AuditEntry entry, ct)` (`09`). |
| `Abstractions/INotificationService.cs` | `NotifyAsync(userIds, type, params…)`. |
| `Results/Result.cs`, `Error.cs` | `Result`, `Result<T>`, `Error(Code, Message?)`. الرموز تُعرَّف ثوابت في `Errors.cs` لكل ميزة. |
| `Models/PagedRequest.cs`, `PagedResult<T>.cs` | `Page`, `PageSize` (أقصى 100)، `Q`, `Sort`. |
| `Validation/ValidationBehavior.cs` | تشغيل المدققات قبل أي خدمة (يدوياً عبر مساعد، لا MediatR). |

### 4.2 `Features/` — مجلد لكل ميزة، وفي كل مجلد: `I{X}Service.cs`، `{X}Service.cs`، `Dtos/`، `Validators/`، `Errors.cs`
| الميزة | الخدمة | المسؤولية (المرجع) |
|---|---|---|
| `Auth` | `IAuthService` | تسجيل الدخول، التغيير الإلزامي، تجديد الجلسة، نسيت كلمة المرور (`03`) |
| `Users` | `IUserService` | إنشاء/تعديل/تعطيل/استعادة/بحث المستخدمين، تعيين الأدوار، استثناءات الصلاحيات (`03`) |
| `Credentials` | `ICredentialService` | عرض/تغيير كلمات المرور، الإرسال الجماعي الأولي، الكلمة المشتركة (`03`) |
| `Permissions` | `IPermissionAdminService` | الفهرس، أدوار↔صلاحيات، العرض الفعلي (`03`) |
| `Settings` | `ISettingsAdminService` | قراءة/تعديل الإعدادات + سياسة كلمات المرور + التلميحات (`03`) |
| `Sync` | `IUniversitySyncService` | الاستيراد والمراجعة (`04`) |
| `Centers` | `ICenterService` | تعديل `is_faculty` و`short_code` وتفعيل المراكز (`04`) |
| `Tracks` | `ITrackService` | المحاور (الجودة) (`05`) |
| `Courses` | `ICourseService` | إنشاء مباشر (جودة/أدمن)، تعديل، تعطيل/استعادة/أرشفة، فتح من جديد (`05`) |
| `Proposals` | `IProposalService` | اقتراح العميد، الإرجاع، إعادة الإرسال، الاعتماد، الرفض (`05`) |
| `Cancellations` | `ICancellationService` | طلب الإلغاء والموافقة (`05`) |
| `Participants` | `IParticipantService` | التسجيل الذاتي، إضافة/إخراج بواسطة الجودة/الأدمن (`05`) |
| `Lectures` | `ILectureService` | إضافة/تعديل/تعطيل المحاضرة، إنهاء الدورة، المواد (`05`/`06`) |
| `Attendance` | `IAttendanceService` | تفعيل الكود، إدخال الكود، التحديد اليدوي (`06`) |
| `Exams` | `IExamService` | إنشاء الامتحان، رصد النتائج، تعديل شرط الامتحان (`06`) |
| `Eligibility` | `IEligibilityService` | إعادة تقييم الاستحقاق وإصدار/إلغاء الشهادات (`06`) |
| `Certificates` | `ICertificateService` | الإصدار، التوليد PDF، التحقق العام، رفع القالب (`06`) |
| `Evaluation` | `IEvaluationService` | النموذج الموحد، النسخ، الإجابات، التمديد، التثبيت (`07`) |
| `Impact` | `IImpactService` | رفع الأثر، الإعادة، الاعتماد (`07`) |
| `Needs` | `INeedService` | الاحتياجات التدريبية (`07`) |
| `ExternalInstructors` | `IExternalInstructorService` | طلبات الحساب، الإنشاء من الأدمن (`05`) |
| `Profiles` | `IProfileService` | الملف المهني، دليل الأعضاء (`08`) |
| `ProCerts` | `IProCertService` | الشهادات المهنية: رفع، مراجعة، إحصائيات (`12`) |
| `Vault` | `IVaultService` | المستودع المركزي (`09`) |
| `Audit` | `IAuditQueryService` | استعلام السجل والتدفق المباشر (`09`) |
| `Notifications` | `INotificationQueryService` | الجرس (`09`) |
| `Stats` | `IStatsService` | إحصائيات الجامعة/الكلية/الدورة/العضو (`08`) |
| `Lookups` | `ILookupService` | قوائم البحث من الخادم (الأعضاء، الدورات، المراكز) |

> كل خدمة تتحقق من الصلاحية والنطاق **داخلها** (`IPermissionService` + قاعدة النطاق)، ولا تعتمد على فلتر الصفحة وحده.

---

## 5. `ZuLms.Infrastructure`
| مجلد | ملفات |
|---|---|
| `Persistence/` | `AppDbContext.cs` (يطبّق `IAppDbContext`، `UseSnakeCaseNamingConvention`، رمز التزامن `xmin`، **مرشّح استعلام عام** `IsActive==true` لكل `ISoftDeletable` ويتجاوزه الأدمن بـ `IgnoreQueryFilters()` عبر دوال `…IncludingInactive()` في الخدمات المخصصة)، `Configurations/*.cs` (ملف `IEntityTypeConfiguration<T>` لكل كيان بكل القيود والفهارس في `02`)، `Migrations/` (هجرة أولية واحدة `InitialCreate` ثم هجرة لكل تغيير)، `Seed/DbSeeder.cs` (روابط `02` §Seeds) |
| `Security/` | `AesGcmEncryptionService.cs`, `Pbkdf2PasswordHasher.cs`, `JwtTokenService.cs`, `RefreshTokenService.cs`, `CurrentUserAccessor.cs` (يقرأ من `HttpContext`) |
| `Email/` | `MailKitEmailSender.cs`, `EmailOutboxWorker.cs` (BackgroundService يعالج `email_outbox`)، `Templates/initial-credentials.{ar,en}.html`, `forgot-password.{ar,en}.html`, `external-instructor-account.{ar,en}.html` (بريد منسق بهوية الجامعة، RTL) |
| `Files/` | `LocalFileStorage.cs` (المسار من `ZU_STORAGE_ROOT`؛ اسم الملف المخزن GUID؛ يحسب SHA-256) |
| `Pdf/` | `QuestPdfCertificateRenderer.cs`, `CertificateLayout.cs` (يقرأ `layout_json`) |
| `Qr/` | `QrCodeService.cs` |
| `University/` | `UniversityApiClient.cs` (HttpClient + ترقيم + مهلة + إعادة محاولة بسيطة 3 مرات) |
| `Background/` | `EvaluationPinningWorker.cs` (كل ساعة)، `RefreshTokenCleanupWorker.cs` (يومياً)، `EmailOutboxWorker.cs` |
| `Time/` | `SystemClock.cs` |
| `DependencyInjection.cs` | `AddInfrastructure(IConfiguration)` |

الإعدادات السرية **من متغيرات البيئة فقط**: `ZU_DB_CONNECTION`, `ZU_ENC_KEYS` (JSON: `{ "current":"k1", "keys":{"k1":"<base64 32 bytes>"} }`), `ZU_JWT_KEY` (≥ 64 بايت base64), `ZU_SMTP_*`, `ZU_STORAGE_ROOT`, `ZU_PUBLIC_BASE_URL` (`https://portal.zu.edu.jo`). ممنوع وضعها في أي ملف مُلتزَم به.

---

## 6. `ZuLms.Web`
```
Program.cs
Extensions/            ServiceCollectionExtensions.cs, WebApplicationExtensions.cs
Middleware/            ExceptionHandlingMiddleware.cs, SecurityHeadersMiddleware.cs,
                       CorrelationIdMiddleware.cs, ForcedPasswordChangeMiddleware.cs
Security/              PermissionRequirement.cs, PermissionAuthorizationHandler.cs,
                       RequirePermissionAttribute.cs, CookieJwtEvents.cs, AntiforgeryAjaxFilter.cs
Localization/          SharedResource.cs, SharedResource.ar.resx, SharedResource.en.resx,
                       RequestCultureFromUserProvider.cs
Api/                   (Controllers، كلها تحت /api/v1، `[ApiController]`، تستدعي خدمة واحدة فقط)
  AuthController, UsersController, CredentialsController, PermissionsController,
  SettingsController, HintsController, SyncController, CentersController, TracksController,
  CoursesController, ProposalsController, CancellationsController, ParticipantsController,
  LecturesController, AttendanceController, ExamsController, ImpactController,
  EvaluationController, CertificatesController, NeedsController, ExternalInstructorsController,
  ProfilesController, VaultController, AuditController, NotificationsController, StatsController,
  LookupsController, FilesController
Areas/
  Public/Pages/        Login, ForgotPassword, ChangePassword, Verify, Error, AccessDenied
  Admin/Pages/         (08)
  Quality/Pages/       (08)
  Dean/Pages/          (08)
  Instructor/Pages/    (08)
  Member/Pages/        (08)
  Shared/Pages/        Profile, MembersDirectory (مشتركة لكل المسجّلين)
Pages/Shared/          _Layout.cshtml, _LayoutAuth.cshtml, _Sidebar*.cshtml (واحد لكل دور), _Topbar.cshtml,
                       _RoleSwitcher.cshtml, _LanguageToggle.cshtml, _NotificationBell.cshtml,
                       _ConfirmModal.cshtml, _DateTimePicker.cshtml, _FieldHint.cshtml,
                       _PagedTable.cshtml, _EmptyState.cshtml, _StatusBadge.cshtml
TagHelpers/            FieldHintTagHelper.cs, PermissionTagHelper.cs (`<zu-if-permission key="…">`)
wwwroot/
  css/                 site.css (هوية الجامعة)، prototype.css (منقول من البروتوتايب)، rtl.css, ltr.css
  js/                  api.js, i18n.js, hints.js, validation.js, pickers.js, charts.js, notifications.js
  js/pages/<area>/<page>.js   ملف لكل صفحة
  lib/                 bootstrap, jquery, jquery-validation, chartjs, datepicker (من التيمبليت)
  fonts/               Tajawal (woff2)
  img/                 شعار الجامعة (من البروتوتايب/التيمبليت)
appsettings.json       بلا أسرار
```

### 6.1 عقد الـ API (ثابت لكل نقاط النهاية)
- **نجاح:** `200/201` + `{ "ok": true, "data": … }`.
- **فشل:** `400/403/404/409/422` + `{ "ok": false, "code": "COURSE_NOT_APPROVED", "message": "<نص بلغة المستخدم>", "fieldErrors": { "title_ar": ["…"] } }`.
- **قوائم:** `GET …?page=1&pageSize=20&q=…&sort=field:asc` ← `{ ok:true, data:{ items:[…], total:n, page:1, pageSize:20 } }`.
- **وقت:** يستقبل الخادم التواريخ/الأوقات من الواجهة كنص محلي (`yyyy-MM-dd` و`HH:mm`) بتوقيت عمّان ويحوّلها هو إلى UTC.
- **CSRF:** كل `POST/PUT/PATCH/DELETE` يحمل ترويسة `RequestVerificationToken` (يضيفها `api.js` تلقائياً من `<meta name="csrf">`).
- **أخطاء الحقول:** `api.js` يربط `fieldErrors` بالحقول بحسب `name` ويعرضها تحت الحقل.
- **الأخطاء 401:** يعيد `api.js` توجيه المستخدم إلى `/login?returnUrl=…`.
- **التحقق على العميل:** `jquery-validation` بقواعد `data-rule-*` تعكس قواعد FluentValidation نفسها (الحقل العربي إلزامي، الحدود الدنيا/القصوى من الإعدادات تُحقن من الخادم في `data-*`). الخادم يعيد التحقق دائماً.

### 6.2 الصفحات والمسارات
مسار الصفحة: `/{area}/{page}`؛ مثل `/admin/users`, `/quality/proposals`, `/dean/propose`, `/instructor/courses/{id}/lectures`, `/member/available`. القوائم الجانبية لكل دور في ملف `_Sidebar{Role}.cshtml` وتُبنى من كتالوج الصفحات في `08` وتُخفي العنصر إذا فُقدت صلاحية عرضه.

### 6.3 اللغة والاتجاه
- الثقافات المدعومة: `ar` (افتراضي) و`en`. المزوّد `RequestCultureFromUserProvider`: لغة `users.preferred_language` للمسجّل، وإلا كوكي `zu_lang`، وإلا `ar`.
- `<html lang dir>`: `ar`/`rtl` أو `en`/`ltr`. حمّل `bootstrap.rtl.min.css` للعربية و`bootstrap.min.css` للإنجليزية. استخدم خصائص منطقية (`ms-*`, `me-*`, `text-start`) لا `left/right`.
- الأرقام لاتينية (0-9) في اللغتين.
- محتوى `_ar` إلزامي و`_en` اختياري؛ دالة عرض واحدة `Localized(ar, en)` ترجع `ar` إن كانت اللغة عربية أو كان `en` فارغاً.
- تبديل اللغة يحفظ `preferred_language` في الحساب فوراً (`PATCH /api/v1/auth/language`).
- **الواجهة فاتحة فقط**: لا `prefers-color-scheme: dark`، ولا زر تبديل مظهر.

### 6.4 تلميحات الحقول (Field Hints)
كل حقل نموذج تعرض الصفحة بجانبه أيقونة `?`. الوسم `<input zu-hint="course.title_ar" ...>` يولّد الأيقونة؛ عند التفاعل (تمرير/تركيز) يستدعي `hints.js` `GET /api/v1/hints?context={pageKey}` (ترويسة `Cache-Control: no-store`) ويعرض أحدث نص `hint_ar`/`hint_en`. بالتالي يظهر تعديل الأدمن مباشرة حتى في الصفحات المفتوحة دون إعادة نشر. مفاتيح الحقول (field_key) تُسجَّل تلقائياً في جدول `field_hints` عند أول تشغيل (إدخال بنص عربي افتراضي وتفعيل) لكل حقل وارد في `08`.

### 6.5 البحث
- قوائم **كبيرة** (المستخدمون، الأعضاء، الموظفون، الدورات، سجل التدقيق، الشهادات): مربع بحث + ترقيم صفحات من الخادم + تأخير 300ms.
- قوائم **قصيرة محدودة** (نمط الدورة، نعم/لا، الحالات، المحاور): قائمة منسدلة عادية بلا بحث.
- منتقي «العضو» في أي نموذج (اختيار محاضر، إضافة مشارك): منسدلة قابلة للبحث تستدعي `GET /api/v1/lookups/members?q=&centerId=` (حد أدنى حرفان، 20 نتيجة).

---

## 7. `tests/` (تفاصيلها في `10`)
- `Domain.Tests`: كل ما في `Rules/`.
- `Application.Tests`: الخدمات على قاعدة PostgreSQL حقيقية (Testcontainers).
- `Infrastructure.Tests`: التشفير، الـ Hash، الـ PDF، العميل الجامعي بـ MockHttp.
- `Web.Tests`: الصلاحيات على كل نقطة نهاية (WebApplicationFactory).
- `Architecture.Tests`: قاعدة الاعتماد في الأعلى + «لا `DateTime.Now`» + «لا `Html.Raw`» + «كل Controller لا يحقن DbContext».
