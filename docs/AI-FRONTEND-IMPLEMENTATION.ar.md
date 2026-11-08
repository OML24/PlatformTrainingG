# عقد تنفيذ الفرونت — ZU-LMS بهوية البروتوتايب ومكوّنات Paces

**الإصدار:** 1 — 31 أكتوبر 2026. **المخاطب:** AI Agent منفّذ. **هذا مخطط تنفيذ وليس تقريراً عن واجهة أُنجزت.**

## 0. النتيجة المحددة المطلوبة

حوّل واجهة المشروع `C:\Users\VIP\Documents\ChatGPT\New project\newnew\zu-lms` إلى واجهة تطابق **التخطيط والتنسيق والألوان والكثافة والهدوء البصري للبروتوتايب المرفق**، باستخدام مكوّنات `template/paces/` لكل عنصر متاح فيه. التحسين المسموح: اتساق، استجابة، وضوح، لغتان، إتاحة، حالات تحميل/خطأ؛ وليس هوية جديدة أو ميزات جديدة أو تغييراً في أعمال الباك إند.

**المخرج:** واجهة حقيقية متصلة بالعقود الحالية، بكل الوظائف التي يتيحها الخادم، لا صفحات عرض ساكنة ولا بيانات Demo. المتطلبات التي لا يدعمها الخادم تُوثّق كفجوات وتمنع قبولها، ولا تُزوّر في JavaScript.

### مصادر القرار وترتيبها

1. `../zu-lms-build-packمعتمد/zu-lms-build-pack/00..12` وقرارات المستخدم الصريحة المعتمدة. اقرأها كلها.
2. هذا الملف يحدد خطة التنفيذ والعرض ضمن الحزمة، ولا يغير قواعدها.
3. `prototype/css/styles.css` **ثم** `prototype/css/experience.css` والصفحات `admin/quality/dean/instructor/member/members-directory/index.html`. الطبقة الثانية جزء من المرجع، وليست اختيارية.
4. `template/paces/` للمكوّنات؛ لا تنسخ التطبيق التجريبي أو menus/config/themes.
5. الكود الحالي لتحديد عقود الربط، لا لتبرير مخالفة متطلب معتمد.

لا تستخدم نسخ بروتوتايب/تيمبليت من مشاريع أخرى بجانب `newnew`. النسختان المستخرجتان داخل `zu-lms` هما مصدر العمل؛ الأرشيفان `../prototype.zip` و`../template.zip` مرجع احتياطي، لا تُعد استخراجاً فوق ملفات معدّلة.

## 1. سور حماية الباك إند — ملزم

### مسموح بالتعديل

- HTML/Razor العرض فقط في `src/ZuLms.Web/Pages/**/*.cshtml` و`Areas/Public/Pages/*.cshtml`، مع إبقاء `@page` وModels والحقن واستدعاءات الصلاحيات وعقود route كما هي.
- CSS التطبيق، وملفات JavaScript الخاصة بالتفاعل/rendering، وموارد النصوص `Localization/SharedResource.ar.resx` و`.en.resx`.
- إضافة partials عرض، وCSS/JS عرض، وأصول محلية من التيمبليت عند الحاجة، و`data-testid`/ARIA دون تغيير بيانات العمل.
- وثائق الجرد والتغييرات والأدلة، واختبارات المتصفح المسموح بأدواتها الموجودة.

### محمي من التعديل في هذه المهمة

`ZuLms.Domain/**`, `ZuLms.Application/**`, `ZuLms.Infrastructure/**`, `ZuLms.Web/Api/**`, `Security/**`, `Middleware/**`, `Background/**`, `Program.cs`, `*.cshtml.cs`, ملفات C# الأخرى وTagHelpers، migrations/seed، `deploy/**`, `.github/**`, ملفات الحل والحزم والإعدادات، مخطط/قيود قاعدة البيانات، قواعد الحساب، JWT/CSRF/CSP، مدد الجلسات، endpoints، DTOs، permissions، enums، وخدمات PDF والبريد والمزامنة.

**الإجراء عند العائق:** إن كان العرض المطلوب يحتاج endpoint/DTO/صلاحية/route غير موجود: سجّل في `docs/frontend-blockers.md` المتطلب والملف والعقد الناقص وما يحتاجه وكيل الباك إند. أوقف تلك الجزئية فقط، وواصل الأجزاء المستقلة. لا تنشئ API بديلة، ولا قراءة DB من Razor، ولا توسّع صلاحية، ولا تخفِ النقص خلف زر يعمل شكلياً.

### ما يجب أن يبقى ثابتاً حتى داخل ملفات مسموحة

- `@page`، route params، أسماء POST/PUT fields وenum values، method/path/payload للطلبات، نوع JSON/FormData، `version/xmin`، CSRF، معالجة 401/refresh، credentials same-origin.
- شروط الصلاحية والنطاق الحالية؛ إن اكتشفت أنها تخالف الحزمة وثّقها، ولا تستبدلها بفحص اسم الدور في JS.
- أسماء APIs العامة: `ZuLms.api`, `ZuLms.toast`, `ZuLms.applyFieldErrors`, `ZuLms.ui.confirm/prompt/show`, `ZuLmsPickers` حتى لو أعيد تنفيذ مظهرها.
- حفظ المعرّفات الكبيرة/الأرقام/التواريخ بنفس تنسيق العقد. لا تحول ID إلى نص العرض ولا تعيد حساب أهلية أو إحصاء من صفوف الصفحة.

## 2. تشخيص البداية الذي يجب معالجته

من قراءة الملفات الحالية، لا من اختبار متصفح مكتمل:

| الملاحظة | الملف | الإجراء المحدد |
|---|---|---|
| `app.css` يحمل كثيراً من styles.css، لكن تأثير experience غير مطبق | `_Layout.cshtml`, `wwwroot/css/app.css` | انقل الطبقتين بشكل منظّم وفق §5، لا تكتفِ بتغيير primary |
| تنسيق Bootstrap افتراضي للداخل مع shell مختلف | `site.css` والصفحات | طبّق أنماط الصفحات والمكونات في هذا الملف لكل شاشة |
| نصوص عربية ثابتة في التنقل وصفحات وJS | `_Layout.cshtml`, `course-details.js` وغيرها | تحويل كامل إلى موارد، مع الحفاظ على النصوص الحرفية للحزمة |
| RTL مثبت في body/sidebar/forms | `app.css`, `site.css` | خصائص منطقية وLTR صحيح؛ لا تعويض بترجمة النص وحدها |
| `nav-item` نمط عام قد يصيب nav-tabs | `app.css` | قصر تنسيق الملاحة على `.sidebar-nav .nav-item` |
| توست layout اسمه `zu-toast-host` وآخر ينشئه api باسم `zu-toasts` | `_Layout.cshtml`, `api.js` | مضيف واحد وفق §6، أبقِ واجهة `ZuLms.toast` |
| `<dialog>` وحوارات كثيرة مستقلة | `site.js`, `Pages/Courses/Details.cshtml` | احتفظ بعقود الإغلاق/Promises والـIDs، وحّد البنية المرئية دون كسر showModal |
| تاريخ/وقت أصلي | `_DirectCourseForm.cshtml`, `Details.cshtml` وغيرها | جرد شامل واستبدال وفق §7؛ لا regex أعمى |
| سكربت inline في الملف المهني | `Pages/Shared/Profile.cshtml` | انقله لملف خارجي ومرر userId عبر data attribute؛ لا تخفف CSP |
| Profile عرض فقط ظاهرياً في الملف الحالي | `Pages/Shared/Profile.cshtml` | ربط التحرير بالـAPI الموجود بعد جرد عقده؛ إن غير مدعوم وثّق الحظر |
| القوائم مبنية على مجموع الصلاحيات وroleLabel على أولوية أعلى دور | `_Layout.cshtml` | عرض سياقي بالمبدّل الفعلي وفق §8 دون تعديل auth |
| مسارات مطلوبة مدمجة أو غير ظاهرة بالجرد | الحزمة 08 والكود | جدول GAP صريح؛ لا إضافة routes أثناء تصميم الشكل |

## 3. المرحلة F0 — التثبيت والجرد قبل أول تعديل

1. اعمل من جذر `zu-lms` فقط؛ افحص AGENTS إن أضيفت لاحقاً، واقرأ الحزمة و`QUESTIONS.md` و`docs/implementation-progress.md` و`docs/template-inventory.md`.
2. سجّل baseline للملفات والبصمات؛ لأن المشروع قد يكون Untracked تحت Git الأب، لا تعتمد `git diff` وحده. لا تستخدم git clean/reset ولا تنقل عمل المستخدم.
3. أنشئ `docs/frontend-baseline.md`: التاريخ، SHA/بصمة الإصدار، المتصفحات، رابط التشغيل، الصفحات الحالية وصورها AR/EN desktop/mobile، الأخطاء الموجودة مسبقاً.
4. أنشئ `docs/frontend-contracts.csv` بأعمدة: page/file، IDs، names، data attributes، selectors، event، method/path، payload keys/types، response keys، permissions، loading/error behavior. اقرأ ملفات JS كاملة لكل صفحة قبل تغيير DOM.
5. أنشئ `docs/frontend-page-matrix.csv`: requiredRoute، actualRoute، Razor، JS، prototypeSection، templateSource، pattern، permission، state، stage، screenshot، blocker.
6. أضف في `docs/template-inventory.md` جرداً دقيقاً للمقاطع المستخدمة فعلياً: الملف، selector، تبعيات CSS/JS، ما نُقل، وما تم تكييفه. الجرد الحالي تمهيدي فقط.
7. لصور المرجع: ملفات prototype تستخدم مسارات `logo.png` و`template/...` وCDN قد لا تُحل من مكانها الحالي. أنشئ **نسخة معاينة خارج ملفات المرجع الأصلية**، واربط الأصول المحلية الموجودة بالفعل. سجّل التصحيح؛ لا تعِد تصميم المرجع لتعويض الأصل المفقود ولا تجعل صفحة مكسورة أساس المقارنة.
8. افتح جميع أقسام الأدوار، لا أول dashboard فقط؛ الأقسام الفعلية مثل `sec-dashboard` و`sec-users`، أما `admin#dashboard` في الحزمة فوصف للقسم. لا تفترض أن hash وحده يعرضه.
9. اقرأ `styles.css` و`experience.css` معاً وعاين computed styles. أنشئ صوراً مرجعية للـshell، cards، table، form، modal، directory، profile، lecture/code.
10. التبعيات الناقصة/المسارات غير المنفذة/تعارضات الحزمة توثّق قبل التطوير. لا تصنف الفرق المساري كتحسين تصميم مسموح.

**بوابة F0:** جرد كل صفحة وعقد كل عنصر قبل تعديلها، وبصمات الملفات المحمية وصور baseline موجودة. لا تبدأ إعادة كتابة شاملة.

## 4. نظام التصميم المحسوم

الأصل هو البروتوتايب بطبقتيه. القيم التالية مواصفات التنفيذ؛ لا تضف ألواناً وهوية مستقلة.

### 4.1 الألوان والحالات

| Token | القيمة | الاستخدام |
|---|---|---|
| navy | `#022C5A` | الهوية، العناوين، primary، active |
| navy-dark / light | `#011B38` / `#044284` | النص الداكن/hover |
| background | `#F4F6F9` | خلفية مساحة العمل كما styles.css |
| surface / surface-subtle | `#FFFFFF` / `#F8FAFC` | بطاقات/رؤوس جداول |
| border / border-light | `#E2E8F0` / `#EDF2F7` | حدود هادئة |
| text / text-secondary / text-muted | `#0F172A` / `#475569` / `#64748B` | muted من experience؛ لا تستخدم #94A3B8 للنص الصغير المهم |
| success text / surface | `#0F766E` / `#E6F8F5` | معتمد/ناجح/محفوظ |
| warning text / surface | `#B45309` / `#FEF3C7` | معلق/تحذير |
| danger text / surface | `#BE123C` / `#FFE4E6` | معاد/خطأ/إجراء خطر |
| info text / surface | `#0369A1` / `#E0F2FE` | منتهية/معلومة |
| accent-secondary | `#6366F1` مع `#EEF2FF` | أيقونات إحصاء محدودة، لا primary بديل |

**خريطة شارات الدورة:** Proposed أصفر بالنص «مقترحة — بانتظار الجودة»؛ Returned أحمر «معادة للتعديل»؛ Rejected رمادي غامق «مرفوضة»؛ Approved أخضر «معتمدة» وشارة «لم تبدأ/جارية» حسب بيانات الخادم؛ Finished أزرق «منتهية»؛ Cancelled أسود «ملغية بعد الاعتماد». التعطيل والأرشفة شارتان مستقلتان لا تستبدلان status. لا تستخدم لون `bs-rejected` الموجود لإظهار Rejected بالأحمر خلاف الحزمة.

لا تعتمد اللون وحده: نص حالة وأيقونة عند الحاجة. تحقّق من التباين؛ يجوز تغميق foreground داخل العائلة نفسها لتحقيق 4.5:1، مع توثيق القيمة، دون تغيير الهوية.

### 4.2 الخط والأبعاد

- Tajawal المحلي الموجود في `wwwroot/fonts/tajawal` و`css/tajawal.css`، fallback Arial/sans-serif. لا Google Fonts/CDN.
- حجم أساس `15px`، line-height `1.65`. العنوان الرئيسي داخل المحتوى `25px/700`، عنوان الهيدر `19px`، عنوان البطاقة `15–16px/700`، النص `14–15px`، الجدول `13.5–14px`، المساعدة `12–13px`، أرقام KPI `29px`.
- عرض sidebar سطح المكتب `265px`، header أدنى `70px`، مساحة المحتوى max-width `1440px` داخل المساحة المتبقية ومتمركزة، padding `32px`.
- radii: عنصر/حقل/زر `8px`، بطاقة/تبويب مجمع `12px`، عنصر كبير استثنائي `16px`؛ لا حواف مبالغ فيها.
- spacing scale: `4,8,12,16,20,24,32` px. gap شبكات `16px` أو `24px`، داخل البطاقة `22px`، header البطاقة `18px 22px`.
- خلفية بيضاء وحدود خفيفة؛ بطاقات البيانات والإحصاء **دون ظل** كما experience. ظل صغير فقط dropdown/modal/العنصر العائم، لا قفز البطاقات عند hover.
- زر أساسي واحد في منطقة الإجراءات؛ أحمر للخطر، outline للثانوي. لا primary متعدد لكل صف.
- ارتفاع هدف التفاعل `44px` على الهاتف، وحقول `42–44px`، textarea لا يقل عن 3 أسطر. أيقونة مزخرفة 18–22px، مع `aria-hidden=true` إن لها نص.
- الأيقونات Tabler المحلية الموجودة؛ لا مزج emoji وLucide وFontAwesome. رموز الأدوار من البروتوتايب.

### 4.3 breakpoints والإيقاع

- عند `≥992px`: sidebar ثابت عند inline-start؛ main له margin-inline-start=265px؛ header صف واحد ما دام المحتوى يتسع.
- عند `<992px`: sidebar drawer بعرض `min(285px,85vw)` + backdrop؛ main بلا margin؛ header min-height70 مع wrap. هذا تكييف صريح لتحاشي ازدحام الـtablet في التطبيق، مع الحفاظ على نمط البروتوتايب.
- عند `≤768px`: page padding `22px 16px`، بطاقة padding16، عنوان header16، نماذج عمود واحد، شبكة بطاقات وفق المساحة. عند 320px لا يوجد overflow عام.
- عند `≥1200px`: KPI أربعة أعمدة إن العدد يسمح؛ 576–1199 عمودان؛ أقل من576 عمود واحد. لا تفرض أربع بطاقات إذا الخادم يوفر أكثر/أقل؛ حافظ على كل المؤشرات المطلوبة.
- نماذج ثنائية اللغة: حقل عربي وإنجليزي بعمودين على desktop، متتابعين على mobile؛ textarea الطويل بعرض كامل أو زوج واضح.
- الجداول الإدارية تبقى جداول داخل `.table-responsive`؛ لا تحوّلها إلى بطاقات تخفي أعمدة/إجراءات. بطاقات الدورات والدليل تستخدم grid مرن.
- احترام `prefers-reduced-motion`، بدون animations دخول وقوائم نابضة؛ transitions لا تزيد عن 200ms للأجزاء الضرورية.

## 5. المرحلة F1 — الأصول وCSS دون ازدواجية

1. احتفظ بـBootstrap 5 الحالي واحداً فقط؛ العربية `bootstrap.rtl.min.css` والإنجليزية `bootstrap.min.css`، لا تحميلهما معاً.
2. أنشئ `wwwroot/css/prototype.css` كمصدر منظم للقواعد البصرية المستخدمة من `styles.css` ثم `experience.css`. أبقِ إشارة مصدر لكل مجموعة، واحذف من النسخة المنقولة فقط قواعد demo/QR حضور/مكونات غير مستخدمة. لا تعدّل الأصلين في `prototype/`.
3. عالج قواعد `.nav-item` و`.header` ونحوها بالـscoping حتى لا تتعارض مع Bootstrap داخل dialogs/tabs. لا تنقل reset عاماً يمس كل component بلا مراجعة.
4. اجعل `site.css` طبقة تكييف Bootstrap للهوية والمكوّنات المشتركة، و`rtl.css/ltr.css` لفروق الاتجاه الضرورية فقط؛ استخدم logical properties أساساً.
5. ترتيب CSS: Bootstrap → Tabler → Tajawal → CSS plugin المستخدم → prototype.css → site.css → ملف الاتجاه → CSS صفحة إن لزم. لا تعتمد app.min.css العام من Paces إن كان يكرر Bootstrap/الثيم.
6. أزل ربط `app.css` من layout بعد نقل قواعده اللازمة فقط. لا تترك app.css وprototype.css محمّلين معاً. لا تحذف الملف القديم قبل grep للتأكد أنه غير مستخدم في auth أو صفحة أخرى؛ وثّق استبداله.
7. افحص `_Layout.cshtml.css` و`ZuLms.Web.styles.css` الناتج: أصلح المصدر scoped إن كان يفرض قواعد scaffold قديمة، ولا تعدّل الملف generated.
8. اربط `--bs-primary` ومشتقاته وbutton focus مع navy؛ عناصر dialogs خارج `.page-content` تأخذ نفس الهوية، لا تبقى زرقاء Bootstrap.
9. ممنوع `!important` عشوائي أو `overflow-x:hidden` لإخفاء مشاكل؛ يُسمح فقط لقواعد hidden/حالات دقيقة موثقة. لا inline styles متكررة لكل بطاقة.
10. راجع URL في كل CSS/font/icon منقول والتراخيص المصاحبة. لا تنسخ كل assets غير المستعملة.

**بوابة F1:** الأصول بلا 404/CDN، الخط والأيقونات محليان، لا Bootstrap/handlers مزدوجة، عرض AR/EN على 390 و1440، shell مطابق للتوكنات.

## 6. المرحلة F2 — مكتبة العرض المشتركة

### 6.1 المصادر الدقيقة من Paces

الأسماء التالية موجودة في التيمبليت المرفق؛ افتح المثال وخذ **مقطع component** فقط، لا الصفحة كاملة:

| العنصر | الملف داخل `template/paces/` | المكوّن/تطبيقه |
|---|---|---|
| Grid وCards | `ui-grid.html`, `ui-cards.html` | `row`, `col-*`, `card`, `card-header`, `card-body` |
| أزرار/شارات/تنبيه | `ui-buttons.html`, `ui-badges.html`, `ui-alerts.html` | Bootstrap variants مع tokens الجامعة |
| Forms | `form-elements.html`, `form-layout.html`, `form-validation.html` | label/control/feedback/fieldset، لا حزمة validation جديدة |
| Select | `form-select.html` | الشكل؛ القوائم القصيرة native select، بحث الأعضاء بالخدمة الحالية |
| جداول | `tables-static.html`, `tables-datatables-basic.html` | table-responsive + رأس/صفوف؛ **لا تضف DataTables** لمجرد الشكل |
| Pagination | `ui-pagination.html` | pagination مرتبطة بـtotal/page من الخادم |
| Tabs/Collapse | `ui-tabs.html`, `ui-accordions.html`, `ui-collapse.html` | التبويبات والتفاصيل الثانوية |
| Modal/Drawer | `ui-modals.html`, `ui-offcanvas.html` | header/body/footer وbackdrop وfocus |
| Dropdown/Hint | `ui-dropdowns.html`, `ui-tooltips.html`, `ui-popovers.html` | سلوك Bootstrap مع محتوى التلميح الحي |
| Toast/Loading | `ui-notifications.html`, `ui-spinners.html`, `ui-placeholders.html` | حالات العملية وskeleton |
| Progress | `ui-progress.html` | التقدم البصري دون حساب عمل جديد |
| رفع ملفات | `form-fileuploads.html` | الشكل فقط فوق input file الحالي؛ لا Dropzone/plugin جديد |
| Date/time | `form-pickers.html` | daterangepicker + Moment المحليان الموجودان فعلياً |
| Charts | `charts-chartjs-bar.html`, `charts-chartjs-line.html`, `charts-chartjs-other.html` | Chart.js محلي، بيانات API الفعلية |
| Permissions | `apps-users-permissions.html`, `apps-users-role-details.html`, `apps-users-roles.html` | شكل المصفوفة فقط، القواعد من الحزمة |
| Profile/Settings | `apps-users-profile.html`, `apps-users-account-settings.html` | أجزاء غير موجودة في prototype بالهوية نفسها |
| Auth/Errors | `auth-card-sign-in.html`, `auth-card-reset-pass.html`, `auth-card-new-pass.html`, `error-403.html`, `error-404.html`, `error-500.html` | قالب بسيط بهوية الجامعة، لا تسجيل ذاتي/تواصل اجتماعي |

التيمبليت يذكر flatpickr في بعض markup، لكن التطبيق الحالي يستخدم daterangepicker وأصوله موجودة؛ لا تستنتج توفر مكتبة أو تضفها من اسم data-provider. احتفظ بالـplugin الحالي وكيّف عرضه.

### 6.2 الأجزاء المشتركة المحددة

أضف/استخدم داخل `Pages/Shared/`: `_Topbar`, `_SidebarAdmin`, `_SidebarQuality`, `_SidebarDean`, `_SidebarInstructor`, `_SidebarMember`, `_LanguageToggle`, `_NotificationBell`, `_ConfirmModal`, `_FieldHint`, `_PagedTable`, `_EmptyState`, `_StatusBadge` **كمقاطع عرض فقط**، مع `_RoleSwitcher` و`_DateTimePicker` الحاليين. يمكن تمرير بيانات العرض بـViewData أو نماذج موجودة؛ لا تنشئ خدمة أو PageModel جديداً لتجريد بسيط.

إذا كان جزء ديناميكياً يُبنى بـJS مثل الصفوف/التوست: اجعل renderer مشتركاً بدلاً من partial غير مستخدم. لا تضف ملفات شكلية لا تستعملها الصفحات.

### 6.3 عقود الشكل والتفاعل

**بطاقة بيانات:** رأس أبيض فيه icon+عنوان+إجراء ثانوي، body أبيض، border1px/r12. الجدول في body بلا padding إضافي، نموذج body22px. لا card داخل card لمجرد صنع إطار.

**جدول:** caption/عنوان واضح، toolbar بحث+فلاتر فوقه، صفوف 14×18 padding، الحالة شارة، أعمدة الأرقام LTR، الإجراءات بعناوين فعلية وtooltip للأيقونات. footer إجمالي/نطاق النتائج وpagination. Empty/Error/Loading داخل عرض الجدول كامل الأعمدة، لا صفّ نجاح فارغ عند فشل GET.

**نموذج:** label دائم، required علامة واضحة، help ثم feedback تحت الحقل؛ `name` يطابق fieldErrors. لا تعتمد placeholder وحده. الأسماء الحالية المستخدمة للـpayload لا تتغير. عند إضافة name لحقل كان بلا name اربطه بالمفتاح الفعلي من عقد الاستجابة، لا تخمّن snake_case مقابل camelCase.

**Modal:** رأس عنوان ومغلق، body رسالة وحقول، footer إجراء رئيسي وثانوي؛ عرض عادي480–560px، واسع720–800px، mobile=`calc(100% - 32px)`، max-height=`calc(100dvh - 32px)` مع scroll داخلي. زر الخطر أحمر. إذا يوجد `<dialog>` يستعمله JS بـ`showModal/close` فاحتفظ بالعنصر وتلك الدوال؛ استعمل بنية/تنسيق Paces داخله ولا تحوله إلى div بلا تحديث مدروس لكل مستهلك.

**Confirm contract:** `confirm` يعيد Promise<boolean>، `prompt` يعيد Promise<string|null>، `show` يعرض قراءة فقط. Escape/Cancel => false/null. لا تعامل `''` قبولاً للسبب المطلوب؛ validation قبل الحفظ. يمكن إضافة options اختيارية للعنوان/الخطورة/الإلزام/label مع إبقاء النداءات القديمة صالحة، ثم تحديث كل callsite يحتاج سبباً إلزامياً. لا تستخدم `window.alert/confirm/prompt`.

**Toast:** احتفظ بـ`ZuLms.toast(message,kind)`، واستخدم مضيفاً واحداً هو `#zu-toasts` تزامناً مع api.js الحالي؛ حدّث layout ليتوافق واحذف ازدواج host غير المستخدم. نجاح/status وerror/alert، close له نص مترجم. لا تغيّر request/refresh/CSRF أثناء تحسين toast.

**Empty:** icon هادئ + عنوان/وصف + إجراء مسموح فقط إن للمستخدم صلاحية؛ لا زر إنشاء عند عضو ليس له الإنشاء. نص teaching حرفي: «لا توجد دورات مسندة لك كمحاضر حالياً، ستظهر هنا تلقائياً فور اعتماد تكليفك من الكلية والجودة».

**Loading/Error:** loading منفصل عن empty؛ تعطيل submit أثناء طلبه؛ عند الفشل يبقى مدخل المستخدم ويعود الزر، ويظهر error/fieldErrors. القراءة لها retry، والكتابة الغامضة لا يعاد إرسالها تلقائياً. لا تنشئ نجاحاً تفاؤلياً لإجراء رسمي.

**Tabs:** اجعل tablist وتبويبات aria-selected/controls وtabpanel، keyboard مناسب. إظهار panel لا يمنح صلاحية لبياناته. لا تغير routes الحالية لتقليد show() الخاص بالديمو.

**Charts:** حجم ثابت للحاوية desktop240–280px/mobile220px، ألوان من tokens، إتلاف chart قبل إنشاء بديل، labels عربية/إنجليزية، عنوان النطاق والدورة والفترة، جدول/ملخص بديل. لا رسوم عشوائية ولا جمع إحصاء من صفحة نتائج مجزأة.

## 7. التواريخ والترجمة والـJavaScript — قواعد تمنع الكسر

### 7.1 التاريخ والوقت

1. اجرد كل `type=date/time/datetime-local` في cshtml وفي strings إنشاء DOM داخل JS. استبدل العرض بـtext + المكوّن الموجود.
2. حلّل `_DateTimePicker.cshtml`, `DateTimePickerModel.cs`, `pickers.js` وكل serializer قبل التغيير. لا تغيّر الـModel C# في هذه المهمة؛ ضع خيارات عرض data attributes حيث يكفي.
3. ترتيب scripts مرة واحدة: jQuery → Moment → daterangepicker → `pickers.js` → JS الصفحة. تأكد CSS plugin محمّل. الحوارات الديناميكية تستدعي `ZuLmsPickers.init(root)` بعد الإضافة/الفتح. init idempotent لا يكرر plugin.
4. date-only يبقى `yyyy-MM-dd`؛ datetime/time حسب عقد endpoint الحالي. افصل قيمة العرض عن قيمة الإرسال عند اللزوم؛ لا تستخدم `new Date('YYYY-MM-DD')` لعرض يوم فتغيّره المنطقة الزمنية.
5. الحزمة تقرر عرض عمّان وإرسال صيغة محلية للخادم، بينما بعض الكود الحالي يحول موعد التسجيل إلى UTC. اقرأ نوع DTO و`AmmanDateTimeOffsetConverter` **للقراءة** وثبّت عقده في contracts. إن كان endpoint يقبل local المعتمد فاستعمله؛ إن كان يقبل instant فقط يمكن محول عرض عمّان يحفظ instant نفسه. إن تعذر تحقيق متطلب الحزمة بلا تغيير الخادم فسجّل BLOCKED-CONTRACT ولا تغيّر تفسير التاريخ خفيةً.
6. لا تملأ تاريخاً اختيارياً تلقائياً بمجرد فتح picker، ووفّر مسحاً إلى null. لا تحصر الدقائق بمضاعفات5 إذا العقد يقبل دقيقة؛ اضبط timePickerIncrement للعرض كي لا يفقد دقة بيانات موجودة.
7. `pickers.js` الحالي لا يوفر وقتاً منفرداً واضحاً ويحتوي clear يحتاج مراجعة. للوقت المنفرد استخدم جزء time picker من المكوّن الموجود أو واجهة اختيار ساعات/دقائق بنمط form-select من Paces مرتبطة بـHH:mm؛ دون input time أصلي أو إضافة plugin. اختبر 00:00 و23:59 وفترة عابرة لمنتصف الليل حسب قواعد الخادم.
8. داخل `<dialog>` تحقق أن قائمة picker في top layer قابلة للنقر؛ اضبط parentEl إلى حاوية مناسبة في الحوار وفق دعم plugin، لا z-index عشوائياً على body. جرّب RTL/LTR والهاتف.

### 7.2 الترجمة والاتجاه

1. كل عنوان/زر/label/placeholder/tooltip/aria-label/حالة فارغة/نص JS إلى `SharedResource.ar/en.resx`؛ استخدم مفاتيح ثابتة بنمط Area.Page.Element ومتسقة مع الموارد الحالية. لا تغيّر مفتاحاً يستخدمه backend؛ أضف مفاتيح عرض فقط.
2. استخدم `i18n.js` الحالي بعد فهم آليته. مرّر strings إلى JS عبر data attributes مرمّزة من Razor أو الآلية الحالية؛ لا inline script ولا `Html.Raw` لتصدير JSON.
3. `Profile.cshtml`: انقل كتلته إلى `wwwroot/js/profile.js`، مرر `data-user-id`، وحافظ على `/api/v1/profiles/{userId}` وشكل الرد. استخدم textContent للمدخلات.
4. استخرج بقية `<script>` inline و`onclick=` في كل الصفحة/renderer. الأحداث في JS خارجي فقط، CSP لا يتغير.
5. استخدم `margin-inline`, `padding-inline`, `inset-inline-start`, `border-inline-start`, `text-align:start`, `ms/me`؛ sidebar في اليمين بالعربية واليسار بالإنجليزية. Drawer animation يعكس الاتجاه صراحة.
6. code/email/reference/login URLs تستخدم `dir=ltr` وbdi عند الحاجة. الأرقام اللاتينية والتواريخ المعروضة بتوقيت عمّان؛ أسماء المحتوى *_en لها fallback عربي فقط عند غياب ترجمتها.
7. لا تترجم اسم شهادة مهنية أو جهة عالمية قسراً؛ هذان اسمان أصليان كما `12`.

### 7.3 ربط الأحداث

- احتفظ بالـIDs القائمة مثل `course-details-page`, `course-details-actions`, `course-details-edit-form`, `d-form` إن وُجد فعلياً؛ لا تفترض ID من هذه الأمثلة: اقرأ الجرد. النموذج المباشر الحالي اسمه **`direct-form`** وحقوله `d-*`.
- عند نقل العنصر بين تبويبات يبقى مرة واحدة في DOM. لا تنسخ form للـmobile وتخلق duplicate IDs أو submit مزدوجاً.
- احتفظ بالevent delegation أو أعد الربط بعد render بطريقة واحدة. لا تهيئة عند كل فتح دون إزالة/تحقق.
- لا تعِد كتابة `api.js` كعميل جديد؛ تعديل مظهر toast/feedback فقط عند الحاجة وباختبار regression مستقل للـ401/CSRF.
- لا تعتمد قيمة زر disabled لحماية البيانات، ولا تكسر `<zu-if-permission>` أو الحالات القادمة من API.

## 8. المرحلة F3 — الـShell والتنقل العام

### الهيكل

`body → .app-layout → aside.sidebar + .main-content → header.header + main.page-content`؛ الشعار ثم بطاقة المستخدم ثم المبدّل/القائمة حسب السياق ثم الروابط المشتركة. Header: اسم الصفحة ووصف قصير، مبدّل السياق، اللغة، الجرس، حساب المستخدم والخروج. لا شريط جانبي كحلي كامل؛ المرجع sidebar أبيض بعلامة active كحلية خفيفة.

### التنفيذ

1. افصل `_Layout` إلى مقاطع مشتركة دون تغيير منطق الحماية. إبقاء anti-forgery meta وscript order وRenderSection.
2. دور العرض يأتي من السياق المعتمد الحالي في `_RoleSwitcher`/`role-switcher.js` و`Security/ContextEndpoints.cs` قراءةً؛ لا أعلى صلاحية فقط. إن لم يكن السياق متاحاً للرندر استخدم بيانات المبدّل الموجودة بعد تحميلها؛ لا تنشئ endpoint جديداً.
3. القائمة = صفحات السياق المحدد **∩ الصلاحيات الفعلية**. روابط المشتركة بحسب permission، والمدرب الخارجي بلا Member/Shared/password self-service.
4. المجموعات بالترتيب التالي، مع إخفاء المجموعة الفارغة:

| السياق | ترتيب المجموعات والعناصر |
|---|---|
| Admin | الرئيسية: dashboard؛ الإدارة: accounts/credentials، users/external requests، roles إن منفذة؛ الدورات: courses، إنشاء مباشر admin، احتياجات قراءة/إدارة؛ الجودة المشتركة: المقترحات/الإلغاءات/النموذج/النتائج/الشهادات المهنية وفق الصلاحيات؛ النظام: settings، password-policy، hints، centers، sync، certificate-template؛ الرقابة: vault، audit، live-feed، stats إن منفذة |
| Quality | dashboard؛ متابعة: proposals، cancellations، needs؛ برامج: courses، إنشاء مباشر/تاريخي، tracks، external instructors إن منفذة؛ قياس: eval-form، evaluations؛ اعتماد: certificates، pro-certs، pro-cert-stats؛ vault؛ الدورات المُدارة عبر مساحة instructor |
| Dean | dashboard؛ courses؛ propose؛ needs؛ members؛ stats إن منفذة؛ رابط سياق member والملف |
| Instructor | courses؛ بعد اختيار الدورة تنقل محلي لتفاصيلها/محاضراتها/حضورها/مشاركيها/امتحانها/أثرها/نتائجها حسب المتاح؛ العودة للدورات؛ سياق member للداخلي فقط |
| Member | dashboard؛ available؛ enrolled؛ enter-code؛ impact؛ certificates؛ pro-certs؛ teaching؛ directory/profile حسب الصلاحيات |

5. استخدم المسارات **الفعلية** في الروابط العاملة؛ مثال available حالياً `/courses/available` وenrolled `/member/courses`. وثّق اختلاف الحزمة ولا تنشئ alias من تلقاء نفسك.
6. لا تعرض رابطاً لمسار غير منفذ، لكن اتركه GAP في مصفوفة التغطية فلا يُعد محذوفاً من المطلوب. غياب roles/stats/غيرهما لا يصبح «تم» لأن القائمة أخفته.
7. active item = أدق مسار مطابق، لا تجعل `/admin/courses` و`/admin/courses/new` نشطين معاً. عناصر مجموعة المساحة تبقى واضحة على deep link.
8. footer لا يعرض تغيير كلمة المرور للخارجي. directory/profile لا يفترضان أن Local أدمن له ملف موظف.
9. notifications: شارة عدد لا نقطة ثابتة، قائمة min-width مرنة `min(360px,calc(100vw - 32px))`، آخر20 وread-all، تاريخ ورسالة ورابط المصدر، حالة فارغة/loading/error.
10. Drawer: aria-controls/expanded، فتح/إغلاق بالزر والخلفية وEscape، إعادة focus، منع تمرير الخلفية أثناء الفتح، إغلاق عند التنقل، عكس الحركة في LTR.

## 9. أنماط الصفحات المعتمدة

هذه ستة أنماط تركيبية من المرجع؛ تستخدم لكل الصفحات، دون اجتهاد تصميم جديد:

- **P1 لوحة:** عنوان ووصف → KPIs → صف رسم كبير8/12 وصغير4/12 إن البيانات متاحة → جدول نشاط/قائمة مهام. مصدر `admin/quality/dean/member#dashboard`.
- **P2 إدارة قائمة:** عنوان+إجراء رئيسي → بطاقة filters → بطاقة table → footer pagination. للأدوات الإدارية والطلبات.
- **P3 نموذج:** عنوان+وصف → أقسام cards أو card مقسّمة بفواصل وعناوين → حقول ثنائية الأعمدة → actions واضحة أسفل. مصدر `dean#newcourse` + Paces forms. ليس Wizard جديداً.
- **P4 مساحة دورة:** breadcrumb/عودة → عنوان ورقم وحالة → ملخص مؤشرات → تبويبات مرئية حسب الصلاحيات → panel داخل card → إجراءات الخطر في مجموعة منفصلة. مصدر `instructor#coursedetail/attendance` + ui-tabs.
- **P5 ملف/بطاقات:** رأس ملف/صورة/انتماء وعدادات → نبذة وخبرات → شهادات المنصة → المهنية، مع action التحرير للمالك فقط. مصدر profiles في prototype وmembers-directory.
- **P6 مصادقة/رسالة عامة:** شعار+عنوان مختصر → card max-width480 → الحقول/الحالة → روابط ضرورية فقط. مصدر auth-card*؛ لا portal demo أو اختيار دور وهمي.

لكل pattern طبّق حالات: loading، loaded، empty، error، validation، submitting، success، permission-limited، conflict حيث ينطبق. لا تُسقط حالة لأنها غير موجودة في لقطة البروتوتايب.

## 10. خطة التنفيذ صفحة بصفحة

مسارات Razor أدناه نسبة إلى `src/ZuLms.Web`، والـJS موجود في `wwwroot/js`. كل صف عمل إلزامي وله صور قبل/بعد AR/EN desktop/mobile. **خريطة كل المسارات والـJS التفصيلية موجودة أيضاً في §4 من ملف الاختبار المجاور.**

### F4 — العامة والحساب

| الصفحة/الملفات | النمط والمرجع | التكوين المحدد |
|---|---|---|
| `Areas/Public/Pages/Login.cshtml` + auth.js/auth.css | P6، auth-card-sign-in؛ جزء login إن موجود بالprototype | شعار، اسم المنصة، رقم الدخول، كلمة المرور، تذكرني، زر دخول بعرض الحقل، forgot، لغة؛ لا signup/social/اختيار دور |
| ForgotPassword | P6، auth-card-reset-pass | رقم وظيفي+بريد جامعي، نص النتيجة العام الحرفي، رجوع للدخول؛ لا تغيير العقد إلى reset token |
| ChangePassword | P6، auth-card-new-pass | جديدة+تأكيد وسياسة/تلميحات؛ لا حقل الحالية للجلسة المحدودة |
| `Pages/Account/Password.cshtml` | P3 ضمن shell | الحالية+الجديدة+التأكيد، نتيجة وإعادة دخول عند العقد؛ موظف فقط |
| Verify | P6، card+badge | البحث/المرجع حسب الشكل الحالي، سارية ببيانات 06 فقط؛ ملغاة نص وتاريخ؛ مخفية/غير موجودة بلا تفاصيل |
| AccessDenied/Error | P6، error-403/error-500 | رسالة مترجمة، رابط عودة/دخول مناسب، correlation إن موجود دون stack trace |
| Index/Privacy | لا تضف محتوى | افحص الرابط/الغلاف القائم؛ لا تحوّل Index إلى demo hub؛ وثّق scaffold غير المطلوب، ولا تغير server redirect |

### F5 — الدورات المشتركة والعميد

| الملف/المسار | النمط | ترتيب المحتوى والإجراءات |
|---|---|---|
| `Shared/_CourseDirectory.cshtml` + course-directory.js، جميع Courses.cshtml | P2 للإدارة، بطاقات prototype#courses/available للأعضاء حيث لا تفقد حقولاً | بحث رقم/عنوان/محاضر؛ الحالة/الكلية/المحور/النمط/التاريخ/الأرشفة/التفعيل بحسب العقد؛ table أو cards موحدة؛ لا تطبع زر غير مخول |
| `Courses/Available.cshtml` | بطاقات member#available | شريط بحث/فلترة، بطاقة عنوان ومحور ومحاضر وموعد ونمط وسعة إن متاحة؛ «سجّلني»؛ حالة مسجل/مغلق/ممتلئ لا زر ميت |
| `Dean/Propose.cshtml` + dean-proposals.js | P3، dean#newcourse | أقسام: بيانات الدورة، الأهداف والفئة، الجدول/الجمهور، المحاضر، المرفقات؛ لا أسماء مشاركين ولا محاضر كلية أخرى |
| `Shared/_DirectCourseForm.cshtml` + direct-course.js | P3، نفس بصريات المقترح | عنوان «إنشاء دورة مباشرة»، audience ثلاثي، أربعة أنواع محاضر؛ نهاية التقييم؛ historical حسب permission؛ زر اعتماد وليس اقتراح |
| `Courses/Details.cshtml` + course-details.js | P4 | رأس رقم+عنوان+status، toolbar أعمال، تبويبات البيانات والمحاضرات/التقدم/التقييم/الأثر/الملفات/سجل الحالة الموجودة؛ لا نسخ عناصر أو حذف sections الحالية |
| `Courses/Participants.cshtml` | P2، instructor#certificates + tables | بحث أعضاء وإضافة متعددة لمن يملكها، table مشاركين وسعة، إخراج بسبب، تحذير أثر الشهادة |
| `Courses/CancelRequest.cshtml` | P3، dean cancelModal | ملخص الدورة، سبب وكتاب، submit أحمر، إلغاء؛ تطابق شروط الحزمة والإعداد |
| `Dean/Members.cshtml` | P5 قائمة، dean#members + members-directory | بحث ضمن كليته، بطاقات اسم/قسم/خبرات/رابط الملف؛ لا تحكم إداري بالحساب |
| `Dean/Needs.cshtml` | P2 + P3، quality#needs/Paces | قائمته بحالة وملاحظات، نموذج عنوان/وصف/محور/عدد/وقت/كتاب/محاضر مطلوب؛ سجل مراجعات، تعديل المعاد وإرسال |

تفاصيل course-details dialogs: تعديل الحقول بترتيب النموذج، force-status عنوان وتحذير وسبب وتأكيد، threshold فارغ=العامة، evaluation-period منتقي، instructor فروع مطابقة للـenum الحالي، deactivate يعرض N والخيارين الحرفيين. لا تستبدل قيمة الإرسال `Hide` الحالية بـ`Disable` الواردة وصفياً بالحزمة إلا إذا العقد نفسه يطلب ذلك؛ سجّل الفرق ولا تكسر API.

### F6 — المحاضر والعضو

| الصفحة | النمط/المرجع | التعليمات المحددة |
|---|---|---|
| Instructor/Courses | P2/بطاقات instructor#courses | عرض الدورات المسندة، حالة ونمط وعدد مشاركين إن متاح، زر دخول مساحة الدورة |
| Instructor/Lectures + course-lectures.js | P4، instructor#attendance | رأس الدورة ثابت؛ panels محاضرات وحضور وامتحان الموجودة، إضافة جلسة modal P3؛ التاريخ/المكان/Teams حسب النمط |
| شاشة كود الحضور | instructor projectorModal بعد حذف QR | رقم LTR بحجم54 desktop و40 mobile، عنوان الدورة والمحاضرة، timer، حالة منتهٍ/نشط، إعادة التفعيل وفق الخادم؛ لا QR |
| كشف الحضور | instructor#attendance | أسماء بالصفوف ومحاضرات بالأعمدة إن العقد يدعم الكشف الكامل؛ إن الحالي كشف جلسة واحدة، selector جلسة واضح وجدولها دون اختلاق تجميع؛ scroll داخلي |
| الامتحان ضمن الصفحة الحالية | instructor#exams مع قواعد 06 | إنشاء باسم، جدول حالة غير مرصود/ناجح/راسب؛ radio/select صريح، لا درجات ولا علامة unchecked=راسب |
| Instructor/Progress | instructor#certificates | table حضر/من، النسبة، الامتحان، التقييم، الأثر، الاستحقاق وأسبابه، مرجع الشهادة؛ أيقونة+نص |
| Impact/Review | impact#review | اختيار دورة واضح من الطلبات الموجهة للجودة/العميد/الأدمن، طلبات ومرفقات، اعتماد/إعادة بملاحظات؛ العنوان يتبع الدورة المختارة |
| Member/Courses + تفاصيلها الحالية | member#enrolled | قائمة دورات وبطاقة تقدّم أربعة متطلبات من الخادم؛ محاضرات لكل منها مكان/Teams/مواد/حضور وكود مستقل |
| Member/EnterCode | member#entercode | بطاقة لكل محاضرة جارية/اليوم مع اسم الدورة، input رقمي نصي inputmode numeric بطول من الخادم؛ حافظ على الأصفار البادئة |
| Member/Impact | member#impact | الحالة الحالية، ملاحظات الإعادة بالأحمر، نوع الأثر وملف وحالة الرفع، history إن متاح؛ لا زر رفع بعد Approved |
| Member/Certificates | member#certs | cert-card عنوان/فترة/ساعات/مرجع/تحقق وتنزيل لصاحبها، السارية فقط؛ QR فقط إن بياناته متاحة فعلاً |
| Member/Teaching | member#myteaching | الرسالة الثابتة عند الفراغ ثم رابط قائمة التكليف؛ لا إنشاء تكليف من الواجهة |

### F7 — الجودة والمهنية والملف

| الصفحة | النمط/المرجع | التعليمات المحددة |
|---|---|---|
| Quality/Proposals | P2، quality#approvals/rejectModal | بطاقة/جدول كلية وعميد وعنوان وتاريخ ورقم نسخة؛ تفاصيل+مرفقات+نسخ؛ اعتماد modal مفصل، إعادة/رفض بسبب |
| Quality/Cancellations | P2، quality#cancellations | pending/status، الدورة والعميد وسبب/كتاب، قرار واضح؛ لا سحب أعمال الدورة قبل الموافقة |
| Quality/Tracks | P2 + modal، quality#tracks | اسم عربي/إنجليزي ووصف/أهداف وترتيب، أرشفة حسب العقد، حالة واضحة |
| Quality/Needs وAdmin/Needs | P2، quality/admin#needs | عرض اسم العميد والكلية والكتاب والمراجعات؛ Q قرارات، A أدواته المتاحة؛ لا زر رفع جديد |
| Quality/EvalForm | P3، ui-cards/form-elements/ui-buttons | رأس النسخة وحالتها؛ سؤال card مع خيارات radio-definition، إضافة/حذف/ترتيب بأزرار صريحة، خيار نفس السؤال، حفظ مسودة ثم نشر؛ لا drag-only |
| Quality/Evaluations | P2+charts، quality#evals | filters دورة/كلية/محور/فترة/نسخة؛ بطاقة لكل سؤال/مجموعة مقارنة؛ عنوان النسخة والخيارات وأعداد ونسب؛ لا خلط نتائج |
| Quality/Certificates | P2، quality#certs | فلاتر حالة/دورة/بحث، رقم وصاحب/دورة وتاريخ وحالة، issue-pending؛ أفعال بحسب صلاحياتها |
| Member/ProCerts | P5 قائمة + P3 modal، cert-card/Paces forms | كل الحالات، إضافة، تفاصيل، ملاحظات رفض، تعديل وإعادة إرسال للمرفوضة وسحب؛ جميع حقول12 |
| Quality/ProCerts | P2، ui-tabs وتنسيق quality approvals | تبويب pending/all؛ filters، تفاصيل الحقول والملف ورابط تحقق، قبول/رفض/تصحيح/تعطيل بحسب permission، إدارة التصنيفات إن موجودة |
| Quality/ProCertStats | P1 + P2، charts-chartjs | totals، الأكثر عامة، الأكثر بكل كلية، byFaculty/bar، category/doughnut، years/line، issuers/table؛ CSV فقط هنا والسجل |
| Shared/Directory | P5، members-directory.html | toolbar اسم/كلية/قسم/خبرة، grid بطاقات avatar+اسم+انتماء+خبرات+فتح ملف، pagination |
| Shared/Profile | P5، prototype أقسام profile + apps-users-profile | رأس صورة/اسم/انتماء/مناصب/عدد مهني، نبذة وخبرات وتواصل، شهادات منصة ثم مهنية؛ تحرير المالك عبر APIs القائمة فقط |

تفاصيل professional form: الاسم الأصلي والجهة مطلوبان ≤300، العربي اختياري، category select قصير، code أو verify URL أحدهما على الأقل، إصدار ليس مستقبلياً، انتهاء فارغ=لا تنتهي، ملف PDF/PPTX. رابط خارجي `target=_blank rel="noopener noreferrer nofollow"`؛ زر الملف لا يظهر لمشاهد البطاقة العادي. لا تعرض Pending/Rejected في الملف المشترك.

### F8 — إدارة النظام والمستودع والرقابة

| الصفحة | النمط/المرجع | التعليمات المحددة |
|---|---|---|
| Admin/Credentials | P2، admin#credentials | رأس إجراءات محدودة، filters الإرسال والحالة، table بأعمدة03؛ actions تجمع بحسب المهمة، كل حوار بمعرّف حساب واضح |
| حوار الأدوار/الصلاحيات الحالي | P2 واسع، apps-users-permissions | بيانات المستخدم ثم roles بنطاق Dean ثم permissions grouped، ثلاثي Inherit/Allow/Deny، مصادر الأدوار ونتيجة فعلية ومعاينة إن API يدعم؛ لا checkbox ثنائي |
| Admin/Users | P2، admin#users/addExtModal | الوظيفة الحالية طلبات الخارجيين؛ حالة واسم وجهة وبريد ودورة؛ إنشاء/رفض؛ لا تغيير مسار الحسابات من دون قرار |
| Admin/Settings | P3 مجموعات، admin#settings | sections auth/attendance/files/certificates/email/sync/directory/ui، حقل مناسب للنوع، وصف وتلميح وحدود؛ لا raw JSON بديل لكل الإعدادات |
| Admin/PasswordPolicy | P3 + P2 | card سياسة الطول/خيارات، card بحث/إضافة/إزالة الحظر، feedback واضح؛ لا كشف أسرار |
| Admin/Hints | P2 + modal | بحث context/key، عربي/إنجليزي، enabled/active، معاينة النص؛ لا Markdown/HTML غير مأمون |
| Admin/Centers | P2 | بحث ومصدر الاسم قراءة فقط، is_faculty وshort_code وتفعيل، عميد مقترح قراءة |
| Admin/Sync | P2 متعددة cards | شريط آخر تشغيل+زر تحديث؛ جهات؛ تشغيلات؛ مراجعات وفلاتر؛ progress أثناء العمل؛ لا progress وهمي بنسبة مصطنعة |
| Admin/CertificateTemplate | P3، form-fileuploads/cards | نسخة حالية/ملف PDF/layout JSON وnotes إن موجودة؛ upload/preview؛ ليس محرر إحداثيات بصرياً |
| Vault/Index | P2، admin#docsvault | filters، نوع/عنوان/دورة/كلية/تاريخ/رافع، preview/download، manual upload modal، disable للأدمن |
| Admin/Audit | P2، admin#auditlog | فلاتر كاملة، table تاريخ/حساب/فئة/فعل/كيان/نتيجة، modal before/after بتمييز نصي ولوني، CSV مطابق للفلتر |
| Admin/LiveFeed | P2 قائمة أحداث، admin#auditlog | indicator حي، pause/resume، فلتر، توقيت وفاعل وفعل ورابط؛ لا تكرار interval عند التنقل |

### F9 — لوحات الأدوار والإحصاءات

1. Admin/Dashboard: KPIs المعتمدة، آخر10 أحداث، مؤشر المزامنة والطلبات. مرجع admin#dashboard، لكن لا تنقل trends لا يقدمها الخادم.
2. Quality/Dashboard: قوائم معلقة حسب الأنواع، دورات حسب الكلية/المحور وملخص المهنية إن API يدعم. مرجع quality#dashboard.
3. Dean/Dashboard: كليته فقط، حالات المقترحات ومشاركون وشهادات؛ مرجع dean#dashboard.
4. Member/Dashboard: دوراتي/حضوري/شهاداتي وروابط العمل المتاح، لا رسوم جامعة كاملة.
5. Stats المطلوبة وغير المنفذة في routes: سجّل GAP. إن الإحصاء متاح ضمن صفحة حالية فحسّن عرضه هناك، ولا تدّعِ وجود route جديد.
6. UI لا يحسب counts من items الصفحة الأولى. مؤشر غير متوفر لا يُملأ بـ0 يوحي بالحقيقة؛ يظهر خطأ تحميل/حظر متطلب في الأدلة حسب السبب.

## 11. تنفيذ ما ليس موجوداً في البروتوتايب

القرار ثابت: **pattern من §9 + component من جدول §6 + نفس tokens**. أمثلة ملزمة:

- roles/permissions: P2 جدول داخل card، grouped rows، ثلاثي form-select/radio من Paces، بدون تصميم لوحة ألوان جديد.
- pro-certs: cert-card القائمة وP3 للإدخال وP2 للمراجعة؛ حالة وملاحظات كما بقية الطلبات.
- sync: P2 cards للتشغيل والجهات والمراجعات، status badges القياسية.
- eval editor: P3 متكرر لسؤال وخيارات، الأزرار الثانوية outline، النشر primary؛ لا محرر رسومي خارجي.
- أخطاء/تحميل/فراغ: ui-alerts/spinners/placeholders مع توكنات الجامعة، لا illustrations غريبة.

إذا وجدت حاجة خارج هذه الأنماط لا تصمم لغة جديدة: حدّد النمط الأقرب في `frontend-page-matrix` وركّب منه. إن كان الاختلاف وظيفياً لا بصرياً، يسجل كحظر لا كقرار تصميم.

## 12. ممنوعات نقل البروتوتايب والتيمبليت

1. لا «مركز العرض والديمو»، demo.js، config.js لتبديل themes، وضع داكن، showcase أو fake role switch.
2. لا QR حضور ولا Dual-QR. QR الشهادة للتحقق مختلف ومطلوب فقط حيث يدعمه العقد.
3. لا «بناء نموذج تقييم» للمحاضر، ولا درجات امتحان رقمية.
4. لا رفع احتياج للعضو أو Q/A، ولا أسماء مشاركين للعميد.
5. لا Force Purge أو حذف نهائي؛ العرض «تعطيل/استعادة/أرشفة» بالمعنى الصحيح. لا تغيّر HTTP DELETE الحالي الذي ينفذ soft-delete لمجرد اسمه.
6. لا ساعات حضور بدل عدد محاضرات، ولا 75%/5 دقائق/4 خانات ثابتة في العرض بدلاً من الإعدادات.
7. لا بيانات وأسماء وصور وأرقام وهمية في الواجهة الحية. Fixtures للاختبار فقط، والفراغ الحقيقي له EmptyState.
8. لا روابط CDN أو scripts inline أو new React/Vue/Tailwind/build system أو npm/NuGet غير معتمد.
9. لا تصدير عام PDF/Excel/CSV من التيمبليت؛ التصدير المسموح في الحزمة audit وprocert stats فقط.
10. لا نقل دوال `show()` ومحاكاة approvals/localStorage من prototype إلى الإنتاج؛ استعمل وظائف التطبيق الحالية.

## 13. وصفة العمل لكل صفحة — لا يجوز اختصارها إلى CSS عام

1. افتح المرجع البصري المحدد، الصفحة الحالية، Razor وJS والعقود الخاصة بها؛ دوّن حقولها وأفعالها كلها.
2. التقط before في AR/EN و1440/390 بحالة بيانات حقيقية للاختبار.
3. اختر pattern المحدد وأعد تنظيم wrappers/cards/sections فقط أولاً، مع إبقاء IDs/names/data attrs.
4. طبّق typography/spacing/status/buttons/table/forms من المكتبة المشتركة. أعد استعمال component، لا نسخ CSS جديداً.
5. أصلح الترجمة/ARIA/الاتجاه والتواريخ للمكوّنات التي تُعرض في الصفحة، بما فيها generated HTML والحوارات المخفية.
6. حدّث renderers بما يوافق نفس DOM contract؛ قارِن payload قبل/بعد لنفس الإجراء. الحقول ومعانيها ونسخة التزامن لا تتغير.
7. نفّذ كل فعل ظاهر: فتح/بحث/فلترة/صفحة تالية/إضافة/تحرير/حفظ/إلغاء/تنزيل؛ جرّب فشل validation وشبكة و409 وصلاحية حيث ينطبق.
8. افحص console/network وCSP، ثم refresh وتثبت من الحفظ الفعلي. لا تكتفِ بلقطة أولية ناجحة.
9. التقط after بنفس البيانات/الحجم، قارِن مع المرجع، وسجّل الاختلافات المقصودة مثل استبدال QR بكود والحقول الإضافية المعتمدة.
10. حدّث page-matrix والتغطية. لا تنتقل إلى دفعة صفحات جديدة مع عنصر مكسور في component مشترك تعتمد عليه.

## 14. الفحص بعد المراحل وحدود الإصلاح

من جذر `zu-lms`، نفّذ الأوامر منفردة بعد كل مرحلة طبقاً للحزمة، مع تجهيز قاعدة اختبار مستقلة حسب ملف الاختبار:

```powershell
dotnet build ZuLms.sln -c Release
dotnet test ZuLms.sln -c Release --no-build
```

افحص تركيب ملفات JS المعدلة بـ`node --check <file>`، وملفات resx كـXML مع منع duplicate keys ومساواة مفاتيح العرض العربية/الإنجليزية. بناء .NET لا يثبت عمل DOM أو picker؛ اختبار المتصفح إلزامي.

إذا فشل build/test قبل عملك وثّق baseline. إن فشل بسبب عملك أصلح السبب ضمن النطاق. إن احتاج إصلاح backend أوقف الجزء المتأثر وبلّغ، لا توسّع المهمة. لا تعدّل expected tests لإخفاء تغيير عقد.

### فحوص ثابتة قبل التسليم

- بحث في cshtml وJS عن `type="date"`, `type="time"`, `datetime-local` بما يشمل single quotes والإنشاء الديناميكي، وكل inline scripts/handlers؛ راجع النتائج يدوياً.
- بحث عن CDN/import URLs، dark/theme toggles، نص demo، Dual-QR، native dialogs browser calls، placeholders، duplicate IDs، selectors مفقودة.
- مقارنة CSS loaded تمنع تكرار Bootstrap/طبقة prototype؛ assets المحلية كلها200.
- مقارنة requests/contracts بعد كل دفعة؛ لا method/path أو enum/payload تغيّر عَرَضاً.
- مقارنة SHA-256 لكل ملف محمي: يجب أن تطابق baseline. التقرير يذكر الملفات المتغيرة فقط ويعلل كل تغيير. لا تدخل bin/obj/logs في إثبات تغيير المصدر.

## 15. القبول البصري والوظيفي النهائي

### لا تقبل كلمة «مثل البروتوتايب» بلا دليل

لكل route فعلي ولكل حالة خاصة: لقطة مرجع إن وجد + before + after على نفس الأبعاد، وchecklist:

1. نفس لغة التصميم: sidebar أبيض265، header أبيض70، كحلي#022C5A، خلفية فاتحة، Tajawal، حدود/radii/spacing المذكورة.
2. experience مطبق: page padding32/max1440، بطاقات بلا قفز/ظل مبالغ، نص muted مقروء، focus ظاهر، أنماط هادئة.
3. تنسيق الداخل مكتمل لا shell وحده: table/forms/modals/progress/cards/charts/hints/toasts/empty/error.
4. العربية والإنجليزية صحيحتان في الصفحة والحوارات والتوست، LTR يعكس الهيكل فعلياً.
5. 320/390/768/1024/1440/1920 widths؛ لا قص عام أو تراكم أزرار؛ الجداول فقط قابلة لتمرير داخلي.
6. keyboard/200% zoom/reduced motion/تباين AA وfocus؛ الرموز لا تحل محل تسميات مفهومة.
7. كل وظيفة موجودة في baseline باقية وتعمل، وكل متطلب ناقص موثق. لا محو وظيفة لتجميل الواجهة.
8. كل زر/حقل/رابط في `control-matrix` له اختبار؛ نفّذ `AI-UI-TEST-PLAN.ar.md` أو سلّم النسخة لوكيل الاختبار مع أدلة regression الخاصة بك. لا تعلن قبول UI شاملاً قبل جولته الكاملة.
9. صفر تعديل في الملفات المحمية، وصفر تغيير عقد غير مصرّح، وصفر خطأ console/CSP/أصول غير متوقع.

### مخرجات التسليم المطلوبة

- `docs/frontend-baseline.md`، `frontend-contracts.csv`، `frontend-page-matrix.csv`، `frontend-blockers.md`.
- تحديث `docs/template-inventory.md` بالمصادر الفعلية لكل component مستخدم.
- `docs/frontend-change-report.md`: مرحلة/ملف/سبب/مرجع تصميم/حفظ العقد/نتيجة فحص/صورة قبل وبعد.
- `docs/frontend-evidence/<runId>/`: صور مرجعية وقبل/بعد وحوارات لكل دور ولغة، وأدلة البناء والفحوص المنقحة.
- قائمة مسارات الحزمة الغائبة والمزايا التي تحتاج backend بوضوح. «إنجاز إعادة تنسيق الموجود» يختلف في التقرير عن «اكتمال واجهة الحزمة»؛ لا تمنح الثانية إن بقي gap.
- ملخص نهائي موجز: المنجز، نتائج المتصفح والاختبارات، الملفات المحمية التي تأكد عدم تغيرها، العوائق المتبقية، نقطة استئناف محددة للوكيل التالي.

## 16. نص تسليم جاهز لوكيل الفرونت

> اقرأ `docs/AI-FRONTEND-IMPLEMENTATION.ar.md` والحزمة المعتمدة كاملة، ثم نفّذ F0 إلى F9 بالترتيب والبوابات المحددة. طبّق شكل البروتوتايب بطبقتي styles وexperience، وخذ المكوّنات من Paces. التزم بالتوكنات وأنماط الصفحات وخريطة كل صفحة. حافظ على كل عقود DOM/API والصلاحيات، ولا تعدّل أي ملف backend محمي أو route أو قاعدة عمل. إذا نقص دعم الخادم وثّق الحظر وواصل الباقي دون محاكاة بيانات. اختبر كل عنصر وعدّل الواجهة حتى تستوفي معايير القبول، ثم سلّم أدلة قبل/بعد وتقرير العقود والاختبارات، واستعمل ملف خطة UI للجولة الشاملة.
