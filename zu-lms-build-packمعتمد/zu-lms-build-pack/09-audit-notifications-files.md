# 09 — السجلات، الإشعارات، البريد، الملفات، المستودع

## 1. سجل التدقيق (`IAuditLogger`) — **كل شيء يظهر للأدمن**
**قاعدة:** كل عملية تغيّر حالة النظام أو تكشف سرّاً أو تنزّل ملفاً أو تفشل أمنياً تُسجَّل. يُكتب السجل **في المعاملة نفسها** للعملية (فإن فشلت العملية لا سجل نجاح). الفشل الأمني (دخول فاشل، رفض صلاحية، 403) يُسجَّل بـ `success=false` في معاملة مستقلة.
**ما يُسجَّل في كل صف (`audit_logs`):** الوقت، الحساب (id/رقم الدخول/الاسم)، أدواره وقت الحدث، IP، المتصفح (`user_agent` + ملخص الجهاز/النظام)، الفئة، `action_code`، الكيان (نوع/معرّف/وصف)، `course_id` إن وُجد، ملخص عربي وإنجليزي، **القيمة قبل وبعد** (`before_json`/`after_json` للحقول المتغيرة فقط)، النجاح/سبب الفشل، `correlation_id`.
**حجب إلزامي:** احذف قبل الكتابة أي مفتاح اسمه يحوي `password|secret|token|key|hash|enc` ولا تكتب كلمات المرور ولا الكود الصحيح للحضور ولا مفاتيح التشفير أبداً. الإعدادات السرية تُسجَّل «تم التغيير» فقط. عند عرض/تغيير كلمة مرور: يُسجَّل الحدث (من/لمن/متى) بلا قيمة.
**الأحداث (`action_code`) — الحد الأدنى الإلزامي:**
| الفئة | الأحداث |
|---|---|
| Auth | `auth.login`, `auth.login_failed`, `auth.login_first`, `auth.login_shared_initial`, `auth.logout`, `auth.password_changed`, `auth.forgot_requested`, `auth.locked` |
| Security | `security.refresh_reuse`, `security.permission_denied`, `security.rate_limited`, `security.reveal_reauth_failed` |
| User | `user.create|edit|deactivate|restore`, `user.role_assigned|role_revoked`, `credentials.reveal|change|send|send_bulk|shared_set|email_status_set`, `user.access_override_set` |
| Permission | `permission.override_set|override_cleared`, `permission.role_changed` |
| Settings | `settings.changed`, `policy.changed`, `hint.created|updated|deleted`, `center.edited`, `template.uploaded` |
| Course | `course.propose|resubmit|approve|return|reject|create|edit|finish|reopen|force_status|deactivate|restore|archive|unarchive|cancel_request|cancel_approve|cancel_reject|cancel_direct`, `participant.registered|added|removed`, `course.exam_requirement_changed` |
| Lecture | `lecture.create|edit|deactivate`, `material.upload` |
| Attendance | `attendance.code_activated`, `attendance.code_entered` (ناجح)، `attendance.code_wrong`, `attendance.manual_set`, `attendance.manual_cleared` |
| Exam | `exam.created`, `exam.result_set`, `exam.result_changed`, `exam.results_disabled` |
| Impact | `impact.submit|approve|return` |
| Evaluation | `evaluation.submit`, `evalform.draft_saved|published`, `evaluation.period_extended` |
| Certificate | `certificate.issue|revoke|pdf_download|hidden|unhidden`, `procert.*` (`12 §7`) |
| Document | `vault.upload|download|deactivate` |
| Sync | `sync.run`, `sync.review_resolve` |
| Need | `need.submit|resubmit|approve|return` |
| System | `system.startup`, `system.migration` |
**صفحة `/admin/audit`:** جدول بترقيم من الخادم (`audit.page_size`) وفلاتر: فترة، حساب، فئة، `action_code`، كيان، دورة، النجاح/الفشل، IP، بحث نصي في الملخص. فتح صف يعرض **قبل/بعد** بصرياً (فرق ملوّن). زر «تصدير CSV» (`audit.export`) بالفلاتر نفسها.
**صفحة `/admin/live-feed`:** قائمة تتحدث بالاستطلاع كل `audit.live_feed_poll_seconds` عبر `GET /audit/live?afterId=<آخر معرّف>`، تعرض أولاً بأول: موافقة/رفض الجودة، رفع المقترحات وطلبات الإلغاء، إصدار/إلغاء الشهادات، تفعيل الأكواد، التعديلات اليدوية للحضور، كل التغييرات الإدارية. زر إيقاف مؤقت وفلتر فئة.
**الحماية:** الجدول **إلحاقي فقط** (`02 §0`). ممنوع أي واجهة لتعديله أو حذفه (حتى للأدمن).

## 2. الإشعارات داخل المنصة (`INotificationService`)
صف في `notifications` لكل مستلم. الجرس في الهيدر يعرض عدّاد غير المقروء وآخر 20 مع رابط للمصدر. تحديث العداد بالاستطلاع كل 30 ثانية.
| `type` | المستلم | المحتوى |
|---|---|---|
| `ProposalSubmitted` | مستخدمو الجودة | مقترح جديد من كلية X |
| `ProposalDecision` | العميد | اعتماد/إعادة (الملاحظات)/رفض |
| `CourseApproved` | المحاضر الداخلي | تكليف بدورة |
| `ParticipantAdded` / `ParticipantRemoved` | المشارك | إضافة/إخراج |
| `LectureCreated` | المشاركون الفعّالون | «أنشأ المحاضر محاضرة جديدة» |
| `CourseFinished` | المشاركون | انتهت الدورة، متطلبات الشهادة |
| `CertificateIssued` / `CertificateRevoked` | صاحب الشهادة | الإصدار؛ الإلغاء **مع السبب** |
| `EvaluationReopened` | المشاركون المؤهلون | نسخة تقييم جديدة، أعد التقييم |
| `ImpactSubmitted` / `ImpactDecision` | المحاضر / المتدرب | |
| `CancellationRequested` / `CancellationDecision` | الجودة / العميد | |
| `CourseCancelled` / `CourseDisabled` / `CourseRestored` | المحاضر والمشاركون والجودة | |
| `NeedSubmitted` / `NeedDecision` | الجودة / العميد | |
| `ExtInstructorRequested` / `ExtInstructorReady` | الأدمن / الجودة | |
| `ProCertSubmitted` / `ProCertApproved` / `ProCertRejected` | الجودة / صاحبها | |
| `SyncReviewItems` | الأدمن | عناصر مراجعة جديدة |
**العنوان والنص** عربي إلزامي وإنجليزي اختياري، مع مفاتيح موارد لكل نوع. لا تُرسل إشعاراً لمن نفّذ الإجراء بنفسه.

## 3. البريد (`email_outbox` + `EmailOutboxWorker`)
- يُرسل بالبريد **فقط** [م14]: بيانات الدخول الأولية، «نسيت كلمة المرور»، حساب المدرب الخارجي. كل ما عداه إشعارات داخلية.
- **كل رسالة تُكتب أولاً في `email_outbox`** ثم يعالجها العامل: يأخذ الصفوف `Pending` المستحقة (`next_attempt_at <= الآن`) بقفل (`FOR UPDATE SKIP LOCKED`)، يرسل بمعدل `email.throttle_ms`، ويحدّث الحالة (`03 §8` تصنيف النتائج). عند `Sent` **أعد كتابة `body_html` إلى «[محذوف]»** (لأنه قد يحوي كلمة مرور).
- القوالب: `Email/Templates/*.html` منسقة بهوية الجامعة (رأس كحلي `#022C5A`، خط Tajawal كاحتياط Arial، RTL للعربية)، الاسم، رقم الدخول، كلمة المرور في صندوق LTR بخط ثابت العرض، رابط الدخول `ZU_PUBLIC_BASE_URL`، تنبيه «لا تشارك بياناتك». الموضوع بالعربية («بيانات الدخول إلى منصة التطوير والتدريب المهني — جامعة الزرقاء»).
- ممنوع تسجيل نص الرسالة أو كلمة المرور في أي سجل (Serilog يُعدّ بـ Destructuring policy يحجب `body_html`).

## 4. الملفات (`IFileStorage`, `FileRules`)
- **التخزين:** على القرص تحت `ZU_STORAGE_ROOT/{yyyy}/{MM}/{guid}{ext}` (مجلد خارج المشروع، يُركَّب كـ Docker volume). اسم الملف المخزّن GUID؛ الاسم الأصلي يُحفظ في العمود فقط ويُنظَّف (`Path.GetFileName`، حذف أحرف التحكم).
- **الرفع:** `POST /api/v1/files` (multipart) مع `kind`. يتحقق الخادم بالترتيب: صلاحية الرفع لهذا `kind`، **الامتداد** ضمن `files.ext.<kind>`، **الحجم** ≤ `files.max_mb.<kind>`، **النوع الحقيقي** (magic bytes، `FileRules`) يطابق الامتداد، ثم يحسب SHA-256 ويحفظ ويُنشئ `stored_files`. أي فشل → خطأ بلغة المستخدم ولا يُكتب شيء.
- جدول مطابقة `kind` ← مفاتيح الإعدادات: `ImpactFile`→`impact`، `ProposalCouncilLetter|CancellationLetter|NeedLetter|QualityMinutes|Recommendation`→`letters`، `InstructorCv`→`cv`، `TrainingMaterial`→`materials`، `CertificateTemplate`→`cert_template`، `ProfilePhoto`→`photo`، `ProfessionalCertificate`→`pro_cert`، `CertificatePdf` (داخلي فقط).
- **التنزيل:** عبر الـ API فقط (لا مسار مباشر في `wwwroot`)، بعد التحقق من صلاحية المستخدم على السجل المالك للملف، مع `Content-Disposition: attachment` و`X-Content-Type-Options: nosniff`. كل تنزيل لملف حساس (كتب رسمية، أثر، شهادة مهنية، شهادة PDF) يُسجَّل.
- **لا حذف فعلي** أبداً. `is_active=false` يخفيه.
- لا فحص فيروسات في هذه النسخة (سجّل في `QUESTIONS.md` أنه غير مُنفَّذ).

## 5. المستودع المركزي للكتب والموافقات (`IVaultService`)
- **التعبئة التلقائية:** كل رفع لكتاب رسمي ينشئ `vault_documents` فوراً: كتاب مجلس الكلية، السيرة الذاتية للمحاضر الخارجي، كتاب الإلغاء، كتاب الاحتياج، الشهادة المهنية. (مصدر الكيان `source_entity_type/id`.)
- **الرفع اليدوي** (`vault.upload`) للجودة والأدمن: محاضر الجودة (`QualityMinutes`)، كتب التوصيات (`Recommendation`)، أخرى، مع العنوان والدورة/الكلية المرتبطتين.
- **العرض:** `/vault` جدول بحث/فلترة (نوع الوثيقة، دورة، كلية، فترة، رافع)، معاينة (PDF داخل المتصفح) وتنزيل مباشر. الصيغ PDF وDOCX.
- التعطيل (`vault.deactivate`) للأدمن فقط.
