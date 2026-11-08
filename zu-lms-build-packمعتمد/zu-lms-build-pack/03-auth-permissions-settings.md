# 03 — الدخول وكلمات المرور والصلاحيات والإعدادات

## 1. أنواع الحسابات وأسماء الدخول
| النوع | `login_name` | كيف يُنشأ | أول دخول | من يغيّر كلمة مروره |
|---|---|---|---|---|
| `Employee` | الرقم الوظيفي كنص (`employee_code`) | آلياً من الاستيراد (`04`) | إلزامي: تغيير كلمة المرور | المستخدم نفسه + الأدمن |
| `ExternalInstructor` | 4 أرقام من النطاق (`auth.external_login_min..max`) | الأدمن بعد طلب الجودة (`05`) | لا تغيير إلزامي، لا كلمة مشتركة | **الأدمن فقط** |
| `Local` | نص `^[A-Za-z0-9_.-]{3,20}$` | البذرة الأولى فقط (`02 §8`) | لا تغيير إلزامي | الأدمن |

- **تفرّد أسماء الدخول:** عمود `login_name` فريد على كل الحسابات. عند إنشاء مدرب خارجي: ارفض أي رقم موجود، وارفض أيضاً أي رقم يطابق `employee_code` لموظف في `employees` (حتى لو لم يُنشأ له حساب) وأي رقم خارج النطاق. زر «اقترح رقماً متاحاً» يختار أصغر رقم حرّ في النطاق.
- عند إنشاء حساب موظف من الاستيراد وكان `login_name` مأخوذاً: لا تنشئ الحساب، وأنشئ `sync_review_items` بسبب `DuplicateLoginName`.
- تطبيع الإدخال عند الدخول: تقليم المسافات، وتحويل الأرقام الهندية (٠-٩) إلى لاتينية.

## 2. تخزين كلمات المرور (قرار صاحب المتطلبات، مخاطرة مقبولة)
لكل حساب ثلاثة أعمدة في `users`:
1. `password_hash` — للتحقق عند الدخول. **PBKDF2-HMAC-SHA256، 600000 دورة، ملح 16 بايت، ناتج 32 بايت.** الصيغة `v1$600000$<b64salt>$<b64hash>`. مقارنة بزمن ثابت (`CryptographicOperations.FixedTimeEquals`). إن كان عدد الدورات المخزّن أقل من المعتمد حالياً أعد الـ Hash بعد دخول ناجح.
2. `password_enc` — الكلمة **الحالية** مشفّرة (للعرض الإداري و«نسيت كلمة المرور»).
3. `initial_password_enc` — الكلمة الأولية **الشخصية** مشفّرة (Employee فقط). تبقى محفوظة للعرض الإداري بعد أول دخول لكنها لا تُقبل للدخول.

**التشفير:** AES-256-GCM. نونس 12 بايت عشوائي لكل تشفير. الصيغة النصية `v1:{kid}:{b64nonce}:{b64ciphertext}:{b64tag}`. AAD ثابت `zu-lms-v1`. المفاتيح من `ZU_ENC_KEYS` (انظر `01 §5`)؛ التشفير بالمفتاح `current` وفك التشفير بالمفتاح المذكور في `kid`. أداة سطر أوامر `dotnet ZuLms.Web.dll --reencrypt-secrets` تعيد تشفير كل الأعمدة بالمفتاح الحالي بعد تدوير المفتاح.

**التطبيع:** طبّع كل كلمة مرور بـ Unicode NFC قبل الـ Hash أو التشفير أو المقارنة.

## 3. توليد الكلمة الأولية الشخصية
الطول `clamp(16, min_length, max_length)` محرفاً عشوائياً (`RandomNumberGenerator`). الأحرف من `ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789`، والرموز من `!@#%*-_+=?` فقط. ضمّن محرفاً واحداً على الأقل من كل صنف تفرضه السياسة الحالية (كبير/صغير/رقم/رمز)، ثم اخلط ترتيب المحارف عشوائياً. يجب أن تجتاز سياسة كلمات المرور الحالية (وإلا ولّد من جديد حتى 10 محاولات ثم أعد خطأ واضحاً). عند الإنشاء: `password_enc = initial_password_enc`، و`password_hash` من الكلمة نفسها، `must_change_password = true`, `first_login_completed_at = NULL`.

## 4. خوارزمية تسجيل الدخول (`IAuthService.LoginAsync`) — نفّذها حرفياً
المدخلات: `loginName`, `password`, `rememberMe`.
1. طبّع `loginName` وكلمة المرور. طبّق تحديد المعدل (`auth.login_max_per_ip_minute`) وإلا `429 RATE_LIMITED`.
2. ابحث عن المستخدم بـ `login_name` (بما فيه المعطّل). غير موجود → نفّذ عملية تحقق وهمية (لتساوي الزمن) ثم `AUTH_INVALID`.
3. إن كان `locked_until > الآن` → `AUTH_LOCKED` (مع الوقت المتبقي). سجّل المحاولة.
4. تحقق من الاعتماد:
   - **الحالة أ (موظف لم يُكمل أول دخول: `first_login_completed_at IS NULL`):** يُقبل أحد اثنين: (1) كلمة مطابقة لـ `initial_password_enc` بعد فك التشفير (مقارنة بزمن ثابت)، (2) كلمة مطابقة لـ `auth.shared_initial_password` (إن كانت غير فارغة) بعد فك تشفيرها. إن لم يطابق أيهما → فشل.
   - **الحالة ب (غير ذلك):** `Verify(password, password_hash)`.
5. عند الفشل: `failed_login_count++`؛ إن بلغ `auth.max_failed_logins` فاضبط `locked_until = الآن + auth.lockout_minutes` وصفّر العداد. سجّل `auth.login_failed`. أعد `AUTH_INVALID` (رسالة عامة لا تفصح عن السبب).
6. عند النجاح (الاعتماد سليم) افحص الأهلية **بالترتيب**:
   - `is_active=false` → `AUTH_DISABLED`.
   - للموظف: `emp_status` ليس ضمن `access.eligible_emp_status_codes` **و** `access_override_allow=false` → `AUTH_NOT_ELIGIBLE`.
   - مصدر الموظف `source_missing=true`: لا يمنع الدخول (قرار الوثيقة).
   (الرسائل: «الحساب غير مفعّل. راجع إدارة المنصة.» / «حسابك غير مؤهل للدخول حالياً. راجع إدارة المنصة.»)
7. صفّر `failed_login_count` و`locked_until`، حدّث `last_login_at`.
8. إن كانت الحالة أ (أول دخول) → أصدر جلسة **محدودة** بمطالبة `pwd_change=true` ووجّه إلى `/change-password`. سجّل `auth.login_first` وإن كانت الكلمة المشتركة هي المستخدمة سجّل `auth.login_shared_initial`.
9. وإلا أصدر جلسة كاملة. سجّل `auth.login`.
10. وجّه إلى صفحة الهبوط بحسب السياق (§7).

**تغيير كلمة المرور الإلزامي (`/change-password`):** حقول: كلمة جديدة + تأكيدها. التحقق بـ `PasswordPolicyRules.Validate` (السياق: الاسم، `login_name`، الكلمة الأولية الشخصية، الكلمة المشتركة الحالية). الكلمة الجديدة يجب أن تختلف عن الأولية الشخصية وعن المشتركة. عند النجاح: `password_hash` و`password_enc` الجديدتان، `must_change_password=false`, `first_login_completed_at=الآن`, `password_changed_at`, **تجديد `security_stamp`**، إبطال كل refresh tokens السابقة، إصدار جلسة كاملة جديدة. **من هذه اللحظة لا تُقبل أي كلمة أولية لهذا الحساب أبداً.**

**تغيير الكلمة من الموظف لاحقاً (`/account/password`):** كلمة حالية + جديدة + تأكيد، بنفس السياسة، ثم تجديد `security_stamp` وإبطال الجلسات الأخرى. المدرب الخارجي لا يرى هذه الصفحة.

## 5. الجلسات و JWT (قرار مُعدّ الحزمة [م])
- **الواجهة صفحات Razor**، والمتصفح يتنقل بالكوكيز، لذا التوكن في **كوكي HttpOnly** وليس في JavaScript.
- `zu_at`: JWT (HS256، المفتاح `ZU_JWT_KEY`، `iss=aud="zu-lms"`)، صلاحيته `auth.access_token_minutes` (15). مطالبات: `sub`=user id، `login`، `name`، `stamp`=security_stamp، `roles` (مصفوفة رموز الأدوار الفعّالة بما فيها Instructor المشتق)، `lang`، `jti`، وللجلسة المحدودة `pwd_change=true`. كوكي `HttpOnly; Secure; SameSite=Strict; Path=/`.
- `zu_rt`: Refresh token = 64 بايت عشوائي base64url. يُخزَّن في `refresh_tokens` كـ SHA-256 فقط. نفس خصائص الكوكي. **بلا «تذكرني»:** كوكي جلسة (بلا Expires) وصلاحية الخادم المطلقة `auth.session_hours` (12). **مع «تذكرني»:** كوكي دائم و`auth.remember_me_days` (30) مطلقة.
- **التجديد الصامت:** الوسيط `SilentRefreshMiddleware` (قبل المصادقة): إذا غاب `zu_at` أو انتهى وكان `zu_rt` صالحاً → دوّر التوكن (أصدر refresh جديداً، علّم القديم `used_at`، اربطهما `replaced_by_id`) وأصدر `zu_at` جديداً وتابع الطلب. إن قُدِّم refresh **مستعمل سابقاً** → اعتبره سرقة: أبطل **العائلة كلها** (`family_id`)، سجّل `security.refresh_reuse` وأعد 401.
- **الإبطال الفوري:** عند كل طلب يتحقق `OnTokenValidated` من (`is_active`، تطابق `stamp`، الأهلية) عبر ذاكرة مؤقتة 30 ثانية يبطلها `IUserStateCache.Invalidate(userId)` عند: تغيير كلمة المرور، تعطيل الحساب، تغيير الأدوار، تعديل الاستثناءات، تغيير `access_override_allow`، وصول حالة وظيفية جديدة من المزامنة. أي فشل → 401.
- **الخروج:** يُبطل refresh الحالي ويحذف الكوكيين ويسجّل `auth.logout`.
- **الجلسة المحدودة** (`pwd_change=true`): الوسيط `ForcedPasswordChangeMiddleware` يسمح فقط بـ `/change-password` و`/api/v1/auth/change-password` و`/logout` و`/lib` و`/css` و`/js` و`/fonts`؛ غير ذلك → إعادة توجيه إلى `/change-password`.
- **مدة الجلسة المحدودة:** `auth.access_token_minutes` فقط، ولا يُصدر لها refresh token ولا تقبل «تذكرني». تدوير كلمة المرور بعد `rotation_days` يسري على حسابات `Employee` فقط؛ حسابا `ExternalInstructor` و`Local` مستثنيان. نموذج التغيير الإلزامي لا يطلب الكلمة الحالية.
- **CSRF:** `AddAntiforgery` بترويسة `RequestVerificationToken`. يُرفق `api.js` الرمز في كل طلب كتابة. صفحات Razor تستخدم التحقق التلقائي.
- **ترويسات أمان** (وسيط `SecurityHeadersMiddleware`): `Strict-Transport-Security`, `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: same-origin`, `Content-Security-Policy: default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self'; frame-ancestors 'none'` (لا `unsafe-inline` للسكربت؛ ولذلك لا تكتب سكربتاً داخل الصفحات، فقط ملفات `wwwroot/js`).
- **خلف Reverse Proxy:** فعّل `UseForwardedHeaders` (`XForwardedFor|XForwardedProto`) والبروكسيات الموثوقة من إعداد `KnownProxies` في البيئة.

## 6. نسيت كلمة المرور (موظفون فقط)
الصفحة العامة `/forgot-password`: حقلان: **الرقم الوظيفي** و**البريد الجامعي**.
1. حدّ المعدل: `auth.forgot_max_per_account_hour` (3) لكل رقم و`auth.forgot_max_per_ip_hour` (10) لكل IP. تجاوز → استجابة عامة (لا تكشف) وسجل `RateLimited`.
2. ابحث عن مستخدم `Employee` فعّال بهذا `login_name`. قارن البريد المُدخل (أحرف صغيرة، بلا مسافات) بالبريد **الفعّال** (`email_override ?? employees.source_email`). لا تطابق → `NoMatch`.
3. إن كان البريد الفعّال يخص أكثر من حساب فعّال وكان `auth.forgot_block_shared_email=true` → لا ترسل، أنشئ `sync_review_items` بسبب `SharedEmail`، وسجل `SharedEmail`.
4. المطابقة → فك تشفير `password_enc` وأنشئ صف `email_outbox` (`ForgotPassword`) وأرسل إلى ذلك البريد فقط.
5. **الاستجابة للمستخدم دائماً واحدة:** «إذا كانت البيانات مطابقة فستصلك رسالة على بريدك الجامعي.» بزمن استجابة متقارب في كل الحالات. لا تظهر الكلمة في الشاشة ولا في أي سجل.
6. المدرب الخارجي ليس له هذه الميزة (يراجع الأدمن).

## 7. السياق والتوجيه بعد الدخول (متعدد الأدوار)
- أدوار المستخدم الفعّالة تُحسب: أدواره في `user_roles` (Admin, Quality, Dean, Member) + **Instructor مشتق** إن وُجدت دورة `is_active` و`status IN (Approved, Finished)` و`instructor_user_id = هو` + `ExternalInstructor` لحسابات المدرب الخارجي.
- كل `Employee` يحصل على `Member` عند الإنشاء. `ExternalInstructor` لا يملك `Member`.
- **الهبوط الافتراضي بالأولوية:** Admin → `/admin/dashboard`، Quality → `/quality/dashboard`، Dean → `/dean/dashboard`، Member → `/member/dashboard`، ExternalInstructor → `/instructor/courses`، Local → `/admin/dashboard`.
- **مبدّل السياق** في الهيدر وفي الشريط الجانبي (`_RoleSwitcher.cshtml`): قائمة منسدلة بالبيئات المتاحة للمستخدم (إدارة النظام، الجودة، العميد، دوراتي كمحاضر، عضو هيئة تدريس). الاختيار يحفظ كوكي `zu_ctx` ويوجّه إلى هبوط تلك البيئة دون تسجيل خروج. القيمة تُتحقق دائماً في الخادم من الأدوار الفعلية.

## 8. إدارة الحسابات للأدمن (`ICredentialService`, `IUserService`)
**دليل الحسابات (`/admin/credentials`):** جدول بحث من الخادم (الاسم/الرقم، 300ms) بأعمدة: الاسم، رقم الدخول، النوع، الكلية، الحالة (فعّال/معطّل)، «أكمل أول دخول؟»، البريد الفعّال، حالة إرسال بيانات الدخول، آخر دخول. فلاتر: (لم يُرسل / نجح / فشل / غير مؤكد / بلا بريد / لم يكمل أول دخول / معطّل).
أزرار لكل صف:
1. **عرض الكلمات** (`credentials.reveal`): يعرض الكلمة الأولية الشخصية والحالية بعد فك التشفير مع زر نسخ. إن كان `credentials.require_admin_reauth=true` (افتراضي) يطلب كلمة مرور الأدمن نفسه أولاً (تبقى صالحة 5 دقائق في الجلسة). **سجّل** `credentials.reveal` (من، لمن، متى) دون القيمة.
2. **تغيير الكلمة الحالية** (`credentials.change`): حقل الكلمة الجديدة + خيار «إلزام المستخدم بتغييرها عند الدخول التالي» (افتراضي: لا). يتحقق بالسياسة. إن كان الموظف لم يُكمل أول دخول فالتغيير يحدّث الأولية الشخصية والحالية معاً. يجدد `security_stamp`.
3. **تعديل البريد (`email_override`)**، و**تجاوز الدخول** (`access_override_allow` مع سبب إلزامي).
4. **إرسال بيانات الدخول لهذا الحساب** (`credentials.send_initial`): مسموح فقط لموظف لم يُكمل أول دخول وله بريد. إن كانت حالته `Sent` يطلب تأكيداً صريحاً «تم الإرسال سابقاً، هل تعيد؟». إن كانت `Uncertain` يعرض خيارين: «تأكيد أنه وصل» (→ `Sent`) أو «اعتباره فاشلاً» (→ `Failed`).

**الإرسال الجماعي (زر في أعلى الصفحة، `credentials.send_initial`):** يعمل **بأمر يدوي فقط**. يعرض أولاً ملخصاً: عدد المؤهلين، عدد المتخطّين لكل سبب. المؤهل = `Employee` فعّال، لم يُكمل أول دخول، له بريد فعّال، وحالته `NotSent` أو `Failed`. المتخطّى: `Sent` (لا يُعاد الإرسال أبداً)، `Uncertain` (يحتاج معالجة يدوية)، بلا بريد. بعد التأكيد: أنشئ صفاً في `email_outbox` لكل مؤهل (تخطَّ من له صف `Pending`) ويعالجها `EmailOutboxWorker` بمعدل `email.throttle_ms` بين الرسائل. نتائج الإرسال تُحدّث `credentials_email_*`.
**تصنيف نتيجة الإرسال (في `MailKitEmailSender`):** استثناء قبل/أثناء الاتصال أو المصادقة (لم يبدأ الإرسال) → **إعادة محاولة** حتى `email.max_attempts` ثم `Failed`. `SmtpCommandException` برمز 5xx أو رفض صريح من الخادم → `Failed` + نص الخطأ. قبول الخادم للرسالة → `Sent`. أي `IOException`/`TimeoutException`/انقطاع **بعد بدء `SendAsync`** (لا يُعرف هل قُبلت) → `Uncertain`. **ممنوع** افتراض النجاح أو الفشل دون دليل.
**الكلمة المشتركة (`credentials.set_shared_initial`):** حقل يضبطه الأدمن (يجتاز السياسة)، تُخزَّن في `system_settings['auth.shared_initial_password']` مشفّرة (`is_secret=true`). عند تغييرها تتوقف السابقة فوراً. تُقبل فقط لحسابات `Employee` الفعّالة التي لم تُكمل أول دخول. لا تقبلها حسابات المدرب الخارجي. يسجَّل كل استخدام لها (`auth.login_shared_initial`).

**إنشاء مستخدم (`user.create`):** للأدمن فقط ولأنواع: مدرب خارجي (عبر تدفق الطلب في `05`) أو حساب `Local`. حسابات الموظفين تُنشأ بالاستيراد حصراً.
**تعديل مستخدم (`user.edit`):** الاسم الإنجليزي، البريد (override)، اللغة، `access_override_allow`. لا يعدّل بيانات المصدر (الاسم العربي، الكلية، القسم، الحالة الوظيفية).
**تعطيل/استعادة (`user.deactivate`/`user.restore`):** تحذير واضح + سبب إلزامي + تجديد `security_stamp` وإبطال الجلسات. يُمنع تعطيل آخر أدمن فعّال.

### حارس آخر أدمن (يُفرض في الخدمة)
ارفض أي عملية تترك النظام بلا مستخدم فعّال يملك دور Admin: (تعطيل، سحب الدور، `Deny` على `page.admin.users` أو `user.permissions.override` أو `user.roles.assign`). ويرفض الاستثناء `Deny` على هذه المفاتيح الثلاثة للمستخدم نفسه دائماً.

## 9. سياسة كلمات المرور (`password_policy` + `blocked_passwords`)
تسري **عند إنشاء كلمة أو تغييرها** فقط ولا تُجبر أحداً على تغيير كلمته الحالية. الفحوص (`PasswordPolicyRules.Validate`) بهذا الترتيب مع رموز الأخطاء: طول أدنى `PWD_TOO_SHORT`، أقصى `PWD_TOO_LONG`، المسافات إن مُنعت `PWD_NO_SPACES`، الأحرف العربية إن مُنعت `PWD_NO_ARABIC`، التشكيلة إن مفروضة (`PWD_NEED_UPPER|LOWER|DIGIT|SYMBOL`)، الشائع (مطبَّع صغير) `PWD_BLOCKED`، السياق (يحوي اسم المستخدم أو رقم الدخول أو الأولية الشخصية أو المشتركة) `PWD_CONTAINS_CONTEXT`. الأدمن يعدّل كل القيم من `/admin/password-policy` ويضيف/يحذف (حذفاً منطقياً غير مطلوب هنا؛ حذف صف قائمة) كلمات الحظر. الأطوال تُعرض في تلميح الحقل وفي رسائل الخطأ من القيم الحالية.
`rotation_days` إن لم يكن NULL: عند الدخول إن مرّت المدة منذ `password_changed_at` اطلب تغييراً (جلسة محدودة).

## 10. نموذج الصلاحيات
### 10.1 التقييم (`IPermissionService`)
```
effective(user, key):
  p = permission(key); إن لم توجد أو !is_active → false
  o = user_permission_overrides(user, p)
  if o.mode = Allow → true ; if o.mode = Deny → false
  return exists role r in activeRoles(user) with (r,p) in role_permissions
```
`activeRoles` = أدوار `user_roles` الفعّالة + Instructor المشتق + ExternalInstructor. الأدمن يملك كل المفاتيح غير الموسومة † (انظر §11) بفضل صفوف `role_permissions` المزروعة، **وليس** بتجاوز في الشيفرة (حتى يمكن للأدمن نفسه تعديلها من الواجهة).
### 10.2 النطاق (`scope`) — يُتحقق مع المفتاح في **الخدمة** عبر `ScopeGuard`
| النطاق | الشرط |
|---|---|
| `Global` | لا شرط |
| `OwnFaculty` | مركز المورد ∈ `DeanScopeCenterIds` للمستخدم (من `user_roles` ذات `scope_center_id`)؛ وإن لم يكن للمستخدم نطاق عميد فمركز موظفه |
| `InstructedCourse` | `course.instructor_user_id = المستخدم` أو (`course.managed_by_quality` والمستخدم يملك دور Quality) |
| `Self` | المورد يخص المستخدم نفسه |
**الأدمن يتجاوز شرط النطاق** (يمر دائماً) بشرط امتلاكه المفتاح. وقد يمنحه `Deny` خاص فلا يمر.
### 10.3 التطبيق في الطبقات
- **صفحة Razor:** `[RequirePermission("page.admin.users")]` على `PageModel` (سياسة ديناميكية من المفتاح). منع → `/access-denied` (403).
- **Controller:** `[RequirePermission("user.deactivate")]` ثم الخدمة تتحقق من النطاق.
- **الواجهة:** `<zu-if-permission key="…">` يُخفي العناصر؛ والقوائم الجانبية تُبنى بالمفاتيح.
- كل تغيير في الأدوار/الصلاحيات/الاستثناءات يُسجَّل بالقيمة قبل وبعد.

### 10.4 شاشة تعديل مستخدم عند الأدمن (`/admin/users/{id}`)
1. بيانات الحساب والأدوار (إضافة/سحب دور؛ Dean يتطلب اختيار الكلية (مركز `is_faculty=true`)، ويعرض اقتراحاً إن كان `centers.head_employee_code` يطابق موظف هذا المستخدم — **اقتراح فقط**).
2. **جدول كل الصلاحيات** مجمّعة حسب الصفحة/المجال، لكل صف: اسم الصلاحية، نوعها (عرض/إجراء)، النطاق، «تأتي من: <أدوار>» (شارات)، النتيجة من الدور، **مبدّل ثلاثي:** «اتباع الدور / سماح / منع»، **النتيجة الفعلية** (تتحدث فوراً عند التغيير قبل الحفظ). بحث وتصفية داخل الجدول. 
3. زر «معاينة التغييرات» يعرض قائمة (صلاحية، من → إلى، النتيجة الفعلية) ثم «حفظ». استثناءات المستخدم مستقلة عن تعريف الدور فلا تُمسح عند تغيير الدور.

---

## 11. كتالوج الصلاحيات الكامل (بذرة `permissions` و`role_permissions`)
الرموز: **A** مسؤول النظام، **Q** الجودة، **D** العميد، **I** المحاضر (داخلي أو خارجي)، **M** العضو. † = **مستثناة من الأدمن** (لا تُمنح لدور Admin). النوع: `V` عرض صفحة، `X` إجراء. النطاق: G عام، F كليته، C دورته كمحاضر، S ذاته.

### أ) صفحات (V)
| المفتاح | النطاق | A | Q | D | I | M |
|---|---|---|---|---|---|---|
| `page.admin.dashboard` | G | ✓ | | | | |
| `page.admin.users` | G | ✓ | | | | |
| `page.admin.credentials` | G | ✓ | | | | |
| `page.admin.courses` | G | ✓ | | | | |
| `page.admin.settings` | G | ✓ | | | | |
| `page.admin.password_policy` | G | ✓ | | | | |
| `page.admin.hints` | G | ✓ | | | | |
| `page.admin.roles` | G | ✓ | | | | |
| `page.admin.centers` | G | ✓ | | | | |
| `page.admin.sync` | G | ✓ | | | | |
| `page.admin.cert_template` | G | ✓ | | | | |
| `page.admin.audit` | G | ✓ | | | | |
| `page.admin.live_feed` | G | ✓ | | | | |
| `page.admin.stats` | G | ✓ | | | | |
| `page.admin.needs` | G | ✓ | | | | |
| `page.vault` | G | ✓ | ✓ | | | |
| `page.quality.dashboard` | G | ✓ | ✓ | | | |
| `page.quality.proposals` | G | ✓ | ✓ | | | |
| `page.quality.cancellations` | G | ✓ | ✓ | | | |
| `page.quality.courses` | G | ✓ | ✓ | | | |
| `page.quality.tracks` | G | ✓ | ✓ | | | |
| `page.quality.external_instructors` | G | ✓ | ✓ | | | |
| `page.quality.needs` | G | ✓ | ✓ | | | |
| `page.quality.evaluations` | G | ✓ | ✓ | | | |
| `page.quality.eval_form` | G | ✓ | ✓ | | | |
| `page.quality.certificates` | G | ✓ | ✓ | | | |
| `page.dean.dashboard` | F | | | ✓ | | |
| `page.dean.courses` | F | | | ✓ | | |
| `page.dean.propose` | F | † | | ✓ | | |
| `page.dean.members` | F | | | ✓ | | |
| `page.dean.needs` | F | † | | ✓ | | |
| `page.dean.stats` | F | | | ✓ | | |
| `page.instructor.courses` | C | ✓ | ✓ | | ✓ | |
| `page.instructor.course` (مساحة الدورة: ملخص) | C | ✓ | ✓ | | ✓ | |
| `page.instructor.lectures` | C | ✓ | ✓ | | ✓ | |
| `page.instructor.attendance` | C | ✓ | ✓ | | ✓ | |
| `page.instructor.participants` | C | ✓ | ✓ | | ✓ | |
| `page.instructor.exam` | C | ✓ | ✓ | | ✓ | |
| `page.instructor.impact` (صفحة `/impact/review`) | G | ✓ | ✓ | ✓ | | |
| `page.instructor.certificates` | C | ✓ | ✓ | | ✓ | |
| `page.instructor.evaluation` | C | ✓ | ✓ | | ✓ | |
| `page.member.dashboard` | S | ✓ | ✓ | ✓ | | ✓ |
| `page.member.available` | S | ✓ | ✓ | ✓ | | ✓ |
| `page.member.enrolled` | S | ✓ | ✓ | ✓ | | ✓ |
| `page.member.certificates` | S | ✓ | ✓ | ✓ | | ✓ |
| `page.member.impact` | S | ✓ | ✓ | ✓ | | ✓ |
| `page.member.teaching` (دوراتي كمحاضر) | S | ✓ | ✓ | ✓ | ✓ | ✓ |
| `page.member.pro_certs` | S | ✓ | ✓ | ✓ | | ✓ |
| `page.quality.pro_certs` | G | ✓ | ✓ | | | |
| `page.quality.pro_cert_stats` | G | ✓ | ✓ | | | |
| `page.shared.directory` | G | ✓ | ✓ | ✓ | ✓ | ✓ |
| `page.shared.profile` | G | ✓ | ✓ | ✓ | ✓ | ✓ |

> ملاحظة: الأدمن والجودة والعميد أعضاء هيئة تدريس أيضاً (لديهم M) فيرون صفحات `page.member.*` عبر المبدّل. المدرب الخارجي (`ExternalInstructor`) يحصل على كل ما في عمود I ما عدا `page.member.*` و`page.shared.*`.

### ب) إجراءات (X)
| المفتاح | النطاق | A | Q | D | I | M |
|---|---|---|---|---|---|---|
| `user.create` | G | ✓ | | | | |
| `user.edit` | G | ✓ | | | | |
| `user.deactivate` | G | ✓ | | | | |
| `user.restore` | G | ✓ | | | | |
| `user.roles.assign` | G | ✓ | | | | |
| `user.permissions.override` | G | ✓ | | | | |
| `credentials.reveal` | G | ✓ | | | | |
| `credentials.change` | G | ✓ | | | | |
| `credentials.send_initial` | G | ✓ | | | | |
| `credentials.set_shared_initial` | G | ✓ | | | | |
| `settings.edit` | G | ✓ | | | | |
| `password_policy.edit` | G | ✓ | | | | |
| `hints.manage` | G | ✓ | | | | |
| `roles.edit_permissions` | G | ✓ | | | | |
| `centers.edit` | G | ✓ | | | | |
| `sync.run` | G | ✓ | | | | |
| `sync.review.resolve` | G | ✓ | | | | |
| `cert_template.upload` | G | ✓ | | | | |
| `certificates.revoke_manual` | G | ✓ | | | | |
| `certificates.issue_pending` | G | ✓ | ✓ | | | |
| `audit.export` | G | ✓ | | | | |
| `vault.upload` | G | ✓ | ✓ | | | |
| `vault.download` | G | ✓ | ✓ | | | |
| `vault.deactivate` | G | ✓ | | | | |
| `tracks.manage` | G | ✓ | ✓ | | | |
| `course.create_direct` | G | ✓ | ✓ | | | |
| `course.create_historical` | G | ✓ | ✓ | | | |
| `course.edit` | G | ✓ | ✓ | | | |
| `course.deactivate` | G | ✓ | | | | |
| `course.restore` | G | ✓ | | | | |
| `course.archive` | G | ✓ | | | | |
| `course.unarchive` | G | ✓ | | | | |
| `course.reopen` | G | ✓ | | | | |
| `course.force_status` | G | ✓ | | | | |
| `course.cancel_direct` | G | | ✓ | | | |
| `course.propose` | F | † | | ✓ | | |
| `proposal.resubmit` | F | † | | ✓ | | |
| `proposal.decide` | G | ✓ | ✓ | | | |
| `cancellation.request` | F | † | | ✓ | | |
| `cancellation.decide` | G | ✓ | ✓ | | | |
| `need.submit` | F | † | | ✓ | | |
| `need.resubmit` | F | † | | ✓ | | |
| `need.review` | G | ✓ | ✓ | | | |
| `extinstr.select_existing` | G | ✓ | ✓ | | | |
| `extinstr.request_account` | G | ✓ | ✓ | | | |
| `extinstr.create_account` | G | ✓ | | | | |
| `participants.view.all` | G | ✓ | ✓ | | | |
| `participants.view.faculty` | F | | | ✓ | | |
| `participants.add` | G | ✓ | ✓ | | | |
| `participants.remove` | G | ✓ | ✓ | | | |
| `participants.self_register` | S | ✓ | ✓ | ✓ | | ✓ |
| `lecture.manage` (إضافة/تعديل/تعطيل) | C | ✓ | ✓ | | ✓ | |
| `material.manage` | C | ✓ | ✓ | | ✓ | |
| `course.finish` | C | ✓ | ✓ | | ✓ | |
| `attendance.activate_code` | C | ✓ | ✓ | | ✓ | |
| `attendance.edit_manual` | C | ✓ | ✓ | | ✓ | |
| `attendance.enter_code` | S | ✓ | ✓ | ✓ | | ✓ |
| `exam.manage` (إنشاء/رصد النتائج) | C | ✓ | ✓ | | ✓ | |
| `exam.requirement.edit` | G | ✓ | ✓ | | | |
| `exam.results.disable` | G | ✓ | | | | |
| `impact.submit` | S | ✓ | ✓ | ✓ | | ✓ |
| `impact.review` | G | ✓ | ✓ | ✓ | | |
| `evaluation.submit` | S | ✓ | ✓ | ✓ | | ✓ |
| `evalform.edit` (مسودة/نشر) | G | ✓ | ✓ | | | |
| `evaluation.extend_period` | G | ✓ | ✓ | | | |
| `evaluation.results.all` | G | ✓ | ✓ | | | |
| `evaluation.results.course` | C | ✓ | ✓ | | ✓ | |
| `certificates.view.all` | G | ✓ | ✓ | | | |
| `certificates.view.faculty` | F | | | ✓ | | |
| `certificates.view.own` | S | ✓ | ✓ | ✓ | | ✓ |
| `certificates.download.own` | S | ✓ | ✓ | ✓ | | ✓ |
| `stats.university` | G | ✓ | ✓ | | | |
| `stats.faculty` | F | ✓ | ✓ | ✓ | | |
| `stats.course` | C | ✓ | ✓ | | ✓ | |
| `stats.member` | G | ✓ | ✓ | | | |
| `procert.submit` | S | ✓ | ✓ | ✓ | | ✓ |
| `procert.withdraw` | S | ✓ | ✓ | ✓ | | ✓ |
| `procert.download.own` | S | ✓ | ✓ | ✓ | | ✓ |
| `procert.review` | G | ✓ | ✓ | | | |
| `procert.edit` | G | ✓ | ✓ | | | |
| `procert.download.any` | G | ✓ | ✓ | | | |
| `procert.deactivate` | G | ✓ | | | | |
| `procert.categories.manage` | G | ✓ | ✓ | | | |
| `procert.stats` | G | ✓ | ✓ | | | |
| `profile.edit.own` | S | ✓ | ✓ | ✓ | | ✓ |
| `faculty.members.view` | F | | | ✓ | | |

**قواعد البذرة:** (1) صفوف `role_permissions` لدور Admin = كل المفاتيح غير الموسومة †. (2) صف الدور Instructor = عمود I، وصف دور ExternalInstructor = عمود I ما عدا `page.member.*` و`page.shared.*`. (3) المفاتيح الموسومة † لا تُعطى للأدمن؛ يستطيع الأدمن تغيير ذلك لنفسه عبر الواجهة لكن **الافتراضي** أنها غير ممنوحة. (4) `Q` يملك `*.instructor.*` و`lecture.manage` وغيرها بنطاق C لكن النطاق لا يمر إلا في الدورات `managed_by_quality`. (5) لا يوجد إجراء `course.propose` ولا `need.submit` لغير D.
**لا تضف مفتاحاً جديداً** إلا بتعليمة. أضف إلى كل مفتاح اسماً عربياً وإنجليزياً (`name_ar`/`name_en`) من ملف الموارد.

---

## 12. كتالوج الإعدادات الكامل (بذرة `system_settings`) — كلها تُعدَّل من `/admin/settings`
| المفتاح | النوع | الافتراضي | الحد | الوصف |
|---|---|---|---|---|
| `auth.max_failed_logins` | Int | 5 | 1–50 | محاولات الدخول الخاطئة قبل القفل |
| `auth.lockout_minutes` | Int | 15 | 1–1440 | مدة القفل |
| `auth.login_max_per_ip_minute` | Int | 20 | 1–1000 | حد الدخول للـ IP في الدقيقة |
| `auth.access_token_minutes` | Int | 15 | 5–120 | عمر توكن الوصول |
| `auth.session_hours` | Int | 12 | 1–72 | عمر الجلسة بلا «تذكرني» |
| `auth.remember_me_days` | Int | 30 | 1–365 | عمر «تذكرني» |
| `auth.external_login_min` / `_max` | Int | 8000 / 9999 | 1000–9999 | نطاق رقم دخول المدرب الخارجي |
| `auth.forgot_max_per_account_hour` | Int | 3 | 1–50 | حد «نسيت كلمة المرور» لكل حساب/ساعة |
| `auth.forgot_max_per_ip_hour` | Int | 10 | 1–500 | لكل IP/ساعة |
| `auth.forgot_block_shared_email` | Bool | true | | منع الإرسال لبريد مشترك بين حسابات |
| `auth.shared_initial_password` | String (سري) | فارغ | | الكلمة الأولية المشتركة (تُضبط من صفحة الدليل) |
| `credentials.require_admin_reauth` | Bool | true | | طلب كلمة الأدمن قبل عرض الكلمات |
| `access.eligible_emp_status_codes` | String | `13` | | رموز `emp_status` المؤهلة للدخول (مفصولة بفواصل) |
| `directory.academic_main_job_codes` | String | `1` | | رموز `main_job_code` الأكاديمية |
| `directory.include_non_academic` | Bool | false | | إظهار غير الأكاديميين في دليل الأعضاء |
| `org.faculty_unit_codes` | String | `8` | | `unit_code` الذي يُفترض أنه كلية عند أول ظهور مركز |
| `attendance.threshold_percent` | Int | 75 | 1–100 | نسبة الحضور الدنيا |
| `attendance.code_length` | Int | 4 | 4–8 | عدد خانات كود الحضور للمحاضرات الجديدة |
| `attendance.code_validity_minutes` | Int | 5 | 1–120 | صلاحية الكود بعد التفعيل |
| `attendance.max_wrong_attempts` | Int | 5 | 1–100 | محاولات خاطئة قبل قفل المتدرب |
| `attendance.lock_minutes` | Int | 10 | 1–1440 | مدة قفل إدخال الكود |
| `attendance.min_lectures_to_finish` | Int | 1 | 1–100 | أقل عدد محاضرات لإنهاء الدورة |
| `attendance.allow_edit_after_finish` | Bool | true | | تصحيح الحضور بعد إنهاء الدورة وبعد الشهادة |
| `files.max_mb.impact` | Int | 10 | 1–500 | حجم ملف الأثر |
| `files.max_mb.letters` | Int | 10 | 1–500 | الكتب الرسمية والسير الذاتية |
| `files.max_mb.materials` | Int | 50 | 1–1000 | مواد المحاضرات |
| `files.max_mb.cert_template` | Int | 10 | 1–100 | قالب الشهادة |
| `files.max_mb.photo` | Int | 2 | 1–20 | صورة الملف الشخصي |
| `files.ext.impact` | String | `pdf,docx` | | الامتدادات المسموحة للأثر |
| `files.ext.letters` | String | `pdf` | | الكتب الرسمية والسير الذاتية (السيرة: `pdf,docx` في `files.ext.cv`) |
| `files.ext.cv` | String | `pdf,docx` | | |
| `files.ext.materials` | String | `pdf,pptx,ppt,docx,xlsx,zip` | | |
| `files.ext.photo` | String | `jpg,jpeg,png` | | |
| `files.max_mb.pro_cert` | Int | 10 | 1–500 | حجم ملف الشهادة المهنية |
| `files.ext.pro_cert` | String | `pdf,pptx` | | امتدادات الشهادة المهنية |
| `procert.show_expired_in_profile` | Bool | true | | عرض المنتهية في الملف بشارة «منتهية» |
| `procert.stats_top_n` | Int | 10 | 3–100 | حجم قوائم «الأكثر» في الإحصائيات |
| `courses.cancel_requires_letter` | Bool | true | | إلزام كتاب رسمي للإلغاء |
| `courses.external_requires_letter_and_cv` | Bool | true | | إلزام كتاب مجلس الكلية + السيرة لخبير خارجي |
| `evaluation.default_period_days` | Int | 14 | 0–365 | اقتراح تاريخ نهاية التقييم عند الاعتماد |
| `cert.reference_pattern` | String | `ZU-{YYYY}-{CODE}-{NNNN}` | | صيغة الرقم المرجعي |
| `cert.reference_padding` | Int | 4 | 1–10 | عدد خانات التسلسل |
| `cert.general_short_code` | String | `GEN` | | رمز الدورات العامة |
| `cert.verify_base_url` | String | `https://portal.zu.edu.jo/verify` | | أساس رابط التحقق في QR |
| `cert.verify_rate_per_minute` | Int | 60 | 1–1000 | حد التحقق العام للـ IP |
| `email.from_address` | String | `lms@zu.edu.jo` | | عنوان المرسل |
| `email.from_name_ar` / `_en` | String | «منصة التطوير والتدريب المهني — جامعة الزرقاء» | | اسم المرسل |
| `email.throttle_ms` | Int | 300 | 0–10000 | تأخير بين الرسائل |
| `email.max_attempts` | Int | 3 | 1–10 | محاولات الإرسال قبل `Failed` |
| `sync.employees_url` / `sync.departments_url` / `sync.centers_url` / `sync.emails_url` | String | روابط `04` | | مصادر الاستيراد |
| `sync.timeout_seconds` | Int | 60 | 5–600 | مهلة الطلب |
| `sync.page_limit` | Int | 500 | 25–5000 | حجم صفحة ORDS |
| `ui.default_page_size` | Int | 20 | 10–100 | حجم صفحة الجداول |
| `audit.live_feed_poll_seconds` | Int | 5 | 2–60 | تحديث التدفق المباشر |
| `audit.page_size` | Int | 50 | 10–200 | حجم صفحة السجل |

**شروط الإعدادات:** كل تعديل يتحقق من النوع والحدّين في الخادم، ويُسجَّل (قديم → جديد)، ويُبطل الذاكرة المؤقتة، ويظهر أثره مباشرة. ممنوع قراءة أي إعداد من `appsettings.json`؛ الأسرار فقط من البيئة (`01 §5`). كل إعداد يظهر في صفحة الإعدادات في مجموعة (auth, attendance, files, certificates, email, sync, directory, ui) بعنوان ووصف عربي وإنجليزي وتلميح.

## 13. تحديد المعدل (`AddRateLimiter`)
سياسات: `login` (من الإعداد، مفتاحها IP)، `forgot` (مفتاحها IP + حدّ الحساب في الخدمة)، `verify` (`cert.verify_rate_per_minute`)، `attendance-code` (60/دقيقة لكل مستخدم + القفل في `attendance_code_attempts`)، `api-default` (600/دقيقة لكل مستخدم). التجاوز → `429` بالعقد العام.
