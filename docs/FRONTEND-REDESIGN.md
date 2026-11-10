# ZU-LMS — خطة إعادة تصميم الواجهات (Frontend Redesign)

> **ملف تسليم (Handoff).** إذا توقف العمل في أي لحظة، يستطيع أي مطوّر أو نموذج AI آخر أن يكمل من هنا.
> اقرأ القسم 1 (القواعد) ثم القسم 5 (سجل التقدّم) لتعرف أين وصلنا، ثم نفّذ أول بند غير مُعلَّم في القسم 4.

---

## 1. القواعد الثابتة (لا تُكسر)

1. **لا تعديل على الباك إند إطلاقاً.** ممنوع لمس أي ملف `.cs` أو `Api/` أو `Application/` أو `Domain/` أو `Infrastructure/` أو `appsettings*.json` أو `Program.cs`. العمل فقط في:
   - `src/ZuLms.Web/Pages/**/*.cshtml` و `src/ZuLms.Web/Areas/Public/Pages/*.cshtml` (العرض فقط، بدون تغيير `@page` أو `@inject` أو منطق الصلاحيات)
   - `src/ZuLms.Web/wwwroot/css/*` و `src/ZuLms.Web/wwwroot/js/*`
2. **حافظ على كل `id` و `name` و `data-*`** الموجودة في الصفحات؛ ملفات JS الحالية تعتمد عليها. أي عنصر جديد يُضاف، لا يُحذف عنصر قديم مرتبط بـ JS.
3. **نفس الـ API بالضبط.** نفس المسارات ونفس شكل البيانات (انظر القسم 3). لا تفترض حقولاً غير موجودة.
4. **Bootstrap 5.3 RTL + Tabler Icons + Chart.js 4.5.1 + خط Tajawal** — كلها محلية في `wwwroot/lib`. **لا CDN ولا مكتبات جديدة** (السيرفر مشترك وقد لا يصل للإنترنت). لا React ولا أي framework.
5. **الاتجاه:** الصفحات RTL بالعربي و LTR بالإنجليزي. استخدم خصائص CSS المنطقية (`inset-inline-start`, `margin-inline-end`, `padding-inline`) بدل left/right.
6. **الترجمة:** النصوص العربية الثابتة الجديدة تُضاف ترجمتها في `wwwroot/js/ui-translations.js` (قاموس نص عربي ← إنجليزي، يُترجم تلقائياً عند اللغة en). داخل JS استخدم `ZuLmsI18n.localize('عربي','English')`.
7. **إمكانية الوصول:** احترم `prefers-reduced-motion`، تباين ألوان كافٍ، `aria-label` للأزرار الأيقونية، التركيز المرئي `:focus-visible`.
8. **الهوية:** كحلي جامعة الزرقاء `#022C5A` (أساسي) + ذهبي `#D4A72C` (إبراز) + أبيض. لا تغيّر الشعار `wwwroot/img/logo.png`.

## 2. الهيكل الحالي (قبل التعديل)

| الطبقة | الملف | الدور |
|---|---|---|
| Layout | `Pages/Shared/_Layout.cshtml` | الشريط الجانبي + الهيدر + تحميل CSS/JS لكل الصفحات الداخلية |
| CSS shell | `wwwroot/css/prototype.css` | متغيرات الألوان + الشريط الجانبي + الهيدر |
| CSS components | `wwwroot/css/site.css` | تكييف Bootstrap (بطاقات، جداول، نماذج، Toasts…) |
| Login | `Areas/Public/Pages/Login.cshtml` + `wwwroot/css/auth.css` + `wwwroot/js/auth.js` | صفحة الدخول (و auth.css تُستخدم أيضاً في ForgotPassword / ChangePassword / Verify / AccessDenied) |
| Stats | `Pages/Shared/_StatsDashboard.cshtml` + `wwwroot/js/stats-dashboard.js` | لوحة الإحصائيات المشتركة (أدمن/جودة/عميد) — مستخدمة في `Admin/Dashboard`, `Admin/Stats`, `Quality/Dashboard`, `Dean/Dashboard`, `Dean/Stats` |
| Member | `Pages/Member/Dashboard.cshtml` + `wwwroot/js/member-dashboard.js` | لوحة العضو |
| Admin extra | `Pages/Admin/Dashboard.cshtml` + `wwwroot/js/admin-dashboard.js` | آخر الأحداث + آخر مزامنة |
| ملف غير مستخدم | `wwwroot/css/app.css` | قديم، غير محمّل في أي صفحة — اتركه |

**نسخة احتياطية من الملفات الأصلية قبل التعديل:** `docs/frontend-redesign-backup/` (نفس المسارات النسبية تحت `src/ZuLms.Web`). للتراجع عن أي ملف انسخه من هناك.

## 3. عقود الـ API المستخدمة في الواجهات المعدّلة (لا تتغير)

- `POST /api/v1/auth/login` ← body `{ loginName, password, rememberMe }` ← يرجع `{ sessionId?, redirectUrl }`
- `GET /api/v1/stats/lookups?role=Admin|Quality|Dean[&userSearch=..]` ← `{ canFilterFaculty, canFilterUser, faculties:[{id,name}], users:[{id,name}] }`
- `GET /api/v1/stats/dashboard?role=..&centerId=&userId=&fromDate=YYYY-MM-DD&toDate=YYYY-MM-DD` ←
  `{ scopeLabel, courses, participants, certificates, finishedCourses, completionRate, byStatus:[{label,value}], byFaculty:[{label,value}], byTrack:[{label,value}], monthly:[{label,courses,participants}], person?:{id,name,courses,finishedCourses,certificates} }`
- لوحة الإحصائيات (جدول أحدث الدورات): `GET /api/v1/courses/directory?view=All|Dean&page=1[&centerId=]` ← `items` مرتبة من الأحدث (`id, courseNo, titleAr/En, status, centerNameAr, instructorNameAr, participantsCount, plannedStartDate`) — نعرض أول 6
- Member: `GET /api/v1/courses/available?page=1` (`total`), `GET /api/v1/courses/directory?view=Member&page=1` (`total`), `GET /api/v1/certificates/mine` (array)
- Admin: `GET /api/v1/audit?page=1` (`items[{summaryAr, actorName, at}]`), `GET /api/v1/sync/runs` (array `{status, startedAt, finishedAt}`)

## 4. خطة التنفيذ (المراحل والخطوات)

### المرحلة 0 — التحضير ✅
- [x] قراءة الكود وتحديد ملفات الواجهة
- [x] نسخ احتياطي إلى `docs/frontend-redesign-backup/`
- [x] كتابة هذا الملف

### المرحلة 1 — نظام التصميم العام (يؤثر على كل الصفحات)
- [x] إنشاء `wwwroot/css/zu-theme.css` (طبقة أخيرة فوق prototype.css و site.css):
  - ~~شريط جانبي كحلي~~ (أُلغي بطلب المستخدم — الشريط الجانبي بشكله الأصلي)
  - هيدر زجاجي (backdrop-filter) ثابت مع ظل عند التمرير
  - بطاقات بحواف 14px وظلال ناعمة، رؤوس بطاقات بأيقونة
  - جداول: رأس ملوّن خفيف، صفوف hover، زوايا دائرية
  - نماذج وأزرار: أزرار كحلية صلبة، حالات تركيز واضحة
  - شارات الحالة `zu-status-*` بنقطة ملوّنة
- [x] إنشاء `wwwroot/js/zu-ui.js`: عدّاد أرقام متحرك `ZuUi.countUp` + ظل الهيدر عند التمرير
- **قرار تصميمي:** لا حركات دخول عامة ولا ripple. الحركة التلقائية الوحيدة هي عدّادات KPI؛ أي حركة أخرى تكون استجابة لفعل المستخدم. الذهبي المعتمد `#C9A227`.
- [x] ربطهما في `_Layout.cshtml` (CSS بعد `site.css`، JS بعد `shell.js`)

### المرحلة 2 — صفحة الدخول وصفحات الـ Auth
- [x] إعادة كتابة `wwwroot/css/auth.css`: تقسيم الشاشة — لوحة هوية كحلية متحركة (أشكال/شبكة/توهج) مع مزايا المنصة + نموذج زجاجي على اليمين/اليسار. متجاوب (على الجوال تختفي اللوحة وتبقى خلفية متدرّجة)
- [x] تعديل `Login.cshtml` (العرض فقط، نفس `id`s): حقول بأيقونات، زر إظهار كلمة المرور، تحذير Caps Lock، زر بحالة تحميل (spinner)
- [x] تعديل `auth.js` بإضافة: إظهار/إخفاء كلمة المرور، Caps Lock، حالة التحميل، اهتزاز البطاقة عند الخطأ — **بدون تغيير منطق الإرسال**
- [x] التأكد أن ForgotPassword / ChangePassword / Verify / AccessDenied تظهر بشكل جيد بالـ CSS الجديد (يجب أن تبقى `.auth-shell` و`.auth-box` تعمل وحدها بدون لوحة الهوية)

### المرحلة 3 — لوحة الإحصائيات (الأهم)
> ⚠️ هذا وصف الإصدار الأول. **الإصدار الحالي هو v2** (بدون Hero وبدون شريط الرؤى وبدون sparkline/الحلقة، مع جدول أحدث الدورات) — انظر سطر "لوحة الإحصائيات v2" في القسم 5.
- [x] إعادة كتابة `_StatsDashboard.cshtml`:
  - Hero علوي بتدرّج كحلي: العنوان + نطاق البيانات + الوقت + أزرار (تحديث، طباعة)
  - شريط فلاتر قابل للطي + أزرار نطاق سريع (هذا الشهر / 3 أشهر / هذه السنة / الكل)
  - 4 بطاقات KPI: أيقونة متدرّجة، رقم متحرك، sparkline من بيانات monthly، مؤشر اتجاه (آخر شهر مقابل السابق)، والبطاقة الرابعة حلقة تقدّم دائرية لنسبة الإنجاز
  - شريط "رؤى ذكية" محسوبة من البيانات: أعلى شهر، متوسط المشاركين لكل دورة، أكثر كلية نشاطاً، أكثر محور
  - رسم التطور الشهري مع أزرار تبديل (خط/أعمدة) وإخفاء/إظهار السلاسل
  - رسم الحالات (doughnut) مع رقم الإجمالي في المنتصف وأسطورة تفاعلية بالأرقام والنسب
  - الكليات: أعمدة أفقية + **قائمة ترتيب** بأشرطة تقدّم؛ النقر على كلية يطبّق فلتر الكلية (إن كان متاحاً)
  - المحاور: تبديل بين أعمدة/دائري
  - كل بطاقة رسم: زر تكبير (ملء الشاشة) وزر تنزيل PNG
  - Skeleton أثناء التحميل
  - **نفس الـ ids القديمة** (`stats-center`, `stats-user-search`, `stats-user`, `stats-from`, `stats-to`, `stats-reset`, `stats-filters`, `kpi-*`, `chart-*`, `person-*`, `stats-scope`, `dashboard-error`)
- [x] إعادة كتابة `stats-dashboard.js` بنفس الاستدعاءات وإضافة ما سبق

### المرحلة 4 — لوحات أخرى
- [x] `Member/Dashboard.cshtml` + `member-dashboard.js`: ترحيب باسم المستخدم، 3 بطاقات KPI متحركة، بطاقات وصول سريع (الدورات المتاحة، دوراتي، شهاداتي، إدخال كود الحضور، كلمة المرور)
- [x] `Admin/Dashboard.cshtml` + `admin-dashboard.js`: آخر الأحداث كـ Timeline بأيقونات وألوان، بطاقة آخر مزامنة بحالة ملوّنة

### المرحلة 5 — التحقق
- [x] فحص JS (`node --check`) لكل ملف معدّل
- [x] صفحة اختبار ثابتة ببيانات وهمية لعرض الدخول والإحصائيات (عربي/إنجليزي، سطح مكتب/جوال) وأخذ لقطات
- [ ] تشغيل التطبيق فعلياً (`dotnet run` من `src/ZuLms.Web`) ومراجعة: login، admin/dashboard، quality/dashboard، dean/dashboard، member/dashboard، admin/stats، وصفحة جدول عشوائية (admin/courses)

### المرحلة 6 — (اختياري لاحقاً) تحسين صفحات إضافية
- صفحات الجداول (الدورات، الدليل، الصلاحيات) تستفيد تلقائياً من zu-theme.css. أي تحسين إضافي يكون صفحة صفحة وبنفس القواعد.
- `Quality/ProCertStats.cshtml` + `quality-pro-cert-stats.js` يمكن أن تتبع نفس أسلوب لوحة الإحصائيات.

## 5. سجل التقدّم (حدّثه بعد كل خطوة)

| التاريخ | الخطوة | الحالة | ملاحظات |
|---|---|---|---|
| 2026-11-10 | المرحلة 0 | ✅ مكتملة | نسخ احتياطي + الخطة |
| 2026-11-10 | المرحلة 1 | ✅ مكتملة | `zu-theme.css` + `zu-ui.js` + سطرين في `_Layout.cshtml` (CSS بعد styles.css، JS بعد shell.js). الهوية: كحلي + ذهبي `#C9A227` + نقش النجمة الثمانية (khatam) كمتغير CSS `--zu-khatam` |
| 2026-11-10 | المرحلة 2 | ✅ مكتملة | Login مقسوم: لوحة هوية + نموذج. `auth.css` يدعم `.auth-split` والصفحات القديمة المستقلة. `auth.js`: نفس منطق الإرسال + عين كلمة المرور + Caps Lock + spinner + اهتزاز عند الخطأ. **ملاحظة:** ملفات المشروع بنهايات LF — حافظ عليها |
| 2026-11-10 | المرحلة 3 | ✅ مكتملة | لوحة الإحصائيات: Hero بنقش + نطاقات زمنية سريعة، فلاتر قابلة للطي مع عدّاد الفلاتر النشطة، 4 بطاقات KPI (عدّاد متحرك + sparkline + اتجاه آخر شهر + حلقة نسبة الإنجاز)، شريط رؤى، رسم شهري (تبديل الدورات/المشاركات + خط/أعمدة)، دائري للحالات مع أسطورة تفاعلية، ترتيب الكليات (النقر يطبّق فلتر الكلية)، المحاور (أعمدة/دائري مع دمج "أخرى")، وفي كل لوحة: جدول/تنزيل PNG/ملء الشاشة. تلميحات HTML مخصصة. لوحة الألوان الفئوية تم التحقق منها (CVD) |
| 2026-11-10 | المرحلة 4 | ✅ مكتملة | لوحة العضو (ترحيب + KPI + وصول سريع محمي بـ `<zu-if-permission>`)، الأدمن (Timeline للأحداث + بطاقة حالة المزامنة). ترجمات إنجليزية للنصوص الجديدة أضيفت أعلى `ui-translations.js` |
| 2026-11-10 | المرحلة 5 | 🟡 جزئية | تم: `node --check` لكل JS، اختبار بصري ببيانات وهمية (عربي/إنجليزي، 1440px و390px) واختبار تفاعل آلي بلا أخطاء. **لم يتم:** تشغيل `dotnet build`/`dotnet run` الفعلي (لا يوجد .NET في بيئة العمل) |
| 2026-11-10 | لوحة الإحصائيات v2 | ✅ مكتملة | بطلب المستخدم وعلى نمط صورة مرجعية (Helpdesk dashboard) مع الحفاظ على ألواننا: أُلغي الـ Hero الكحلي وشريط الرؤى؛ صار: عنوان فاتح + قائمة الفترة (select `#zs-range`) + زر تصفية يفتح `<details>` + تحديث/طباعة. بطاقات KPI: رقم كبير + أيقونة كبيرة + سطر "مقارنة بالشهر السابق" (والبطاقة الرابعة شريط تقدّم). صف: دائري الحالات بأسطورة جانبية بالنسب + أعمدة شهرية بأرقام فوق الأعمدة والعمود الأعلى مميّز. صف: ترتيب الكليات + المحاور (أعمدة بأرقام). صف أخير: جدول **أحدث الدورات** من directory API. بطاقات لوحة العضو حُوّلت لنفس البنية |
| 2026-11-10 | تعديلات بطلب المستخدم | ✅ مكتملة | (1) **الشريط الجانبي رجع لشكله الأصلي** (أبيض + كحلي من `prototype.css`) — حُذف قسم Sidebar من `zu-theme.css`، لا تُعِد تلوينه. (2) **Login:** حُذف النص والمزايا ونجمة الخلفية من لوحة الهوية. الصورة الفوتوغرافية كانت مغبشة (المصدر واتساب 1600px)، فاستُبدلت بـ **رسمة توضيحية** مولّدة من صورة الحرم (العلم) على نمط الرسومات الخطية: خطوط حبر كحلية + 4 درجات رمادي-كحلي مسطحة + السماء ورقية + **العلمان فقط بألوانهما**، وحواف تذوب في لون الخلفية `#eef1f5`. الملفات: `wwwroot/img/login-campus.jpg` (2400×1600) و`login-campus-sm.jpg` (1200). سكربت التوليد: `tools/make-login-illustration.py` (Python + OpenCV؛ المصدر `WhatsApp Image 2026-10-10 at 2.01.48 PM.jpeg` في جذر المشروع). **v3 (الحالي):** الرسمة أوضح — CLAHE للتباين المحلي، تنعيم أخف، 5 درجات بدل 4، خطوط XDoG+Canny أدق، واكتشاف السماء بالمناطق المتصلة بأعلى الصورة بدون عبور حواف قوية (حتى لا تُمسح المباني البيضاء/البوابة). **بدون حز:** الرسمة على أبيض والصفحة كلها بيضاء، والرسمة تتلاشى تدريجياً نحو جهة النموذج عبر `mask-image` على `.auth-photo::before` (78%→شفاف)، ولوحة النموذج بلا حواف/ظل. على الجوال: الرسمة شريط علوي يتلاشى للأسفل داخل النموذج، الاسم مخفي والشعار داخل النموذج |
| 2026-11-10 | رسمة الدخول النهائية | ✅ مكتملة | **الرسمة الحالية هي `Done.png`** (رسمة حبر يدوية الطابع للحرم مع العلمين بالألوان، أضافها المستخدم في جذر المشروع، 1536×1024). عولجت: خلفيتها الرمادية (~234) حُوّلت لأبيض نقي (levels، نقطة البياض 228) لتندمج مع الصفحة، ثم كُبّرت بـ Lanczos + UnsharpMask خفيف → `wwwroot/img/login-campus.jpg` (2304×1536) و`login-campus-sm.jpg` (1200×800). نفس أسماء الملفات فلم يتغير أي CSS/HTML. سكربت `tools/make-login-illustration.py` صار مرجعاً قديماً فقط (لم يعد مستخدماً). لتبديل الرسمة مستقبلاً: نفس المعالجة ونفس الأسماء |

## 6. الخطوات القادمة حتى النهاية

1. **(ضروري)** من `src/ZuLms.Web`: `dotnet build` ثم `dotnet run`، وافتح: `/login`، `/admin/dashboard`، `/admin/stats`، `/quality/dashboard`، `/dean/dashboard`، `/member/dashboard`، وصفحة جدول مثل `/admin/courses`. جرّب اللغتين.
2. إن ظهر خطأ Razor في `Login.cshtml` فسببه المحتمل الدالة المحلية `L(...)` — بديلها: `@(ar ? "..." : "...")`.
3. تأكد أن نصوص `scopeLabel` وأسماء الحالات القادمة من الـ API تظهر مترجمة بالإنجليزي (تعتمد على `ui-translations.js`).
4. (اختياري) إضافة بحث عام و"إجراءات سريعة" في الهيدر (`_Layout.cshtml`) كما في الصورة المرجعية.
5. (اختياري) تطبيق نفس أسلوب الإحصائيات على `Quality/ProCertStats.cshtml` + `quality-pro-cert-stats.js`.
6. (اختياري) مراجعة صفحات الجداول الكبيرة بصرياً بعد الثيم الجديد.

**ملاحظة:** أثناء جلسة التصميم ظهر تعديل على `src/ZuLms.Application/Features/Courses/CourseForceStatusService.cs` (08:54 بتوقيت عمّان) — ليس من عمل إعادة التصميم.

## 7. كيف تكمل إذا توقف العمل

1. اقرأ القسم 5 وحدّد آخر خطوة مكتملة.
2. افتح الملف المعني وقارنه بالنسخة الاحتياطية لتعرف ما تم فعلاً (`diff docs/frontend-redesign-backup/<path> src/ZuLms.Web/<path>`).
3. أكمل البند التالي في القسم 4 بنفس الأسلوب (الألوان والمتغيرات معرّفة أعلى `zu-theme.css`).
4. بعد كل ملف: `node --check` لملفات JS، ثم حدّث القسم 5.
