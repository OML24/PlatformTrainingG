# 08 — صفحات الواجهة (Razor) ونقاط الـ API

## 1. قواعد الواجهة العامة
- **الشكل = البروتوتايب** (`prototype/css/*.css`, بنية `sidebar` + `topbar` + بطاقات). انقل ملفات CSS إلى `wwwroot/css` وطبّقها على `_Layout`. ما لا يوجد في البروتوتايب من التيمبليت.
- كل صفحة Razor: `PageModel` رفيع (تحميل بسيط للقوالب + `[RequirePermission]`) + ملف JS `wwwroot/js/pages/<area>/<page>.js` يستدعي الـ API بـ AJAX. الجداول تُملأ من `GET` بالبحث/الترتيب/الترقيم من الخادم.
- **الأجزاء الثابتة في كل شاشة:** الشريط الجانبي حسب الدور (عناصره بمفاتيح `page.*`)، هيدر فيه: مبدّل السياق، تبديل اللغة (ع/EN)، جرس الإشعارات (قائمة منسدلة + عدّاد + «تعليم الكل مقروءاً»)، اسم المستخدم وخروج.
- **التعريب والإنجليزية شاملان:** عند اختيار English لا يبقى أي نص واجهة عربي في Razor أو الرسائل والنوافذ والجداول المنشأة من JavaScript. تعرض الحقول الثنائية `*En` أولاً مع الرجوع إلى `*Ar` عند غيابها. قاموس الواجهة المركزي يترجم النصوص القديمة والديناميكية ويراقب العناصر التي تضاف بعد تحميل الصفحة.
- **كل إجراء خطر** (تعطيل، إخراج، إلغاء، تغيير حالة، تعطيل شهادات) ينفتح له `_ConfirmModal`: عنوان، تحذير واضح، حقل «السبب» إلزامي حيث ذُكر، زر أحمر.
- **الحالات الفارغة** `_EmptyState` برسالة (مثل نص «لا توجد دورات مسندة لك كمحاضر حالياً، ستظهر هنا تلقائياً فور اعتماد تكليفك من الكلية والجودة»).
- رسائل النجاح/الخطأ بإشعار عائم (Toast). أخطاء الحقول تحت الحقل. لا `alert()`.
- **كل حقل نموذج** له `zu-hint`. مفاتيحها `ctx.field` (مثل `course.title_ar`) وتُنشأ تلقائياً في `field_hints`.
- **ممنوع** حقول التاريخ/الوقت الأصلية (`02`/`00 §3`). استخدم `_DateTimePicker` (من التيمبليت).
- **مخططات الجلسات والحضور** تخص **دورة واحدة** صراحةً: عنوان المخطط = «<رمز الدورة> — <اسم الدورة>»، و«الجلسة الأولى/الثانية…» مرتبطة بها، وعند تغيير الدورة يتغير مصدر المخطط وعنوانه معاً. لا يُجمع مخطط جلسات دورات مختلفة.
- التصدير: `audit.export` (CSV للسجل بالفلاتر) و`procert.stats` (CSV). لا غيرهما.

## 2. كتالوج الصفحات
عمود «بروتوتايب»: القسم المرجعي للشكل. «—» = من التيمبليت.

### 2.1 عامة (`Areas/Public`)
| المسار | المحتوى | بروتوتايب |
|---|---|---|
| `/login` | رقم الدخول، كلمة المرور، «تذكرني»، رابط «نسيت كلمة المرور»، تبديل اللغة | `index.html#login` |
| `/forgot-password` | الرقم الوظيفي + البريد الجامعي (`03 §6`) | — |
| `/change-password` | تغيير إلزامي (جلسة محدودة) | — |
| `/verify?cert=` | `06 §6.3` | — |
| `/access-denied`, `/error` | رسائل | — |

### 2.2 مسؤول النظام (`Areas/Admin`)
| المسار / المفتاح | المحتوى | بروتوتايب |
|---|---|---|
| `/admin/dashboard` `page.admin.dashboard` | مؤشرات: مستخدمون فعّالون، دورات (معتمدة/جارية/منتهية/ملغية)، شهادات سارية، طلبات معلقة، آخر 10 أحداث، مؤشر آخر مزامنة، وبطاقات ظاهرة لمراجعة الأثر وإدخال الدورات/الشهادات السابقة وإدارة إصدار الشهادات | `admin#dashboard` |
| `/admin/users` `page.admin.users` | جدول (بحث، فلاتر: النوع/الدور/الحالة/الكلية)، «تعطيل/استعادة»، تبويب «طلبات المدربين الخارجيين» (`05 §7`)، «إضافة حساب محلي» | `admin#users` + `addUserModal`,`addExtModal` |
| `/admin/users/{id}` | الحساب + الأدوار + **جدول الصلاحيات الثلاثي** (`03 §10.4`) | — (من التيمبليت: جداول/مبدّلات) |
| `/admin/credentials` `page.admin.credentials` | `03 §8` | `admin#credentials` |
| `/admin/courses` `page.admin.courses` | كل الدورات + أزرار (تعطيل/استعادة/أرشفة/فتح من جديد/تغيير حالة/تعديل) | `admin#courses` (يُغيَّر «حذف»→«تعطيل») |
| `/admin/courses/new` | إنشاء مباشر (`05 §6`) | `dean#newcourse` شكلاً |
| `/admin/courses/{id}` | تبويبات: البيانات، المحاضرات، المشاركون (إضافة/إخراج)، الامتحان، الأثر، الشهادات، سجل الحالة، السجل | — |
| `/vault` `page.vault` | المستودع المركزي: جدول (نوع، دورة، كلية، تاريخ، رافع) بحث/فلترة، معاينة وتنزيل | `admin#docsvault` |
| `/admin/audit` `page.admin.audit` | `09 §1` | `admin#auditlog` |
| `/admin/live-feed` `page.admin.live_feed` | تدفق مباشر | `admin#auditlog` |
| `/admin/settings` `page.admin.settings` | الإعدادات مجمّعة (`03 §12`) | `admin#settings` |
| `/admin/password-policy` | `03 §9` + قائمة الحظر | — |
| `/admin/hints` `page.admin.hints` | جدول التلميحات (بحث بالمفتاح/السياق)، إضافة/تعديل/حذف منطقي | — |
| `/admin/roles` `page.admin.roles` | مصفوفة (دور × صلاحية) قابلة للتعديل + فهرس الصلاحيات | — |
| `/admin/centers`, `/admin/sync` | `04 §6` | — |
| `/admin/certificate-template` | `06 §6.5` | — |
| `/admin/stats` `page.admin.stats` | إحصاءات الجامعة/كلية/دورة/عضو + ملخص الشهادات المهنية (`12 §5`) | `admin#stats` |
| `/admin/needs` | قراءة وإدارة الاحتياجات | `admin#needs` |

### 2.3 الجودة (`Areas/Quality`)
| المسار / المفتاح | المحتوى | بروتوتايب |
|---|---|---|
| `/quality/dashboard` | مؤشرات: معلّقات (مقترحات، إلغاء، احتياجات، شهادات مهنية، طلبات مدربين)، دورات حسب الكلية/المحور، وبطاقة ظاهرة لطلبات قياس الأثر الموجهة للجودة | `quality#dashboard` |
| `/quality/proposals` `page.quality.proposals` | `05 §5` | `quality#approvals` + `rejectModal` |
| `/quality/cancellations` | `05 §9` | `quality#cancellations` |
| `/quality/courses` + `/new` + `/new-historical` + `/{id}` | سجل البرامج + إنشاء مباشر/تاريخي | `quality#courses` |
| `/quality/tracks` `page.quality.tracks` | إنشاء/تعديل/ترتيب/أرشفة المحاور | `quality#tracks` |
| `/quality/external-instructors` | المدربون الخارجيون (بحث)، طلب إنشاء حساب للأدمن، حالة الطلبات | `quality#exttrainer` |
| `/quality/needs` | `07 §3` | `quality#needs` |
| `/quality/evaluations` | `07 §1.6` | `quality#evals` |
| `/quality/eval-form` | محرر النسخ `07 §1.2` | — |
| `/quality/certificates` | الشهادات (بحث، حالة، دورة) + «إصدار المعلّقة» | `quality#certs` |
| `/quality/pro-certs`, `/quality/pro-cert-stats` | `12` | — |
| `/vault` | نفسه | |

### 2.4 العميد (`Areas/Dean`)
| المسار | المحتوى | بروتوتايب |
|---|---|---|
| `/dean/dashboard` | مؤشرات كليته: دورات، مقترحات بالحالة، مشاركون، شهادات، وبطاقة ظاهرة لطلبات قياس الأثر الموجهة لعميد الكلية | `dean#dashboard` |
| `/dean/courses` | دورات الكلية بالحالة، المعاد بالأحمر، زر «طلب إلغاء» (`cancelModal`) | `dean#mycourses` |
| `/dean/propose` | `05 §4` | `dean#newcourse` |
| `/dean/members` | جميع أعضاء كلية العميد مع البحث، وفتح الملف الشخصي، ورابط مباشر لإحصاءات كل فرد. يستنتج الخادم كلية العميد من نطاق دوره ولا يتطلب `centerId` من الواجهة. | `dean#members` |
| `/dean/needs` | `07 §3`؛ المحاور والكليات والمحاضرون قوائم ديناميكية من البيانات الفعالة | — (من التيمبليت) |
| `/dean/stats` | إحصاءات الكلية مع قائمة كاملة بأعضائها وفلتر فردي؛ بعد التحقق أن الشخص من كلية العميد تعرض مشاركاته ودوراته وشهاداته حتى لو كانت الدورة مقدمة أو موجهة من كلية أخرى | `dean#dashboard` |
| «دوراتي كعضو متدرب»، «الملف الأكاديمي والشهادات» | روابط إلى `/member/*` و`/shared/profile/{me}` عبر المبدّل | `dean#memberrole`,`dean#profile` |

### 2.5 المحاضر (`Areas/Instructor`) — مساحة معزولة لكل دورة
| المسار | المحتوى | بروتوتايب |
|---|---|---|
| `/instructor/courses` | دورات المحاضر (وللجودة: المُدارة) | `instructor#courses` |
| `/instructor/courses/{id}` | ملخص الدورة: البيانات، العدّادات، إجراء «إنهاء الدورة» | `instructor#coursedetail` |
| `/instructor/courses/{id}/lectures` | إضافة/تعديل/تعطيل محاضرة، رفع مواد، **زر «تفعيل الكود»** وشاشة عرض الكود (وضع العرض الكبير) | `instructor#attendance` + `projectorModal`,`newSessionModal` (يُحذف QR) |
| `/instructor/courses/{id}/attendance` | كشف: لكل محاضرة عمود مربعات تحديد/إزالة يدوياً + عدّاد الحضور | `instructor#attendance` |
| `/instructor/courses/{id}/participants` | `06 §8` | `instructor#certificates` |
| `/instructor/courses/{id}/exam` | `06 §4` (زر «إنشاء امتحان» + ناجح/راسب) | `instructor#exams` |
| `/instructor/courses/{id}/impact` | `07 §2` | `instructor#impact` |
| `/instructor/courses/{id}/evaluation` | نتائج دورته فقط | — |
| «حسابي كعضو/دوراتي كمتدرب» | عبر المبدّل | `instructor#memberrole` |

### 2.6 العضو (`Areas/Member`)
| المسار | المحتوى | بروتوتايب |
|---|---|---|
| `/member/dashboard` | بطاقات: دوراتي، حضوري، شهاداتي | `member#dashboard` |
| `/member/available` | الدورات المتاحة (`05 §10`) بزر «سجّلني» | `member#available` |
| `/member/enrolled` + `/member/courses/{id}` | دوراتي؛ التفاصيل: محاضرات (بطاقة + خانة كود)، عداد الحضور، المواد، الامتحان، «تعبئة التقييم»، «رفع الأثر» | `member#enrolled` + `evalModal` |
| `/member/enter-code` | إدخال سريع: محاضرات اليوم/الجارية للدورات المسجّل بها مع خانة كود لكل منها | `member#entercode` |
| `/member/certificates` | الشهادات السارية + تنزيل PDF | `member#certs` |
| `/member/impact` | رفع/متابعة الأثر | `member#impact` |
| `/member/pro-certs` | `12` | — |
| `/member/teaching` | دوراتي كمحاضر (فارغة بالرسالة الثابتة إلى أن يُكلَّف) | `member#myteaching` |

### 2.7 مشتركة (`Areas/Shared`)
| المسار | المحتوى |
|---|---|
| `/shared/directory` `page.shared.directory` | «دليل الأعضاء»: بحث (اسم/كلية/قسم/خبرات) + بطاقات → الملف. المصدر `members-directory.html` |
| `/shared/profile/{userId}` | ملف مهني: الاسم/الانتماء/المناصب من الجامعة، نبذة وصورة وخبرات ووسيلة التواصل (محلية، يحررها صاحبها `profile.edit.own`)، **بطاقات الشهادات السارية**، **الشهادات المهنية المعتمدة**. لا غياب ولا نتائج ولا كلمات مرور ولا ملفات إدارية |
| `/account/password`, `/account/settings` | تغيير كلمة المرور (موظفون)، اللغة |
المنشور = لمستخدمي المنصة فقط، لا نشر على الإنترنت.

## 3. نقاط الـ API (`/api/v1`) — كلها JSON بالعقد `01 §6.1`، وتتحقق الخدمة من النطاق
| المورد | المسارات |
|---|---|
| `auth` | `POST login`, `POST logout`, `POST change-password`, `POST forgot-password`, `PATCH language`, `GET me` |
| `users` | `GET users?q=&type=&role=&status=`, `GET users/{id}`, `PATCH users/{id}`, `POST users/{id}/deactivate|restore`, `PUT users/{id}/roles`, `GET|PUT users/{id}/permissions` |
| `credentials` | `GET credentials?q=&filter=`, `POST credentials/{userId}/reveal`, `PUT credentials/{userId}/password`, `PUT credentials/{userId}/email`, `PUT credentials/{userId}/access-override`, `POST credentials/{userId}/send`, `POST credentials/send-bulk/preview|confirm`, `PUT credentials/shared-initial`, `POST credentials/{userId}/email-status` |
| `settings` / `policy` / `hints` | `GET|PUT settings`, `GET|PUT password-policy`, `GET|POST|DELETE blocked-passwords`, `GET hints?context=`, `POST|PUT|DELETE hints/{id}` |
| `roles` | `GET roles`, `GET permissions`, `PUT roles/{id}/permissions` |
| `sync` / `centers` | `POST sync/run`, `GET sync/runs[/{id}]`, `GET|PUT sync/sides`, `GET sync/review`, `POST sync/review/{id}/resolve`, `GET centers`, `PUT centers/{id}` |
| `tracks` | `GET tracks`, `POST|PUT tracks/{id}`, `POST tracks/{id}/archive|unarchive` |
| `courses` | `GET courses?…`, `GET courses/{id}`, `POST courses` (إنشاء مباشر)، `PUT courses/{id}`, `POST courses/{id}/deactivate|restore|archive|unarchive|reopen|force-status|finish`, `GET courses/{id}/history` |
| `proposals` | `POST proposals`, `PUT proposals/{courseId}`, `POST proposals/{courseId}/resubmit|approve|return|reject`, `GET proposals/{courseId}/revisions` |
| `cancellations` | `POST courses/{id}/cancellation-request`, `POST cancellations/{id}/approve|reject`, `POST courses/{id}/cancel` (مباشر) |
| `participants` | `GET courses/{id}/participants`, `POST courses/{id}/register`, `POST courses/{id}/participants`, `DELETE courses/{id}/participants/{pid}` (تعطيل بسبب) |
| `lectures` | `GET|POST courses/{id}/lectures`, `PUT|DELETE lectures/{id}`, `POST lectures/{id}/activate-code`, `POST lectures/{id}/enter-code`, `GET lectures/{id}/sheet`, `PUT lectures/{id}/attendance/{pid}`, `POST|GET lectures/{id}/materials` |
| `exams` | `POST courses/{id}/exam`, `GET courses/{id}/exam`, `PUT courses/{id}/exam/results/{pid}`, `PUT courses/{id}/exam-requirement`, `POST courses/{id}/exam/results/disable` |
| `evaluation` | `GET evaluation/form`, `POST evaluation/form/draft`, `PUT evaluation/form/draft`, `POST evaluation/form/publish`, `GET courses/{id}/evaluation`, `POST courses/{id}/evaluation`, `PUT courses/{id}/evaluation-period`, `GET evaluation/results?…` |
| `impact` | `GET courses/{id}/impact`, `POST courses/{id}/impact`, `POST impact/{id}/approve|return` |
| `certificates` | `GET certificates?…`, `GET certificates/mine`, `GET certificates/{id}/pdf`, `POST certificates/issue-pending`, `POST certificates/{id}/revoke`, `GET public/verify/{ref}`, `POST certificate-template`, `GET certificate-template`, `POST certificate-template/preview` |
| `needs` | `GET|POST needs`, `PUT needs/{id}`, `POST needs/{id}/resubmit|approve|return` |
| `ext-instructors` | `GET ext-instructors?q=`, `POST ext-requests`, `GET ext-requests`, `POST ext-requests/{id}/complete|reject` |
| `profiles` | `GET directory?q=&centerId=&departmentId=`, `GET profiles/{id}`, `PUT profiles/me`, `POST profiles/me/photo` |
| `procerts` | `GET|POST procerts`, `PUT procerts/{id}`, `POST procerts/{id}/withdraw|approve|reject|deactivate|restore`, `GET procerts/{id}/file`, `GET procerts/stats`, `GET|POST|PUT procert-categories` |
| `vault` | `GET vault?…`, `POST vault`, `GET vault/{id}/file`, `POST vault/{id}/deactivate` |
| `audit` | `GET audit?…`, `GET audit/live?afterId=`, `GET audit/export?…` |
| `notifications` | `GET notifications?unread=`, `POST notifications/{id}/read`, `POST notifications/read-all` |
| `stats` | `GET stats/university`, `stats/faculty/{id}`, `stats/course/{id}`, `stats/member/{id}` |
| `lookups` | `GET lookups/members?q=&centerId=`, `lookups/courses?q=`, `lookups/centers`, `lookups/tracks` |
| `files` | `POST files` (رفع بنوع `kind`)، لا حذف |
**الصلاحيات:** كل نقطة تحمل مفتاحها من `03 §11`؛ اربط كل مسار بالمفتاح المطابق لاسمه (مثلاً `approve` ← `proposal.decide`).

## 4. إحصاءات (`IStatsService`)
- **الجامعة (أدمن/جودة):** إجمالي الدورات حسب الحالة، حسب الكلية، حسب المحور، حسب النمط؛ المشاركون؛ نسبة الحضور المتوسطة؛ الشهادات الصادرة/الملغاة؛ الدورات الملغية وأسبابها؛ مؤشرات الرضا من النسخة وسؤال بسؤال (قابلة للمقارنة فقط)؛ نتائج الامتحانات (ناجح/راسب/غير مرصود).
- **كلية (عميد/جودة/أدمن):** الأرقام نفسها لكلية واحدة.
- **دورة (محاضر/جودة/أدمن):** حضور كل محاضرة (مخطط بعنوان الدورة)، الاستحقاق، التقييم.
- **عضو:** دوراته، حضوره، شهاداته (للأدمن/الجودة).
- الفلاتر: فترة، كلية، محور، نمط. الرسوم بـ Chart.js محلي.

## 5. مخالفات البروتوتايب (يُعدَّل شكلاً ليوافق الحزمة؛ لا تنقلها حرفياً)
1. احذف **«مركز العرض والديمو»** من كل القوائم وأي صفحة ديمو.
2. `instructor`: احذف «التحضير المزدوج (Dual-QR)» وشاشة QR → كود حضور لكل محاضرة (`06 §2`). احذف «بناء نموذج التقييم». نتائج الامتحان ناجح/راسب فقط (لا أرقام).
3. `member`: احذف «مقترح احتياج تدريبي» و«سجل الغياب والاعتذار». «إدخال رمز الحضور» = كود لكل محاضرة.
4. `admin`: «إدارة وحذف الدورات» → «إدارة الدورات»؛ كل «حذف» → «تعطيل» (وأضف أزرار الأرشفة والاستعادة). أضف الصفحات الجديدة (الإعدادات، السياسة، التلميحات، الأدوار، المراكز، المزامنة، القالب).
5. أي حقل تاريخ/وقت أصلي (`dean.html`, `instructor.html`) → `_DateTimePicker`.
6. أي رقم ثابت في العروض (75%، 5 دقائق، 4 أرقام) → من الإعدادات.
7. الحضور بعدد المحاضرات لا بالساعات. أي نص «ساعات الحضور» يُحذف.
8. لا وضع داكن ولا زر مظهر.
9. نسخ الأسماء الوهمية في البروتوتايب تُستبدل بالبيانات الحية؛ لا تنسخ نصوصها إلى الإنتاج.
10. أضف: العربية/الإنجليزية، البحث، مبدّل السياق، الجرس، الشهادات المهنية، الإحصاءات.
