# العيوب والفجوات

## UIINV-001 — مسارات مطلوبة غير موجودة ساكناً — RESOLVED

- **التصنيف السابق:** `MISSING`
- **الحالة:** `RESOLVED` في 2 نوفمبر 2026؛ `route-matrix.csv` الآن بلا `MISSING-ROUTE`.
- **المصدر:** الحزمة `08 §2` وخطة الاختبار `§4.2`.
- **الدليل:** `route-matrix.csv`، الصفوف ذات `MISSING-ROUTE`.
- **العدد:** 17 مساراً.

المسارات:

`/admin/users/{id}`، `/admin/courses/{id}`، `/admin/roles`، `/admin/stats`،
`/quality/courses/new-historical`، `/quality/courses/{id}`،
`/quality/external-instructors`، `/dean/stats`، `/instructor/courses/{id}`،
`/instructor/courses/{id}/attendance`، `/instructor/courses/{id}/exam`،
`/instructor/courses/{id}/impact`، `/instructor/courses/{id}/evaluation`،
`/member/available`، `/member/enrolled`، `/member/courses/{id}`،
`/account/settings`.

أضيفت المسارات المدمجة كـ aliases لنفس الوظائف المحمية، وأضيفت صفحات فعلية للإحصاءات والأدوار والدورات التاريخية والمدربين الخارجيين وإعدادات الحساب.

## ENV-001 — بيئة UI المعزولة غير متوفرة — PARTIALLY RESOLVED

- **التصنيف:** `BLOCKED`
- **الحالة:** قاعدة وتخزين الجولة وSMTP loopback sink أصبحت معزولة؛ Docker/PostgreSQL 17 container gate ما زال محجوباً.
- **الدليل:** `environment.md` و`commands/03-dotnet-test-release.txt`.

بدأ تنفيذ الواجهة الفعلية؛ تُسجل النتائج الجديدة في `cases.csv` و`traces/`.

## AUTH-SEC-001 — نموذج الدخول قد يرسل كلمة المرور في URL قبل تحميل JavaScript — RESOLVED

- **التصنيف:** عيب أمني وظيفي.
- **الاكتشاف:** إرسال النموذج قبل اكتمال ربط `auth.js` استخدم طريقة HTML الافتراضية `GET` لأن `<form>` لم يحدد `method`.
- **الأثر:** يمكن أن تظهر بيانات الدخول في query string وسجل المتصفح وسجلات الوسطاء عند الإرسال المبكر أو تعطل JavaScript.
- **الإصلاح:** إضافة `method="post"` إلى نموذج `Areas/Public/Pages/Login.cshtml` حتى يفشل fallback بأمان دون وضع الحقول في URL.
- **إعادة التحقق:** نموذج Release يعرض `method=post`، وإرسال fallback أصدر `POST /login` دون كلمة مرور في URL.
- **الدليل:** `traces/auth-hardening-retest-001.json`.

## AUTH-UI-002 — تكرار إرسال نموذج المصادقة أثناء الطلب البطيء — RESOLVED

- **التصنيف:** عيب وظيفي/تزامن.
- **إعادة الإنتاج:** تأخير Promise الخاصة بطلب المصادقة ثم إطلاق حدثي submit متتاليين؛ لوحظ استدعاء API مرتين رغم تعطيل الزر.
- **السبب:** تعطيل زر submit لا يمنع Enter أو حدث submit ثانياً.
- **الإصلاح:** إضافة حارس `submitting` داخل `wwwroot/js/auth.js` مع إعادته في `finally`.
- **إعادة التحقق:** تحت نفس التأخير وحدثي submit أصبح `apiCalls=1`، وبقي الزر معطلاً أثناء الطلب ثم عاد بعد الفشل.
- **الدليل:** `traces/auth-hardening-retest-001.json` و`traces/auth01-partial.json`.

## AUTH-BLOCK-003 — فروع مصادقة تحتاج تحكم زمن/SMTP/E1

- **الحالة:** بقيت الأحكام السابقة `BLOCKED` للحالات `AUTH08/AUTH10/AUTH11/AUTH14` فقط؛ إعادة اختبار AUTH خارج دفعة ADM.
- **AUTH08:** لا توجد أداة clock أو cookie control لانتهاء access cookie ذات `HttpOnly` حتمياً؛ العمر المضبوط 15 دقيقة.
- **AUTH10:** أُنشئ E1 من رحلة الجودة وسُجل دخول خارجيه في ADM15، لكن اختبارات منع `/account/password` و`/member/profile` لم تُعد ضمن هذه الدفعة.
- **AUTH11/AUTH14:** SMTP loopback sink أصبح متاحاً، لكن فروع استعادة كلمة المرور ورسائل AUTH لم تُعد ضمن هذه الدفعة.
- **الدليل:** `traces/auth04-auth14-final.json`.

## ADM-UI-004 — فجوات إدارة المستخدمين والأدوار — RESOLVED

- **الحالة:** `RESOLVED` وأعيد اختبار البنود من UI في 2 نوفمبر 2026.
- **ADM01:** أضيف آخر 10 أحداث وآخر مزامنة وحالاتها الفارغة وروابط KPI؛ طلبات audit/sync/stats عادت 200.
- **ADM02:** أضيفت فلاتر مستقلة للنوع والدور والكلية والتفعيل، واختبرت طلباتها منفردة.
- **ADM03:** يمنع `CreateLocalAsync` الأرقام ضمن النطاق الخارجي المحجوز ويرجع `LOGIN_RESERVED`.
- **ADM08/ADM09:** أصبحت التغييرات staged، مع preview/cancel/apply متعدد، ومصادر الأدوار ونطاق الكلية ظاهرة.
- **ADM11 (أمني):** حدث الإغلاق يمحو حقول الأولية والحالية وإعادة التحقق ويخفي لوحة العرض.
- **الدليل:** `traces/adm01-adm15-retest.json`.

## ADM-BLOCK-005 — Fixtures وSMTP المطلوبة لـADM — RESOLVED

- **الحالة:** `RESOLVED`؛ أضيفت N3/N4/N5، واستخدمت بيئة SMTP loopback sink لا ترحل البريد.
- `ADM06`: عُطلت مؤقتاً أدوار Admin للحسابات الاختبارية عبر UI؛ رفض الخادم سحب آخر Admin بـ409، ثم أعيدت الأدوار.
- `ADM12`: N3/N4/N5 أكملوا تدفق التغيير الإلزامي قبل/عند أول دخول.
- `ADM13/ADM14`: جُرّب الإرسال الفردي وإعادة الإرسال والإرسال غير المؤكد والحل والمعاينة الجماعية والتأكيد.
- `ADM15`: أنشئ E1 من اقتراح العميد وقرار الجودة وطلب إنشاء الحساب؛ اختبر رفض shared initial ودخول كلمة E1 الفردية.
- **الدليل:** `traces/adm01-adm15-retest.json` و`fixtures-manifest.json`.

## CRS-UI-006 — حقول المحاضر الخارجي غائبة من نموذج العميد — RESOLVED

- **التصنيف:** فجوة UI تمنع رحلة طلب المدرب الخارجي.
- **الاكتشاف:** `dean-proposals.js` كان يعتمد على عناصر نوع المحاضر والاسم والجهة والبريد والكتاب والسيرة، لكنها غائبة من `Pages/Dean/Propose.cshtml`.
- **الإصلاح:** أضيف اختيار داخلي/خارجي وحقول البيانات ومرفقات المجلس والسيرة مع روابط الملفات المحفوظة.
- **إعادة التحقق:** أرسل D1 مقترحاً خارجياً ومرفقين من UI، واعتمدته الجودة مع `RequestNewAccount`، ثم أكمل A1 الطلب وأنشأ E1.
- **الدليل:** `traces/adm01-adm15-retest.json`.
