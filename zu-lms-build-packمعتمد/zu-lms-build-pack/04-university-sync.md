# 04 — استيراد بيانات الجامعة (`IUniversitySyncService`)

## 1. الواجهات الأربع (روابطها إعدادات `sync.*_url`)
| المصدر | الرابط الافتراضي |
|---|---|
| الموظفون | `https://eservices.zu.edu.jo/ords/fees/qa/employees` |
| الأقسام | `https://eservices.zu.edu.jo/ords/fees/qa/departments` |
| المراكز | `https://eservices.zu.edu.jo/ords/fees/qa/centers` |
| البريد | `https://eservices.zu.edu.jo/ords/fees/qa/email` |

كل استجابة: `{"items":[...]}` (قد تحوي `hasMore`/`limit`/`offset` بصيغة Oracle ORDS).

## 2. حقائق مستخلصة من العينات (تحكم التصميم)
1. **`employee_id` ليس فريداً عبر الجهات:** يتكرر الرقم نفسه مع `side` مختلف (مثل 1 و3). **المفتاح = (`side`, `employee_id`).** وكذلك `center_id` يتكرر عبر الجهات و`department_id` أيضاً → مفتاحاهما (`side`,`center_id`) و(`side`,`department_id`).
2. اسم حقل الجهة يختلف: `side` في employees/departments/centers و**`side_id`** في email. جهات: 320 جامعة الزرقاء، 321 مدارس الجامعة، 369 الكلية التقنية المتوسطة، 366 زاركي، 319 و345 أخرى.
3. الربط (تم التحقق منه على العينات): `employees(side, center_id)` → `centers(side, center_id)`؛ `employees(side, department_id)` → `departments(side, department_id)` وللقسم `center_id` يطابق مركز الموظف.
4. `centers.centr_head` هو الرقم الوظيفي لعميد/رئيس المركز (ويطابق `employees.cntr_mgr_id`) **بلا جهة** → افترض جهة المركز نفسها. يُستخدم **اقتراحاً فقط** عند تعيين العميد.
5. مراكز `unit_id=8` ليست كلها كليات (فيها «وحدة تنسيق المساقات الخدمية» 102، «عمادة الدراسات المسائية» 121، ودمجان 61 و62) → `is_faculty` قرار محلي يعدّله الأدمن.
6. الأقسام تضم مكاتب وشعباً إدارية، لا أقساماً أكاديمية فقط.
7. نصوص تحوي مسافات زائدة أو بادئة (`" محمد علي"`). التواريخ `…T21:00:00Z` = منتصف ليل عمّان.
8. `main_job_dscp` بصيغتين («اداري» و«إداري») → اعتمد `main_job_code` (1 تدريسي، 2 إداري، 3 مستخدم) لا النص.
9. بريد: بعض الصناديق **مشتركة/وحدات** (مثل `hr@`, `medical@`, `student@`, `it1@`) وبريد المدارس `@zuis.edu.jo` والتقنية `@ztic.edu.jo`. أحرف كبيرة أحياناً (`Security@`).
10. العينات فيها بيانات شخصية حساسة (`sid`, `birth_date`, `mother_name`, `bassport`, `mobile`) → **لا تُستورد** (`02 §1`).

## 3. جلب البيانات (`UniversityApiClient`)
```
GET {url}?limit={sync.page_limit}&offset={k}   (k = 0، ثم k += عدد العناصر المستلمة)
كرّر طالما hasMore == true.  إن لم يوجد hasMore في الاستجابة فهي الأخيرة.
حد أقصى 2000 صفحة (حماية من حلقة لا نهائية) → خطأ.
المهلة sync.timeout_seconds، إعادة المحاولة 3 مرات بتأخير 2،4،8 ثوانٍ.
```
أي صفحة تفشل بعد المحاولات → فشل **ذلك المصدر كاملاً**: لا يُطبَّق أي تغيير منه ولا يُحسب أي سجل «مفقوداً». استجابة بلا عناصر بينما الجدول المحلي فيه سجلات فعّالة تُعامل **فشلاً مشبوهاً** (لا اعتماد). **لا يُعتمد تحديث ناقص أو فاشل دليلاً على اختفاء أحد.**

## 4. ترتيب التنفيذ وتفاصيل كل مصدر
قفل `pg_try_advisory_lock(0x5A55_53594E43)`: تشغيل واحد في كل مرة (وإلا `SYNC_ALREADY_RUNNING`). أنشئ `sync_runs`. ثم بهذا الترتيب: **centers → departments → employees → emails → provisioning → كشف المشترك**.

### 4.1 الجهات (`university_sides`)
اجمع كل قيم `side`/`side_id` الواردة؛ أنشئ ما ينقص بـ `is_imported=false` (الاسم من `side_desc` في employees إن وُجد). **تُعالَج فقط صفوف الجهات `is_imported=true`** (الافتراضي 320). الصفوف الأخرى تُتجاهل بصمت.

### 4.2 المراكز → `centers`
| المصدر | العمود |
|---|---|
| `side` | `side_code` |
| `center_id` | `center_code` |
| `center_dscp` | `name_ar` (تقليم) |
| `unit_id` | `unit_code` |
| `centr_head` | `head_employee_code` |
| `fac_registrar` | `registrar_employee_code` |
ممنوع استيراد أي عمود آخر. عند **الإنشاء فقط**: `is_faculty = (unit_code ∈ org.faculty_unit_codes)` **ثم** اضبط `false` للمراكز (320: 61، 62، 102، 121)؛ `short_code = NULL`. **المزامنة لا تعدّل `is_faculty` ولا `short_code` ولا `is_active` بعد الإنشاء أبداً.**

### 4.3 الأقسام → `departments`
`side`→`side_code`، `department_id`→`department_code`، `department_dscp`→`name_ar`، `center_id`→`center_code` ثم `center_id` (FK) بـ (`side_code`,`center_code`)، `reg_code`→`reg_code`، `gate_dep_head`→`head_employee_code`. إن لم يوجد المركز → `center_id=NULL` + عنصر مراجعة `UnresolvedReference`.

### 4.4 الموظفون → `employees`
| المصدر | العمود | معالجة |
|---|---|---|
| `side` | `side_code` | |
| `employee_id` | `employee_code` | |
| `fname,sname,tname,lname` | الأربعة + `full_name_ar` | `NameRules.Join` |
| `ename` | `full_name_en` | تقليم، فارغ→NULL |
| `center_id`/`department_id` | `*_code` ثم FK | بالمعرّفين مع `side` |
| `main_job_code`/`main_job_dscp` | `main_job_code`/`main_job_desc` | `is_academic = main_job_code ∈ directory.academic_main_job_codes` |
| `sub_job_id`/`sub_job_dscp` | `sub_job_code`/`sub_job_desc` | |
| `emp_status`/`emp_status_desc`/`chk_emp_stat` | كما هي | **تُحفظ منفصلة عن `users.is_active`** |
| `start_work_date`/`stop_work_date` | `date` | تحويل UTC→عمّان |
حساب `source_hash` = SHA-256 لـ JSON ثابت الترتيب للحقول المحوّلة. **تطابق الهاش = تخطَّ (unchanged).** `search_text` يُعاد حسابه عند التغيير.
بعد التحديث: لكل موظف تغيّرت `emp_status` → `IUserStateCache.Invalidate` لحسابه (فيُمنع/يُسمح فوراً وفق `access.eligible_emp_status_codes`).

### 4.5 البريد → `employees.source_email`
المفتاح (`side_id`,`employee_id`)؛ `email` تقليم + أحرف صغيرة؛ يُهمل `zu_cler`, `blc_cler`, `extention`. صف لموظف غير موجود محلياً → تجاهل. مفتاح مكرر → خذ أول قيمة غير فارغة + عنصر مراجعة `DuplicateSourceKey`.

### 4.6 تجهيز الحسابات (Provisioning)
لكل `employees` غير `source_missing` في جهة مستوردة ليس له `users.employee_id`:
1. `login_name = employee_code` كنص. إن كان مأخوذاً (مدرب خارجي أو موظف آخر) → لا تنشئ، عنصر مراجعة `DuplicateLoginName`.
2. أنشئ المستخدم `Employee` بالأسماء ولغة `ar`، كلمة أولية شخصية (`03 §3`)، `must_change_password=true`، دور `Member`، `credentials_email_status=NotSent`.
3. للموجودين: حدّث `full_name_ar/en` و`search_text` فقط.
**لا يُرسَل أي بريد تلقائياً** (الإرسال أمر يدوي من الأدمن).

### 4.7 المفقودون
بعد جلب **ناجح كامل** لمصدر: سجل محلي (في جهة مستوردة) لم يظهر → `source_missing=true`، `source_missing_since=الآن`، عنصر مراجعة `MissingFromSource`. **لا تعطّل ولا تغيّر `is_active` ولا تمنع الدخول.** إن ظهر لاحقاً → `source_missing=false` وأغلق العنصر آلياً (`Ignore` + ملاحظة «عاد للظهور»).

### 4.8 كشف البريد المشترك
بعد الدمج: أي بريد فعّال (`email_override ?? source_email`) يخص أكثر من مستخدم فعّال → عنصر مراجعة `SharedEmail` لكل بريد (المفتاح = البريد).

## 5. ملخص التشغيل
`sync_runs.stats_json` = لكل مصدر: `{fetched, added, updated, unchanged, missing, errors:[{key, message}]}` + `accountsCreated`, `reviewItemsCreated`، وقتا البداية والنهاية. الحالة: `Success` (كل المصادر نجحت)، `Partial` (بعضها)، `Failed` (كلها).

## 6. واجهة الأدمن `/admin/sync` (`page.admin.sync`)
- زر **«تحديث بيانات الجامعة»** (`sync.run`): يبدأ التشغيل في الخلفية ويعرض شريط تقدم يستطلع `GET /api/v1/sync/runs/{id}` كل ثانيتين.
- جدول **الجهات** مع مفتاح «استيراد هذه الجهة» (يعدّل `is_imported`).
- جدول **سجل التشغيلات** (الوقت، المنفّذ، الحالة، الأعداد).
- جدول **عناصر المراجعة المفتوحة** بفلتر السبب؛ لكل عنصر أزرار: **إبقاء** (`Keep`)، **تعطيل** (`Disable`: يعطّل حساب المستخدم منطقياً بسبب إلزامي)، **تجاهل** (`Ignore`) + ملاحظة (`sync.review.resolve`).
- صفحة `/admin/centers` (`page.admin.centers`): جدول المراكز (بحث) بتعديل `is_faculty` و`short_code` (حروف كبيرة لاتينية 2–10) و`is_active` ويعرض «عميد مقترح: <اسم الموظف>» إن وُجد.
