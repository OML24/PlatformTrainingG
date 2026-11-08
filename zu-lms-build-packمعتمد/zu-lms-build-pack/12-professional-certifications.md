# 12 — الشهادات المهنية (شهادات الشركات العالمية) في الملف الشخصي

شهادات يحصل عليها العضو من جهات عالمية (Microsoft، Cisco، PMI، Google، AWS...)، **غير** شهادات المنصة (`06`). يرفعها العضو مباشرة **إلى الجودة**، والجودة تقبل أو ترفض، وما يُقبل **يظهر في ملفه الشخصي فوراً** لجميع مستخدمي المنصة. لكل عضو (عميد، عضو، محاضر داخلي) ملف مشترك؛ المدرب الخارجي بلا ملف ولا شهادات مهنية.

## 1. القرارات [م]
- اسم الشهادة والجهة المانحة **أسماء علم تُكتب كما وردت في الوثيقة** (أي لغة): خانة واحدة إلزامية، والعربية اختيارية (`name_ar`) — استثناء مقصود من قاعدة «العربية إلزامية». الوصف خانة واحدة اختيارية.
- لا بد من **دليل تحقق واحد على الأقل**: رمز الشهادة (`credential_code`) أو رابط التحقق (`verify_url`)، **وملف PDF/PPTX إلزامي** (`files.ext.pro_cert`، الحجم `files.max_mb.pro_cert`).
- الملف يراه ويحمّله **صاحبه والجودة والأدمن فقط**. بقية المستخدمين يرون البطاقة (الاسم، الجهة، التاريخ، الرمز، رابط التحقق) دون الملف.
- ما يظهر في الملف المشترك: `Approved` و`is_active` فقط. الـ`Submitted` والـ`Rejected` يراها صاحبها في صفحته.
- الشهادة **المنتهية** (`expiry_date < اليوم`) تبقى ظاهرة بشارة «منتهية» (إعداد `procert.show_expired_in_profile`).
- العضو **لا يعدّل** شهادة معتمدة؛ يستطيع **سحبها** (تعطيل منطقي) وإعادة رفع جديدة. الجودة والأدمن يعدّلان (تصحيح خطأ).

## 2. حقول نموذج الرفع (`/member/pro-certs`، تلميح لكل حقل)
| الحقل | العمود | قيد |
|---|---|---|
| اسم الشهادة | `name` | إلزامي ≤300 |
| الاسم بالعربية | `name_ar` | اختياري |
| الجهة المانحة | `issuer_name` | إلزامي ≤300 |
| موقع الجهة | `issuer_website_url` | اختياري، رابط `https?://` |
| التصنيف | `category_id` | إلزامي، قائمة قصيرة بلا بحث |
| الوصف | `description` | اختياري |
| رمز الشهادة (Credential ID) | `credential_code` | اختياري* |
| رابط التحقق | `verify_url` | اختياري* |
| تاريخ الإصدار | `issued_date` | إلزامي، ليس في المستقبل |
| تاريخ الانتهاء | `expiry_date` | اختياري (فارغ = لا تنتهي)، ≥ الإصدار |
| ملف الشهادة | `file_id` | إلزامي PDF/PPTX |
\* واحد منهما على الأقل (`PROCERT_NEEDS_EVIDENCE`). منع التكرار: نفس (المستخدم، الجهة، الرمز) غير مرفوض/معطّل → `PROCERT_DUPLICATE`.

## 3. دورة الحالة
`Submitted` → (جودة) `Approved` | `Rejected` (ملاحظات إلزامية). المرفوضة يعدّلها صاحبها ويعيد الإرسال (`Submitted`، حدث `Resubmitted`). كل حدث في `professional_cert_events`. عند الإرسال: إشعار الجودة. عند القرار: إشعار صاحبها (وتظهر في ملفه فور `Approved` دون أي تأخير أو تخزين مؤقت).
الملف يُسجَّل في المستودع المركزي (`doc_type=ProfessionalCertificate`).

## 4. الصفحات
- **العضو `/member/pro-certs`** (`page.member.pro_certs`): قائمة شهاداتي بكل الحالات (شارة الحالة، ملاحظات الرفض بالأحمر)، زر «إضافة شهادة مهنية»، «تعديل وإعادة إرسال» للمرفوضة، «سحب».
- **الجودة/الأدمن `/quality/pro-certs`** (`page.quality.pro_certs`): تبويبان «بانتظار المراجعة» و«الكل»؛ جدول بحث (اسم العضو، الشهادة، الجهة، الرمز) بفلاتر: الحالة، الكلية، التصنيف، الجهة، منتهية؛ فتح تفصيل: كل الحقول، **تنزيل الملف**، رابط التحقق (`target=_blank rel="noopener noreferrer nofollow"`)، أزرار **قبول/رفض** (`procert.review`)، **تعديل** (`procert.edit`)، **تعطيل** (`procert.deactivate` للأدمن). إدارة التصنيفات (`procert.categories.manage`).
- **الملف الشخصي `/shared/profile/{id}`:** قسم «الشهادات المهنية» بجانب شهادات المنصة: بطاقات (الاسم، الجهة، التصنيف، تاريخ الإصدار، شارة «سارية/منتهية»، الرمز، زر «تحقق» يفتح `verify_url`). ويظهر العدد في رأس الملف («N شهادة مهنية»).

## 5. الإحصاءات (`/quality/pro-cert-stats`، `page.quality.pro_cert_stats`؛ للأدمن والجودة؛ وتظهر ملخصاتها في `/admin/stats` وفي لوحة الجودة)
تُحتسب على `Approved` و`is_active` فقط. فلاتر: الكلية، التصنيف، الجهة المانحة، الفترة (تاريخ الإصدار)، «تضمين المنتهية» (افتراضي نعم).
`GET /api/v1/procerts/stats?facultyId=&categoryId=&issuer=&from=&to=&includeExpired=true`:
```json
{ "totals": {"certs":0,"members":0,"avgPerMember":0.0,"membersWithAtLeastOne":0,"coveragePercent":0.0},
  "topMembers": [{"userId":0,"name":"","facultyName":"","count":0}],          // الأكثر عامةً (procert.stats_top_n)
  "topMembersByFaculty": [{"centerId":0,"facultyName":"","members":[{"userId":0,"name":"","count":0}]}],
  "byFaculty": [{"centerId":0,"facultyName":"","certs":0,"members":0,"avgPerMember":0.0}],
  "byCategory": [{"categoryId":0,"name":"","certs":0,"members":0}],
  "topIssuers": [{"issuer":"","certs":0,"members":0}],
  "byYear": [{"year":2026,"certs":0}] }
```
- الترتيب: عدد الشهادات تنازلياً ثم الاسم. «الكلية» = `employees.center_id` للمستخدم إن كان `is_faculty` وإلا «جهات غير أكاديمية».
- العرض: بطاقات إجمالية + جدول «الأكثر امتلاكاً (عامةً)» + جدول «الأكثر في كل كلية» (قسم لكل كلية يعرض أعلى N) + مخطط أعمدة للكليات (Chart.js) + مخطط دائري للتصنيفات + مخطط خطي للسنوات + جدول أكثر الجهات المانحة، وزر **تصدير CSV** (`procert.stats`).
- كل الاستعلامات من الخادم بـ `GROUP BY`، لا حساب في المتصفح.

## 6. صلاحيات (انظر `03 §11`)
`page.member.pro_certs`, `procert.submit`, `procert.withdraw`, `procert.download.own`, `page.quality.pro_certs`, `procert.review`, `procert.edit`, `procert.download.any`, `procert.deactivate`, `procert.categories.manage`, `page.quality.pro_cert_stats`, `procert.stats`.
**تنزيل الملف:** يُسمح لصاحبه (`procert.download.own` + ملكيته) أو لمن يملك `procert.download.any`؛ غير ذلك `403`. كل تنزيل يُسجَّل.

## 7. إشعارات وسجلات
أنواع الإشعار: `ProCertSubmitted` (للجودة)، `ProCertApproved`، `ProCertRejected` (لصاحبها). سجلات التدقيق: `procert.submit|resubmit|approve|reject|edit|withdraw|deactivate|restore|download` بفئة `Certificate`.
