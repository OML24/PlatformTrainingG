# تقرير التعديلات المنفّذة لمطابقة الحزمة المعتمدة (المراحل 01–12)

**التاريخ:** 30 أكتوبر 2026
**المشروع المعتمد:** `newnew/zu-lms`
**المرجع الحاكم:** `newnew/zu-lms-build-packمعتمد/zu-lms-build-pack/` (الملفات `00` إلى `12`، مع تقدّمها على `PLATFORM_SPECIFICATION.md` عند التعارض)

---

## ملخص تنفيذي

اكتملت المراحل **01–09 و11 و12** من الحزمة المعتمدة وأُضيفت اختباراتها، وصار الحل يُبنى بـ**صفر تحذيرات وصفر أخطاء**، وتنجح جميع الاختبارات الآلية (**70/70** في `dotnet test`) وجميع فحوصات الـAPI والصفحات الحية (**311/311** في `tools/api-tests/api-test.mjs`)، مع تشغيل مزامنة ناجح (`Success`) على واجهات الجامعة الحقيقية.

> **تنبيه بخصوص الجاهزية للنشر:** المنصة جاهزة وظيفياً لاختبار القبول الداخلي. النشر الإنتاجي معلّق على: القالب الحقيقي للشهادة وإحداثياته، ونجاح بوابة Testcontainers PostgreSQL 17 على CI، واختبار SMTP فعلي. التفاصيل في `docs/api-test-report.md §6`.

---

## أولاً: توحيد عقد الـAPI والغلاف الموحّد (Envelope) والرسائل المترجمة

**المرجع في الحزمة:** `01 §5.4`، `03 §2`، `08 §2.1`.

1. **استحداث `src/ZuLms.Web/Middleware/ApiEnvelopeMiddleware.cs`:**
   - يغلّف جميع استجابات `/api/*` بصيغة موحّدة:
     - النجاح: `{ "ok": true, "data": ... }` (مع تحويل `204 No Content` إلى `200 OK` و`data: null`، والحفاظ على `201 Created` و`202 Accepted`).
     - الفشل: `{ "ok": false, "code": "...", "message": "...", "fieldErrors": { ... } }`.
   - يمرّر الاستجابات الثنائية (مثل تنزيل الملفات `GET /api/v1/files/{id}` بصيغة `application/pdf`) كما هي دون تغليف JSON.
   - يوحّد استجابات `401 Unauthorized` و`403 Forbidden` و`404 Not Found` و`405 Method Not Allowed` وأخطاء التحقق `400 VALIDATION_FAILED` ضمن الغلاف الموحّد.
2. **استحداث `src/ZuLms.Web/Middleware/ApiMessages.cs`:**
   - قاموس رسائل مترجم (عربي/إنجليزي) لجميع أكواد الأخطاء القياسية وأكواد المجال (`AUTH_INVALID`، `CSRF_INVALID`، `FORBIDDEN`، `CONFLICT`، `PROPOSAL_INVALID`، `CANCELLATION_*`، `PWD_*`، إلخ) بالاعتماد على `CultureInfo.CurrentUICulture`.
3. **تحديث `src/ZuLms.Web/Middleware/CorrelationIdMiddleware.cs` (`ExceptionHandlingMiddleware`):**
   - إرجاع `{ ok: false, code: "INTERNAL_ERROR", message, correlationId }` بحالة `500` عند وقوع أي استثناء غير معالج في مسارات `/api/*`.
4. **توحيد أسماء خصائص الترقيم في القوائم (`Rows` → `Items`):**
   - تعديل السجلات (DTOs) لتطابق العقد `{ items, total, page, pageSize }`:
     - `src/ZuLms.Application/Features/Users/Dtos/UserDtos.cs` (`CredentialsDirectoryPageDto`).
     - `src/ZuLms.Application/Features/Courses/ICourseProposalQueryService.cs` (`CourseProposalPageDto`).
     - `src/ZuLms.Application/Features/Courses/ICourseDirectoryService.cs` (`CourseDirectoryPageDto`).
     - `src/ZuLms.Application/Features/Courses/ICourseRegistrationService.cs` (`AvailableCoursePageDto`).
     - `src/ZuLms.Application/Features/Courses/IExternalInstructorAccountService.cs` (`ExternalAccountRequestPageDto`).
     - `src/ZuLms.Application/Features/Sync/IUniversitySyncAdminService.cs` (`SyncReviewPageDto`).

---

## ثانياً: حماية CSRF والتفويض ونقاط المصادقة واللغة والمنطقة الزمنية

**المرجع في الحزمة:** `01 §5.4`، `03 §2–§6`، `08 §2.2`.

1. **استحداث `src/ZuLms.Web/Security/ApiAntiforgeryAttribute.cs`:**
   - مرشّح `IAsyncActionFilter` شامل يتحقق من ترويسة `RequestVerificationToken` على جميع طلبات `POST` و`PUT` و`PATCH` و`DELETE` في مسارات `/api/*`، ويرفض الطلبات غير الصالحة بالحالة `400` والكود `CSRF_INVALID`.
   - تسجيل المرشّح عالمياً في `Program.cs` عبر `AddControllers(o => o.Filters.Add<ApiAntiforgeryAttribute>())`.
2. **استكمال نقاط المصادقة في `src/ZuLms.Web/Security/AuthEndpoints.cs` و`ContextEndpoints.cs`:**
   - إضافة التحقق من CSRF على جميع العمليات المغيّرة للحالة (`/login`، `/refresh`، `/change-password`، `/account-password`، `/logout`، `/forgot-password`، `/language`، و`POST /api/v1/context`).
   - إضافة `GET /api/v1/auth/me` لإرجاع بيانات الجلسة الحالية (`userId`، `loginName`، `fullNameAr`، `preferredLanguage`، `roles`، `passwordChangeOnly`).
   - إضافة `PATCH /api/v1/auth/language` لتغيير لغة واجهة المستخدم وتحديث كوكي `zu_lang` وإعادة إصدار التوكنات.
3. **دعم صلاحيات متعدّدة بمنطق OR في `src/ZuLms.Web/Security/PermissionAuthorization.cs`:**
   - تعديل `RequirePermissionAttribute` ليقبل `params string[] keys` وبناء اسم سياسة مركّب مفصول بـ`|`، وتحديث `PermissionPolicyProvider` و`PermissionAuthorizationHandler` للتحقق من امتلاك أيٍّ من الصلاحيات المطلوبة.
   - تحديث `CourseProposalsController.Get` ليسمح بـ`RequirePermission("course.propose", "proposal.decide")`، و`Resubmit` ليستخدم `RequirePermission("proposal.resubmit")` (وتحديث `CourseProposalService.ResubmitAsync` ليقبل `proposal.resubmit` أو `course.propose`).
   - إضافة `[Authorize]` على `src/ZuLms.Web/Api/FilesController.cs`.
4. **اللغة والمنطقة الزمنية:**
   - استحداث `src/ZuLms.Web/Localization/RequestCultureFromUserProvider.cs` لقراءة اللغة من كوكي `zu_lang` ثم مطالبة JWT `lang` ثم العربية افتراضياً.
   - استحداث `src/ZuLms.Application/Common/Serialization/AmmanDateTimeOffsetConverter.cs` لتطبيع جميع قيم `DateTimeOffset` في JSON إلى المنطقة الزمنية `Asia/Amman`، وتسجيله في `Program.cs`.

---

## ثالثاً: ميزة التلميحات الحقلية (Field Hints) كاملة

**المرجع في الحزمة:** `01`، `04`، `08 §4.3`.

1. **واجهة وتنفيذ الخدمة:**
   - استحداث `src/ZuLms.Application/Features/Hints/IHintService.cs` و`HintService.cs` لدعم جلب التلميحات المفعّلة حسب السياق (`GetByContextAsync`)، وسرد الدليل الإداري (`ListAllAsync`)، والإنشاء (`CreateAsync`)، والتعديل (`UpdateAsync`)، والتعطيل الناعم (`DeactivateAsync`) مع تسجيل التدقيق (`AuditCategory.Settings`).
2. **نقطة النهاية ومساعد الوسوم:**
   - استحداث `src/ZuLms.Web/Api/HintsController.cs` على المسار `/api/v1/hints` (`GET`، `POST`، `PUT {id}`، `DELETE {id}`).
   - استحداث `src/ZuLms.Web/TagHelpers/FieldHintTagHelper.cs` (`<field-hint field-key="..." context-key="..." />`) وتسجيله في `Pages/_ViewImports.cshtml`.
3. **واجهة إدارة التلميحات وتشغيلها:**
   - استحداث صفحة `src/ZuLms.Web/Pages/Admin/Hints.cshtml` و`.cshtml.cs` محمية بصلاحية `page.admin.hints`.
   - استحداث `src/ZuLms.Web/wwwroot/js/hints.js` (تفعيل التلميحات في النماذج) و`wwwroot/js/hints-admin.js` (واجهة الإدارة الكاملة).

---

## رابعاً: العميل الموحّد للـJavaScript ومكوّن التاريخ والوقت والصفحات الناقصة

**المرجع في الحزمة:** `00`، `03 §7`، `04`، `05`، `08 §3–§6`.

1. **استحداث `src/ZuLms.Web/wwwroot/js/api.js` (`ZuLms.api`):**
   - عميل `fetch` موحّد يجلب توكن CSRF تلقائياً ويرفقه في `RequestVerificationToken`، ويفكّ غلاف `{ ok, data }`، ويعرض رسائل الأخطاء المترجمة عبر `ZuLms.toast`.
   - ترحيل جميع ملفات السكربتات في `wwwroot/js/` لاستخدام `ZuLms.api` و`data.items` بدلاً من استدعاءات `fetch` المباشرة و`data.rows` (`login.js`، `change-password.js`، `forgot-password.js`، `account-password.js`، `password-policy.js`، `credentials.js`، `settings.js`، `sync.js`، `centers.js`، `users.js`، `dean-propose.js`، `quality-proposals.js`، `direct-course.js`، `external-instructors.js`، `available-courses.js`، `course-participants.js`، `cancellations.js`، `course-directory.js`، `course-details.js`، `role-switcher.js`).
2. **استبدال حقول التاريخ الأصلية بمكوّن `_DateTimePicker`:**
   - نسخ مكتبتي `moment` و`daterangepicker` إلى `src/ZuLms.Web/wwwroot/lib/`.
   - استحداث `src/ZuLms.Web/Pages/Shared/_DateTimePicker.cshtml` و`wwwroot/js/pickers.js`.
   - استبدال جميع حقول `<input type="date">` و`<input type="datetime-local">` في `Dean/Propose.cshtml`، `Quality/Proposals.cshtml`، `Shared/_DirectCourseForm.cshtml`، و`Shared/_CourseDirectory.cshtml`.
3. **إضافة صفحات الهبوط ولوحات التحكم والصفحات الناقصة:**
   - `Pages/Admin/Dashboard.cshtml` (+ `wwwroot/js/admin-dashboard.js`).
   - `Pages/Quality/Dashboard.cshtml` (+ `wwwroot/js/quality-dashboard.js`).
   - `Pages/Dean/Dashboard.cshtml` (+ `wwwroot/js/dean-dashboard.js`).
   - `Pages/Member/Dashboard.cshtml` (+ `wwwroot/js/member-dashboard.js`).
   - `Pages/Instructor/Courses.cshtml`.
   - `Areas/Public/Pages/AccessDenied.cshtml`.
   - ربط سياسات التفويض لهذه الصفحات في `src/ZuLms.Web/Program.cs`، وتحديث القالب العام `Pages/Shared/_Layout.cshtml` ومبدّل الأدوار `Pages/Shared/_RoleSwitcher.cshtml`، وإضافة مفاتيح الترجمة `Nav.*` و`Hints.*` في `Resources/SharedResource.ar.resx` و`.en.resx`، واستحداث `wwwroot/js/i18n.js` و`wwwroot/js/site.js`.

---

## خامساً: إصلاحات أخطاء منطق التطبيق (Runtime Bugs) المكتشفة أثناء فحص الـAPI

1. **`src/ZuLms.Application/Features/Auth/AuthService.cs`:**
   - **حماية تسجيل الدخول الفارغ:** التحقق من `string.IsNullOrWhiteSpace(request.LoginName) || string.IsNullOrEmpty(request.Password)` وإرجاع `AUTH_INVALID` (`400`) بدلاً من انهيار `LoginNameRules.NormalizeLogin(null)` بـ`ArgumentNullException` (`500`).
   - **حماية مسارات كلمات المرور واستعادة الحساب:** التحقق من القيم الفارغة/`null` في `ChangePasswordAsync` و`ChangeOwnPasswordAsync` و`ForgotPasswordAsync` لمنع استثناءات `ArgumentNullException` وإرجاع أكواد خطأ معيارية (`AUTH_INVALID` / `PWD_TOO_SHORT` / رد عام آمن في `forgot-password`).
   - **مسار الهبوط للحسابات بلا أدوار:** تعديل `Landing` ليحوّل الحسابات التي لا تمتلك أي دور فعّال إلى `/access-denied` بدلاً من `/courses/available` التي تتطلب صلاحية.
2. **`src/ZuLms.Application/Features/Credentials/CredentialService.cs`:**
   - التحقق من `string.IsNullOrEmpty(newPassword)` في `ChangeAsync` وإرجاع `PWD_TOO_SHORT` قبل استدعاء `PasswordPolicyRules.Validate`.
3. **`src/ZuLms.Application/Features/Courses/DirectCourseService.cs`:**
   - السماح بخيار `DirectInstructorChoice.ManagedByQuality` في `ValidateAsync`، وعدم قراءة `input.ExternalNameAr!.Trim()` عند اختيار `ManagedByQuality` في `CreateAsync` (إصلاح `NullReferenceException` كان يتسبّب بخطأ `500` عند إنشاء دورة مباشرة تابعة للجودة).
4. **`src/ZuLms.Application/Features/Org/CenterAdminService.cs`:**
   - فحص وجود المركز في قاعدة البيانات قبل التحقق من `shortCode` ليرجع `404 CENTER_NOT_FOUND` بدلاً من `400`.
5. **`src/ZuLms.Application/Features/Courses/ExternalInstructorAccountService.cs`:**
   - فحص وجود طلب حساب المحاضر الخارجي أولاً في `CompleteAsync` ليرجع `404 EXTERNAL_REQUEST_NOT_FOUND` قبل أخطاء التحقق من اسم الدخول (`400`).
6. **ترحيل قاعدة البيانات:**
   - تطبيق الترحيل `20261029155309_CourseArchiveNotifications` بالكامل ليصبح مجموع الترحيلات المطبّقة **6 ترحيلات** متطابقة مع نموذج وقت التشغيل.
7. **إضافة حزمة اختبارات الـAPI القابلة لإعادة التشغيل داخل المستودع:**
   - استحداث `tools/api-tests/api-test.mjs` و`tools/api-tests/run-app.ps1` و`tools/api-tests/README.md`.

---

## سادساً: المرحلة 06 — محرك الشهادات الكامل (PDF + QR + التحقق العام)

**المرجع في الحزمة:** `06 §5–§7`.

1. **`CertificateService`** (`src/ZuLms.Application/Features/Certificates/`): إصدار ذري داخل المعاملة، ترقيم تسلسلي عبر `NextSequenceAsync`، بناء الرقم المرجعي وفق `cert.reference_pattern` وحشو `cert.reference_padding` ورمز `cert.general_short_code`، لقطة بيانات الحامل والدورة والمحاضر، حالات `Issued`/`Revoked` مع سبب الإلغاء، وإصدار المعلّق (`IssuePending`).
2. **`CertificateTemplateService`**: رفع قالب PDF صفحة واحدة مع `layout_json`، تحقق المخطط والأبعاد، ترقيم `version_no+1` وإبقاء القديم مع `is_current`، ومعاينة PDF ببيانات تجريبية صريحة.
3. **`QuestPdfCertificateRenderer`** (`src/ZuLms.Infrastructure/Pdf/`): تركيب العناصر فوق القالب (الاسم، عنوان الدورة، الفترة، الساعات، المحاضر، تاريخ الإصدار، الرقم المرجعي، QR) بخط Tajawal وتقليص الاسم الطويل ثم قصّه.
4. **`QrCodeService`** + `GET /api/v1/public/verify/{reference}`: تحقق عام بلا مصادقة يعيد بطاقة الشهادة أو لا شيء للمرجع المجهول، ويحترم `cert.verify_rate_per_minute`.
5. **الربط بمحرك الأهلية:** `EligibilityRecalculationService` يصدر الشهادة أو يبقيها معلّقة (`CERT_TEMPLATE_MISSING` / `CERT_RENDERER_PENDING`) دون إفشال العملية، ويُلغي الشهادة عند فقد الشروط مع السبب والإشعار والتدقيق.

---

## سابعاً: المرحلة 07 — التقييم والأثر والاحتياجات

1. `EvaluationQueryService`: نتائج التقييم لكل سؤال (عدد الإجابات والنسبة والمتوسط) وتمديد فترة التقييم (الجودة تحتاج تاريخاً أحدث، والأدمن يغيّره) مع إعادة تشغيل تثبيت النسخ المستحقة.
2. `ImpactService`: رفع ملف الأثر، منع الملف المطابق بـSHA-256، السماح بنسخة جديدة بعد `Returned` فقط، ومراجعة الجودة أو عميد الكلية حسب مسار الدورة (مع صلاحية شاملة للأدمن) باعتماد أو إعادة بملاحظات إلزامية وإعادة حساب الأهلية.
3. `NeedService`: تقديم العميد بالكتاب، إعادة الإرسال بعد الإعادة، قرار الجودة، وتعطيل الأدمن، مع سجل الأحداث والإشعارات.

---

## ثامناً: المرحلة 08 — الصفحات والملفات المشتركة

- صفحات جديدة: `/admin/audit`، `/admin/live-feed`، `/admin/needs`، `/admin/certificate-template`، `/vault`، `/quality/needs`، `/quality/eval-form`، `/quality/evaluations`، `/quality/tracks`، `/quality/certificates`، `/quality/pro-certs`، `/quality/pro-cert-stats`، `/member/pro-certs`، `/member/impact`، `/member/teaching`، `/member/enter-code`، `/dean/members`، `/dean/needs`، `/shared/directory`، `/verify`، مع جرس الإشعارات في `_Layout` وربط الروابط بالصلاحيات.
- `ProfileService`: دليل الأعضاء والملف المشترك (شهادات + شهادات مهنية)، و`TrackAdminService` لإدارة المحاور.

---

## تاسعاً: المرحلة 09 — الملفات والتدقيق والإشعارات

- `FileUploadService`: تحقق الصلاحية والامتداد من `files.ext.*` والحجم من `files.max_mb.*` والنوع الحقيقي (magic bytes)، تخزين خارج `wwwroot`، SHA-256، وربط تلقائي بالمستودع للأصناف الرسمية.
- `VaultService`: ربط الوثائق وتصنيفها `QualityMinutes`/`Recommendation` والتنزيل المحكوم.
- `AuditQueryService`: السرد والتصفية والبث الحي (`/live?afterId=`) وتصدير CSV.
- `NotificationService`: إشعارات داخلية، عدّاد غير المقروء، وتعليم الكل كمقروء.

---

## عاشراً: المرحلة 12 — الشهادات المهنية

`ProCertService`: تقديم/تعديل/إعادة إرسال/سحب، قرار الجودة، تعطيل الأدمن، السرد والتصفية، الإحصاءات (`procert.stats_top_n`) وتصدير CSV، والتصنيفات المهنية الثمانية.

---

## حادي عشر: المرحلة 11 — ملفات النشر

`deploy/Dockerfile`، `deploy/docker-compose.yml`، `deploy/.env.sample`، `deploy/nginx.conf.sample`، `deploy/docs/backup.md`، و`.github/workflows/deploy.yml`، إضافةً إلى `GET /health` (`{"status":"healthy"}`) للفحص الآلي.

---

## ثاني عشر: إصلاحات جولة 30 أكتوبر 2026 (مزامنة حقيقية + قبول شامل)

1. **متانة مصدر البريد (`UniversitySourceMapper.TryEmail` + `UniversitySyncCoordinator.ApplyEmailsAsync`):** واجهة `qa/email` تُرجع صفاً بلا `employee_id`/`side_id`؛ كان يُسقط المصدر كاملاً. الآن يُتخطّى الصف ويُسجّل تحذير واحد `SKIPPED_INVALID_ROWS:<n>` في `stats_json`، مع بقاء قاعدة الحزمة «فشل أي صفحة = فشل المصدر كاملاً». **النتيجة:** التشغيل التالي انتهى `Success` (emails: fetched 2203، unchanged 1684، أضيف 0).
2. **إضافة الإعداد المفقود `files.ext.cert_template = pdf`** في `src/ZuLms.Infrastructure/Persistence/Seed/settings.json` (كان `files.max_mb.cert_template` موجوداً وحده، فكان رفع القالب يفشل بـ`500`). الإعداد يُضاف تلقائياً عند بدء التشغيل لقواعد البيانات القائمة لأن البذرة تُكمل المفاتيح الناقصة.
3. **أدوات فحص قابلة لإعادة التشغيل:** `tools/api-tests/seed-fixtures.mjs` (ينشئ حسابي الموظفين والعميد الثلاثة بكلمة مرور PBKDF2 ونص مشفّر AES-GCM مطابق لصيغة التطبيق، ويختار الكلية بنفس ترتيب واجهة `/api/v1/centers`)، و`tools/api-tests/sync-run.mjs` (تشغيل مزامنة واستطلاع حالتها حتى الانتهاء).
4. **تصحيحات على أداة الفحص:** تمرير أوقات المحاضرات كوقت عمّان (`DateTimeKind.Unspecified`) كما يتطلب العقد، أخذ معرّف المشارك من كشف الحضور بدل قائمة المشاركين، مطابقة توقّع `/dean/members` لمصفوفة صلاحيات `03` (403 للأدمن)، ومقارنة اسم حامل الشهادة باسم المستخدم الفعلي بدل ثابت قديم.
5. **اختبارات جديدة:** `UniversitySourceMapperTests` تغطي تخطّي صفوف البريد الناقصة وقبول الصف الصالح.

---

## ثالث عشر: التحقق النهائي

| البند | النتيجة |
|---|---|
| `dotnet build ZuLms.sln` (Debug/Release) | 0 تحذير، 0 خطأ |
| `dotnet test ZuLms.sln` | **70/70** (Domain 40، Application 11، Infrastructure 6، Architecture 6، Web 7) |
| `node tools/api-tests/api-test.mjs` | **311/311** (A 13، B 23، C 79، D 24، E 36، G 12، H 42، I 77، F 5) |
| صفحات Razor | 67 فحصاً لـ38 مساراً — كلها مطابقة |
| مزامنة الجامعة الحقيقية | `Success` (مراكز 103، أقسام 418، موظفون 11506، بريد 2203) |
| `node --check` لملفات الأداة | ناجح |
