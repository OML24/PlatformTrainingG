# 02 — قاعدة البيانات (PostgreSQL 17 + EF Core)

## 0. قواعد عامة
- الأسماء snake_case. الجداول بصيغة الجمع. المخطط `public`. الامتدادات: `pg_trgm`, `citext` (تُنشأ في أول هجرة).
- **الأنواع:** `bigint` (identity `GENERATED ALWAYS AS IDENTITY`) للمفاتيح، `varchar(n)` للنصوص المحدودة، `text` للطويلة، `boolean`, `timestamptz` (UTC)، `date`, `int`, `smallint`, `numeric(p,s)`, `jsonb`, `uuid`.
- **الـ enums:** عمود `varchar(40)` يخزّن اسم القيمة كما في `01 §3.2` (مثل `Approved`) مع قيد `CHECK (col IN (...))` لكل عمود enum.
- **رموز الأعمدة المشتركة:**
  - `[BASE]` = `id bigint PK`, `created_at timestamptz NOT NULL DEFAULT now()`, `created_by bigint NULL → users.id`, `updated_at timestamptz NULL`, `updated_by bigint NULL → users.id`. (+ رمز التزامن `xmin` يُربط في EF ولا يُنشأ كعمود.)
  - `[SOFT]` = `is_active boolean NOT NULL DEFAULT true`, `deactivated_at timestamptz NULL`, `deactivated_by bigint NULL`, `deactivation_reason text NULL`.
  - `[ARCH]` = `is_archived boolean NOT NULL DEFAULT false`, `archived_at timestamptz NULL`, `archived_by bigint NULL`.
- **البحث العربي:** عمود `search_text varchar(600)` في الجداول المذكورة، يُملأ في التطبيق بدالة `ArabicSearchNormalizer.Normalize()`: إزالة التشكيل والتطويل، توحيد `أ إ آ`→`ا`، `ى`→`ي`، `ة`→`ه`، تحويل لأحرف صغيرة، ضغط المسافات. فهرس `GIN (search_text gin_trgm_ops)`. الاستعلام يُطبَّع بالدالة نفسها ثم `search_text ILIKE '%'||q||'%'`.
- **FK:** `ON DELETE RESTRICT` دائماً (لا حذف فعلي أصلاً).
- **سجل التدقيق غير قابل للتعديل:** بعد الهجرة نفّذ `REVOKE UPDATE, DELETE, TRUNCATE ON audit_logs FROM <app_role>` (مستخدم التطبيق لا يملك سوى INSERT/SELECT على هذا الجدول).

---

## 1. الهيكل الجامعي (مرايا بيانات المصدر، تُملأ بالاستيراد `04`)
```
university_sides
  side_code        int PK
  name_ar          varchar(200) NOT NULL
  is_imported      boolean NOT NULL DEFAULT false     -- الأدمن يختار الجهات التي تُستورد
  last_seen_at     timestamptz NULL

centers [BASE][SOFT]
  side_code        int NOT NULL → university_sides
  center_code      int NOT NULL                        -- center_id في المصدر
  name_ar          varchar(300) NOT NULL               -- center_dscp
  name_en          varchar(300) NULL
  unit_code        int NULL
  head_employee_code      int NULL                     -- centr_head (للاقتراح فقط، لا تعيين آلي)
  registrar_employee_code int NULL                     -- fac_registrar
  is_faculty       boolean NOT NULL DEFAULT false      -- يعدّله الأدمن
  short_code       varchar(10) NULL                    -- للرقم المرجعي للشهادة (IT, ENG...) يعدّله الأدمن
  source_missing   boolean NOT NULL DEFAULT false
  source_missing_since timestamptz NULL
  source_hash      char(64) NULL
  last_seen_at     timestamptz NULL
  UNIQUE (side_code, center_code)
  INDEX (is_faculty) WHERE is_active

departments [BASE][SOFT]
  side_code        int NOT NULL → university_sides
  department_code  int NOT NULL                        -- department_id في المصدر
  center_code      int NULL                            -- center_id الخام من المصدر
  center_id        bigint NULL → centers               -- يُحلّ بـ (side_code, center_code) عند الاستيراد
  name_ar          varchar(300) NOT NULL               -- department_dscp
  name_en          varchar(300) NULL
  reg_code         varchar(20) NULL
  head_employee_code int NULL                          -- gate_dep_head
  source_missing, source_missing_since, source_hash, last_seen_at  (كما في centers)
  UNIQUE (side_code, department_code)

employees [BASE]                                       -- مرآة المصدر، لا تُعدَّل محلياً
  side_code        int NOT NULL → university_sides
  employee_code    int NOT NULL                        -- employee_id في المصدر
  first_name, second_name, third_name, last_name  varchar(100) NULL   -- fname,sname,tname,lname بعد التقليم
  full_name_ar     varchar(400) NOT NULL               -- NameRules.Join(الأربعة)
  full_name_en     varchar(400) NULL                   -- ename
  center_code      int NULL
  department_code  int NULL
  center_id        bigint NULL → centers
  department_id    bigint NULL → departments
  main_job_code    int NULL    main_job_desc varchar(100) NULL
  sub_job_code     int NULL    sub_job_desc  varchar(200) NULL
  emp_status       int NULL    emp_status_desc varchar(100) NULL
  chk_emp_stat     int NULL
  start_work_date  date NULL   stop_work_date date NULL
  source_email     varchar(254) NULL                   -- من واجهة email، أحرف صغيرة
  is_academic      boolean NOT NULL DEFAULT false      -- main_job_code ∈ إعداد directory.academic_main_job_codes
  search_text      varchar(600) NOT NULL
  source_missing, source_missing_since, source_hash, last_seen_at
  UNIQUE (side_code, employee_code)
  INDEX (center_id), INDEX (department_id), GIN (search_text gin_trgm_ops)
```
**ممنوع** إنشاء أعمدة لـ: `sid`, `birth_date`, `mother_name`, `bassport`, `mobile`, `nationality*`, `gender*`, `dual_job_id`, `job_id_app`, `shift_id`, `zu_cler`, `blc_cler`, `extention`.

---

## 2. الهوية والصلاحيات
```
users [BASE][SOFT]
  account_type     varchar(20) NOT NULL                -- Employee | ExternalInstructor | Local
  login_name       varchar(20) NOT NULL UNIQUE         -- Employee: employee_code كنص | External: 4 أرقام | Local: ^[A-Za-z0-9_.-]{3,20}$
  employee_id      bigint NULL UNIQUE → employees      -- إلزامي لـ Employee، NULL لغيره
  full_name_ar     varchar(400) NOT NULL
  full_name_en     varchar(400) NULL
  email_override   varchar(254) NULL                   -- أحرف صغيرة. لـ External هو بريده الوحيد
  password_hash    varchar(300) NOT NULL               -- PBKDF2 (03)
  password_enc     text NOT NULL                       -- الكلمة الحالية مشفرة AES-GCM
  initial_password_enc text NULL                       -- الأولية الشخصية (Employee فقط)
  must_change_password boolean NOT NULL DEFAULT false
  first_login_completed_at timestamptz NULL            -- NULL = لم يكمل أول دخول
  password_changed_at timestamptz NULL   password_changed_by bigint NULL
  security_stamp   uuid NOT NULL DEFAULT gen_random_uuid()
  credentials_email_status varchar(20) NOT NULL DEFAULT 'NotSent'
  credentials_email_sent_at timestamptz NULL
  credentials_email_last_error varchar(1000) NULL
  credentials_email_attempts int NOT NULL DEFAULT 0
  access_override_allow boolean NOT NULL DEFAULT false -- تجاوز الأدمن لمنع الحالة الوظيفية
  access_override_by bigint NULL  access_override_at timestamptz NULL  access_override_reason text NULL
  preferred_language varchar(2) NOT NULL DEFAULT 'ar'  -- ar | en
  ui_settings      jsonb NULL                          -- إعدادات شكل الواجهة فقط (الشريط الجانبي مطوي...)
  last_login_at timestamptz NULL
  failed_login_count int NOT NULL DEFAULT 0   locked_until timestamptz NULL
  search_text      varchar(600) NOT NULL               -- الاسمان + login_name
  CHECK ((account_type='Employee') = (employee_id IS NOT NULL))
  CHECK (account_type<>'ExternalInstructor' OR login_name ~ '^[0-9]{4}$')
  CHECK (account_type<>'Employee' OR login_name ~ '^[0-9]+$')
  GIN (search_text gin_trgm_ops)

roles
  id bigint PK, code varchar(30) UNIQUE, name_ar, name_en, is_system boolean, is_assignable boolean, sort_order int
  -- Instructor: is_assignable=false (مشتق من تكليف الدورة، لا يُعيَّن يدوياً)

permissions
  id bigint PK, perm_key varchar(120) UNIQUE, group_key varchar(60), kind varchar(20), scope varchar(20),
  name_ar varchar(200), name_en varchar(200), sort_order int, is_active boolean DEFAULT true

role_permissions
  role_id → roles, permission_id → permissions, PRIMARY KEY (role_id, permission_id)

user_roles [BASE]
  user_id → users, role_id → roles, scope_center_id bigint NULL → centers
  is_active boolean NOT NULL DEFAULT true, revoked_at timestamptz NULL, revoked_by bigint NULL
  UNIQUE INDEX (user_id, role_id, COALESCE(scope_center_id,0)) WHERE is_active
  -- دور Dean: scope_center_id إلزامي ويجب أن يكون المركز is_faculty=true (يُتحقق في الخدمة)

user_permission_overrides [BASE]
  user_id → users, permission_id → permissions, mode varchar(10) NOT NULL (Allow|Deny), reason text NULL
  UNIQUE (user_id, permission_id)         -- غياب الصف = «اتباع الدور»

refresh_tokens
  id bigint PK, user_id → users, token_hash char(64) UNIQUE, family_id uuid NOT NULL,
  issued_at timestamptz, expires_at timestamptz, remember_me boolean,
  used_at timestamptz NULL, revoked_at timestamptz NULL, revoked_reason varchar(50) NULL,
  replaced_by_id bigint NULL, ip varchar(64), user_agent varchar(400)
  INDEX (user_id), INDEX (expires_at)

forgot_password_attempts
  id bigint PK, login_name_input varchar(40), email_input varchar(254), ip varchar(64),
  requested_at timestamptz, outcome varchar(30)   -- Sent|NoMatch|RateLimited|SharedEmail|NoEmail
  user_id bigint NULL
  INDEX (login_name_input, requested_at), INDEX (ip, requested_at)

password_policy                                        -- صف واحد
  id smallint PK CHECK (id=1)
  min_length int NOT NULL DEFAULT 15       max_length int NOT NULL DEFAULT 128
  allow_spaces boolean NOT NULL DEFAULT true   allow_arabic boolean NOT NULL DEFAULT true
  block_common boolean NOT NULL DEFAULT true   block_context boolean NOT NULL DEFAULT true
  require_upper boolean DEFAULT false  require_lower boolean DEFAULT false
  require_digit boolean DEFAULT false  require_symbol boolean DEFAULT false
  rotation_days int NULL                          -- NULL = بلا تغيير دوري
  updated_by bigint NULL, updated_at timestamptz NULL

blocked_passwords
  id bigint PK, password_normalized varchar(200) UNIQUE, source varchar(20) (Seed|Admin), created_by bigint NULL

external_instructor_profiles
  user_id bigint PK → users
  organization_ar varchar(300) NOT NULL, organization_en varchar(300) NULL
  phone varchar(30) NULL, cv_file_id bigint NULL → stored_files, notes text NULL
  created_from_request_id bigint NULL → external_instructor_account_requests
```

---

## 3. الإعدادات والتلميحات والتسلسلات
```
system_settings
  setting_key varchar(100) PK
  group_key varchar(50) NOT NULL
  value_type varchar(20) NOT NULL                 -- Int|Decimal|Bool|String|Json
  value text NOT NULL
  default_value text NOT NULL
  min_value numeric NULL, max_value numeric NULL
  label_ar varchar(200) NOT NULL, label_en varchar(200) NULL
  description_ar text NULL, description_en text NULL
  is_secret boolean NOT NULL DEFAULT false         -- القيمة مشفرة، تُعرض للأدمن فقط
  sort_order int NOT NULL DEFAULT 0
  updated_by bigint NULL, updated_at timestamptz NULL

field_hints [BASE][SOFT]
  field_key varchar(150) NOT NULL, context_key varchar(100) NOT NULL
  hint_ar text NOT NULL, hint_en text NULL, is_enabled boolean NOT NULL DEFAULT true
  UNIQUE (field_key, context_key) WHERE is_active

number_sequences
  sequence_key varchar(100) PK, last_value bigint NOT NULL DEFAULT 0
  -- مفاتيح: COURSE:{yyyy} ، CERT:{yyyy}:{CODE}.  الزيادة ذرّية: UPDATE ... SET last_value=last_value+1 RETURNING last_value (ضمن المعاملة)
```
كتالوج الإعدادات الكامل (المفاتيح والقيم الافتراضية) في `03 §6`.

---

## 4. السجلات والملفات والإشعارات
```
audit_logs
  id bigint PK
  occurred_at timestamptz NOT NULL
  actor_user_id bigint NULL, actor_login varchar(40) NULL, actor_name varchar(400) NULL
  actor_roles varchar(200) NULL                    -- لقطة الأدوار وقت الحدث
  ip varchar(64) NULL, user_agent varchar(500) NULL, device_summary varchar(200) NULL
  category varchar(30) NOT NULL, action_code varchar(80) NOT NULL
  entity_type varchar(60) NULL, entity_id bigint NULL, entity_label varchar(300) NULL
  course_id bigint NULL                            -- لتصفية سجلات دورة
  summary_ar varchar(500) NOT NULL, summary_en varchar(500) NULL
  before_json jsonb NULL, after_json jsonb NULL    -- بعد حجب الحقول السرية (09)
  success boolean NOT NULL DEFAULT true, failure_reason varchar(500) NULL
  correlation_id varchar(64) NULL
  INDEX (occurred_at DESC), (actor_user_id, occurred_at DESC), (category, occurred_at DESC),
        (entity_type, entity_id), (course_id, occurred_at DESC), (action_code)

stored_files
  id bigint PK, kind varchar(40) NOT NULL, original_name varchar(300) NOT NULL,
  stored_name varchar(80) NOT NULL UNIQUE, relative_path varchar(500) NOT NULL,
  mime_type varchar(100) NOT NULL, size_bytes bigint NOT NULL, sha256 char(64) NOT NULL,
  uploaded_by bigint NULL → users, uploaded_at timestamptz NOT NULL, is_active boolean NOT NULL DEFAULT true
  INDEX (sha256)

vault_documents [BASE][SOFT]
  file_id bigint NOT NULL → stored_files, doc_type varchar(40) NOT NULL,
  title_ar varchar(300) NOT NULL, title_en varchar(300) NULL,
  related_course_id bigint NULL → courses, related_center_id bigint NULL → centers,
  source_entity_type varchar(50) NULL, source_entity_id bigint NULL, uploaded_by bigint NULL, uploaded_at timestamptz NOT NULL
  INDEX (doc_type), (related_course_id), (related_center_id)

notifications
  id bigint PK, user_id bigint NOT NULL → users, type varchar(60) NOT NULL,
  title_ar varchar(300) NOT NULL, title_en varchar(300) NULL, body_ar text NOT NULL, body_en text NULL,
  link_url varchar(300) NULL, related_entity_type varchar(50) NULL, related_entity_id bigint NULL,
  created_at timestamptz NOT NULL, read_at timestamptz NULL, is_active boolean NOT NULL DEFAULT true
  INDEX (user_id, read_at, created_at DESC)

email_outbox
  id bigint PK, kind varchar(40) NOT NULL, to_address varchar(254) NOT NULL, to_name varchar(300) NULL,
  subject varchar(300) NOT NULL, body_html text NOT NULL, related_user_id bigint NULL,
  status varchar(20) NOT NULL DEFAULT 'Pending', attempts int NOT NULL DEFAULT 0,
  last_error varchar(1000) NULL, provider_message_id varchar(200) NULL,
  created_at timestamptz NOT NULL, next_attempt_at timestamptz NOT NULL, sent_at timestamptz NULL, locked_at timestamptz NULL
  INDEX (status, next_attempt_at)
  -- body_html قد يحوي كلمة مرور: تُعاد كتابته إلى «[محذوف]» فور النجاح (status=Sent). ولا يُسجَّل في أي سجل.
```

---

## 5. التدريب
```
training_tracks [BASE][SOFT][ARCH]
  name_ar varchar(300) NOT NULL, name_en varchar(300) NULL,
  description_ar text NULL, description_en text NULL, objectives_ar text NULL, objectives_en text NULL, sort_order int DEFAULT 0

courses [BASE][SOFT][ARCH]
  course_no varchar(30) NOT NULL UNIQUE               -- C-{YYYY}-{NNNN}
  title_ar varchar(300) NOT NULL, title_en varchar(300) NULL
  track_id bigint NOT NULL → training_tracks
  delivery_mode varchar(20) NOT NULL                   -- InPerson|Online|Blended
  objectives_ar text NOT NULL, objectives_en text NULL
  target_audience_ar text NOT NULL, target_audience_en text NULL
  total_hours numeric(6,2) NOT NULL CHECK (total_hours>0)
  planned_start_date date NOT NULL, planned_end_date date NOT NULL CHECK (planned_end_date>=planned_start_date)
  origin varchar(20) NOT NULL                          -- DeanProposal|QualityCreated|AdminCreated|Historical
  proposing_center_id bigint NULL → centers, proposed_by_user_id bigint NULL → users
  audience_type varchar(20) NOT NULL                   -- General|Faculty|Named
  target_center_id bigint NULL → centers               -- إلزامي عند Faculty
  registration_deadline timestamptz NULL, max_participants int NULL CHECK (max_participants>0)
  has_exam boolean NOT NULL DEFAULT false
  status varchar(20) NOT NULL, status_changed_at timestamptz NOT NULL
  approved_by bigint NULL, approved_at timestamptz NULL
  evaluation_end_date date NULL                        -- إلزامي عند الاعتماد
  evaluation_form_version_id bigint NULL → evaluation_form_versions   -- التثبيت (07)
  finished_at timestamptz NULL, finished_by bigint NULL, final_lectures_count int NULL
  attendance_threshold_percent_override smallint NULL CHECK (BETWEEN 1 AND 100)
  managed_by_quality boolean NOT NULL DEFAULT false
  instructor_kind varchar(20) NOT NULL                 -- Internal|ExternalAccount|ExternalNameOnly
  instructor_user_id bigint NULL → users
  instructor_name_ar varchar(300) NOT NULL, instructor_name_en varchar(300) NULL
  instructor_org_ar varchar(300) NULL, instructor_org_en varchar(300) NULL
  cancelled_at timestamptz NULL, cancelled_by bigint NULL, cancellation_reason text NULL
  is_historical boolean NOT NULL DEFAULT false, historical_notes text NULL
  current_revision_no int NOT NULL DEFAULT 1
  last_return_notes text NULL                          -- ملاحظات الجودة الأخيرة (تُعرض بالأحمر)
  deactivation_certificates_action varchar(20) NULL    -- Keep|Disable (اختيار الأدمن عند تعطيل دورة لها شهادات)
  search_text varchar(600) NOT NULL                    -- course_no + العنوانين + اسم المحاضر
  CHECK (instructor_kind IN ('Internal','ExternalAccount') = (instructor_user_id IS NOT NULL))
  CHECK (instructor_kind <> 'ExternalNameOnly' OR managed_by_quality)
  CHECK (audience_type<>'Faculty' OR target_center_id IS NOT NULL)
  CHECK (origin<>'DeanProposal' OR proposing_center_id IS NOT NULL)
  INDEX (status), (proposing_center_id), (target_center_id), (instructor_user_id), (track_id),
        (is_active, is_archived), GIN (search_text gin_trgm_ops)

course_status_history
  id bigint PK, course_id → courses, from_status varchar(20) NULL, to_status varchar(20) NOT NULL,
  changed_by bigint NULL, changed_at timestamptz NOT NULL, reason text NULL
  INDEX (course_id, changed_at)

course_proposal_revisions
  id bigint PK, course_id → courses, revision_no int NOT NULL, snapshot_json jsonb NOT NULL,
  submitted_at timestamptz NOT NULL, submitted_by bigint NOT NULL → users,
  decision varchar(20) NOT NULL DEFAULT 'Pending', decided_by bigint NULL, decided_at timestamptz NULL, decision_notes text NULL
  UNIQUE (course_id, revision_no)
  -- snapshot_json = كل حقول الدورة + قائمة (file_id, purpose, name) للمرفقات وقت الإرسال. لا يُعدَّل بعد الإنشاء.

course_files [BASE][SOFT]
  course_id → courses, file_id → stored_files, purpose varchar(40) NOT NULL,
  lecture_id bigint NULL → lectures, revision_no int NULL, title varchar(300) NULL

course_participants [BASE]
  course_id → courses, user_id → users, source varchar(20) NOT NULL,
  added_by_user_id bigint NULL, joined_at timestamptz NOT NULL,
  is_active boolean NOT NULL DEFAULT true, removed_at timestamptz NULL, removed_by bigint NULL, removal_reason text NULL,
  notes text NULL
  UNIQUE (course_id, user_id)       -- إعادة الإضافة بعد الإخراج = إعادة تفعيل الصف نفسه
  INDEX (user_id)

lectures [BASE][SOFT]
  course_id → courses, title_ar varchar(300) NULL, title_en varchar(300) NULL,
  mode varchar(20) NOT NULL,                           -- InPerson|Online
  starts_at timestamptz NOT NULL, ends_at timestamptz NOT NULL CHECK (ends_at>starts_at),
  location_ar varchar(300) NULL, location_en varchar(300) NULL, teams_url varchar(600) NULL,
  attendance_code varchar(8) NOT NULL CHECK (attendance_code ~ '^[0-9]{4,8}$')   -- الطول من attendance.code_length وقت الإنشاء,
  code_activated_at timestamptz NULL, code_expires_at timestamptz NULL, notes_ar text NULL
  INDEX (course_id, starts_at)

attendance_records
  id bigint PK, lecture_id → lectures, participant_id → course_participants,
  is_present boolean NOT NULL, source varchar(10) NOT NULL, marked_at timestamptz NOT NULL, marked_by bigint NULL
  UNIQUE (lecture_id, participant_id)

attendance_changes
  id bigint PK, lecture_id, participant_id, old_present boolean NULL, new_present boolean NOT NULL,
  source varchar(10) NOT NULL, changed_by bigint NULL, changed_at timestamptz NOT NULL, reason text NULL

attendance_code_attempts
  id bigint PK, lecture_id → lectures, user_id → users, wrong_count int NOT NULL DEFAULT 0,
  locked_until timestamptz NULL, last_attempt_at timestamptz NOT NULL
  UNIQUE (lecture_id, user_id)

course_exams [BASE]
  course_id bigint NOT NULL UNIQUE → courses, name_ar varchar(300) NOT NULL, name_en varchar(300) NULL,
  is_active boolean NOT NULL DEFAULT true

exam_results [BASE]
  exam_id → course_exams, participant_id → course_participants,
  outcome varchar(10) NOT NULL (Passed|Failed), is_active boolean NOT NULL DEFAULT true,
  recorded_by bigint NOT NULL, recorded_at timestamptz NOT NULL, changed_at timestamptz NULL,
  deactivated_by bigint NULL, deactivated_at timestamptz NULL, deactivation_reason text NULL
  UNIQUE (exam_id, participant_id) WHERE is_active
  -- غياب صف نشط = «لم تُرصد النتيجة» وليس رسوباً.

exam_result_history
  id bigint PK, exam_result_id → exam_results, old_outcome varchar(10) NULL, new_outcome varchar(10) NOT NULL,
  changed_by bigint NOT NULL, changed_at timestamptz NOT NULL, note text NULL

impact_submissions [BASE][SOFT]
  participant_id → course_participants, sequence_no int NOT NULL, impact_type varchar(20) NOT NULL,
  file_id bigint NOT NULL → stored_files, file_sha256 char(64) NOT NULL,
  status varchar(20) NOT NULL, submitted_at timestamptz NOT NULL,
  decided_by bigint NULL, decided_at timestamptz NULL, instructor_notes text NULL
  UNIQUE (participant_id, sequence_no)

course_cancellation_requests [BASE]
  course_id → courses, requested_by bigint NOT NULL, reason text NOT NULL, letter_file_id bigint NOT NULL → stored_files,
  status varchar(20) NOT NULL DEFAULT 'Pending', decided_by bigint NULL, decided_at timestamptz NULL, decision_notes text NULL
  UNIQUE (course_id) WHERE status='Pending'

external_instructor_account_requests [BASE]
  course_id → courses, requested_by_user_id bigint NOT NULL,
  full_name_ar varchar(300) NOT NULL, full_name_en varchar(300) NULL,
  organization_ar varchar(300) NOT NULL, organization_en varchar(300) NULL,
  email varchar(254) NOT NULL,
  status varchar(20) NOT NULL DEFAULT 'PendingAdmin',
  forwarded_by bigint NULL, forwarded_at timestamptz NULL,
  handled_by bigint NULL, handled_at timestamptz NULL, created_user_id bigint NULL → users, reject_reason text NULL
```

## 6. التقييم والشهادات
```
evaluation_form_versions [BASE]
  version_no int NOT NULL UNIQUE, status varchar(20) NOT NULL,
  title_ar varchar(300) NOT NULL, title_en varchar(300) NULL, published_at timestamptz NULL, published_by bigint NULL
  UNIQUE INDEX ON ((status)) WHERE status='Draft'      -- مسودة واحدة فقط

evaluation_questions
  id bigint PK, version_id → evaluation_form_versions, question_key uuid NOT NULL, sort_order int NOT NULL,
  text_ar text NOT NULL, text_en text NULL
  UNIQUE (version_id, question_key)

evaluation_options
  id bigint PK, question_id → evaluation_questions, sort_order int NOT NULL,
  label_ar varchar(300) NOT NULL, label_en varchar(300) NULL, score smallint NULL

evaluation_responses
  id bigint PK, course_id → courses, participant_id → course_participants, version_id → evaluation_form_versions,
  submitted_at timestamptz NOT NULL
  UNIQUE (course_id, participant_id, version_id)

evaluation_answers
  id bigint PK, response_id → evaluation_responses, question_id → evaluation_questions, option_id → evaluation_options
  UNIQUE (response_id, question_id)

certificate_templates [BASE]
  version_no int NOT NULL UNIQUE, file_id bigint NOT NULL → stored_files,
  page_width_pt numeric(8,2) NOT NULL, page_height_pt numeric(8,2) NOT NULL,
  layout_json jsonb NOT NULL, is_current boolean NOT NULL DEFAULT false, notes text NULL
  UNIQUE INDEX ON ((is_current)) WHERE is_current

certificates [BASE][SOFT]
  reference_no varchar(40) NOT NULL UNIQUE, verification_token varchar(64) NOT NULL UNIQUE,
  course_id → courses, participant_id → course_participants, user_id → users,
  status varchar(20) NOT NULL (Issued|Revoked), issued_at timestamptz NOT NULL,
  template_id bigint NOT NULL → certificate_templates, pdf_file_id bigint NOT NULL → stored_files,
  holder_name_ar varchar(400) NOT NULL, holder_name_en varchar(400) NULL,
  course_title_ar varchar(300) NOT NULL, course_title_en varchar(300) NULL,
  course_period_text varchar(200) NOT NULL, course_hours numeric(6,2) NOT NULL,
  instructor_name_ar varchar(300) NOT NULL, instructor_name_en varchar(300) NULL,
  revoked_at timestamptz NULL, revoked_reason varchar(50) NULL, revoked_by bigint NULL, revoked_note text NULL,
  hidden_by_course_disable boolean NOT NULL DEFAULT false
  UNIQUE (participant_id) WHERE status='Issued'
  INDEX (user_id, status), (course_id)
  -- لقطة الحقول مجمّدة وقت الإصدار؛ لا تتغير بتعديل الدورة لاحقاً.
```

## 7. الاحتياجات والملفات الشخصية والاستيراد
```
training_needs [BASE][SOFT]
  center_id bigint NOT NULL → centers (كلية العميد), submitted_by bigint NOT NULL → users,
  title_ar varchar(300) NOT NULL, title_en varchar(300) NULL, description_ar text NOT NULL, description_en text NULL,
  track_id bigint NULL → training_tracks, expected_participants int NULL, preferred_timeframe_ar varchar(200) NULL,
  requested_instructor_user_id bigint NULL → users,    -- طلب محاضر من كلية أخرى
  requested_instructor_note_ar text NULL,
  letter_file_id bigint NOT NULL → stored_files, status varchar(20) NOT NULL DEFAULT 'Submitted',
  current_revision int NOT NULL DEFAULT 1, last_return_notes text NULL

training_need_events
  id bigint PK, need_id → training_needs, event varchar(30) NOT NULL (Submitted|Returned|Resubmitted|Approved),
  notes text NULL, by_user_id bigint NOT NULL, at timestamptz NOT NULL

member_profiles
  user_id bigint PK → users, bio_ar text NULL, bio_en text NULL, photo_file_id bigint NULL → stored_files,
  expertise_ar text[] NOT NULL DEFAULT '{}', expertise_en text[] NOT NULL DEFAULT '{}',
  contact_method varchar(20) NULL (Email|Phone|None), contact_value varchar(200) NULL,
  show_in_directory boolean NOT NULL DEFAULT true, updated_at timestamptz NULL

sync_runs
  id bigint PK, started_at timestamptz, finished_at timestamptz NULL, triggered_by bigint NULL,
  status varchar(20) NOT NULL, stats_json jsonb NOT NULL, error_text text NULL

sync_review_items
  id bigint PK, entity_type varchar(30) NOT NULL, entity_key varchar(100) NOT NULL,
  reason varchar(40) NOT NULL, details text NULL, first_seen_run_id bigint NULL → sync_runs,
  origin varchar(20) NOT NULL CHECK (origin IN ('Sync','ForgotPassword','Provisioning')),
  resolved_at timestamptz NULL, resolved_by bigint NULL, resolution varchar(20) NULL, notes text NULL
  UNIQUE (entity_type, entity_key, reason) WHERE resolved_at IS NULL
```

---

## 7-ب. الشهادات المهنية (ملف 12)
```
professional_cert_categories [BASE][SOFT]
  name_ar varchar(200) NOT NULL, name_en varchar(200) NULL, sort_order int NOT NULL DEFAULT 0

professional_certifications [BASE][SOFT]
  user_id bigint NOT NULL → users                         -- صاحب الشهادة
  category_id bigint NOT NULL → professional_cert_categories
  name varchar(300) NOT NULL                              -- اسم الشهادة كما يظهر في وثيقتها (أي لغة)
  name_ar varchar(300) NULL
  issuer_name varchar(300) NOT NULL                       -- الجهة المانحة (Microsoft, PMI, Cisco...)
  issuer_website_url varchar(600) NULL
  description text NULL
  credential_code varchar(200) NULL                       -- رمز/رقم الشهادة (Credential ID)
  verify_url varchar(600) NULL                            -- رابط التحقق من الجهة المانحة (http/https)
  issued_date date NOT NULL, expiry_date date NULL        -- NULL = لا تنتهي
  file_id bigint NOT NULL → stored_files                  -- PDF أو PPTX (files.ext.pro_cert)
  status varchar(20) NOT NULL DEFAULT 'Submitted'         -- Submitted|Approved|Rejected
  submitted_at timestamptz NOT NULL
  decided_by bigint NULL → users, decided_at timestamptz NULL, decision_notes text NULL
  search_text varchar(900) NOT NULL                       -- name + issuer + code + اسم الصاحب
  CHECK (credential_code IS NOT NULL OR verify_url IS NOT NULL)      -- دليل تحقق واحد على الأقل
  CHECK (expiry_date IS NULL OR expiry_date >= issued_date)
  CHECK (verify_url IS NULL OR verify_url ~* '^https?://')
  UNIQUE (user_id, lower(issuer_name), lower(credential_code)) WHERE credential_code IS NOT NULL AND is_active AND status<>'Rejected'
  INDEX (user_id, status), (category_id), (status, submitted_at), GIN (search_text gin_trgm_ops)

professional_cert_events
  id bigint PK, cert_id → professional_certifications, event varchar(20) NOT NULL (Submitted|Resubmitted|Approved|Rejected|Edited|Withdrawn|Restored),
  notes text NULL, by_user_id bigint NOT NULL, at timestamptz NOT NULL
```
بذرة `professional_cert_categories` (قابلة للتعديل): «تقنية المعلومات والحوسبة السحابية»، «الأمن السيبراني»، «إدارة المشاريع والأعمال»، «البيانات والذكاء الاصطناعي»، «التدريس والتعليم الإلكتروني»، «البحث العلمي والنشر»، «اللغات»، «أخرى».

## 8. البيانات الأولية (Seed) — تُنفَّذ من `DbSeeder` عند أول تشغيل ومرة واحدة (idempotent)
1. `roles`: Admin («مسؤول النظام»)، Quality («مسؤول الجودة والاعتماد»)، Dean («عميد الكلية»)، Instructor («محاضر الدورة») `is_assignable=false`، Member («عضو هيئة التدريس / متدرب»)، ExternalInstructor («مدرب خارجي»).
2. `permissions` + `role_permissions`: من كتالوج `03 §5`.
3. `system_settings`: من `03 §6`.
4. `password_policy`: الصف الافتراضي (15 / 128 / مسافات + عربي مسموحان / منع الشائع والسياقي / بلا تشكيلة / بلا دوران).
5. `blocked_passwords`: قائمة الكلمات الشائعة (ملف مضمَّن `Seed/common-passwords.txt` أعلى 10,000) بعد التطبيع. يضيف الأدمن عليها من الواجهة.
6. `university_sides`: `(320, «جامعة الزرقاء», is_imported=true)`. بقية الجهات تُنشأ `is_imported=false` عند أول ظهور في المصدر.
7. `training_tracks` (الأربعة في الوثيقة §5-أ): «محور التطوير التقني والرقمي»، «محور طرائق التدريس والقياس والتقويم»، «محور البحث العلمي والنشر الدولي»، «محور القيادة المؤسسية والموارد البشرية (HR)».
8. `evaluation_form_versions`: النسخة 1 **منشورة** بالأسئلة الأولية أدناه، لكل سؤال خمسة خيارات: «ممتاز» (5)، «جيد جداً» (4)، «جيد» (3)، «مقبول» (2)، «ضعيف» (1). الأسئلة (بيانات أولية يستبدلها الأدمن/الجودة):
   1) «مدى وضوح أهداف الدورة» 2) «مدى ملاءمة المحتوى لاحتياجاتي» 3) «تمكّن المحاضر من المادة» 4) «أسلوب المحاضر في العرض والتفاعل» 5) «جودة المواد التدريبية والتنظيم» 6) «تقييمي العام للدورة».
9. `field_hints`: صف لكل حقل وارد في `08` بنص عربي افتراضي (يُملأ من ملف `Seed/field-hints.ar.json` الذي تنشئه من أعمدة `08`).
10. **الحساب المحلي الأول:** إن لم يوجد أدمن فعّال وكانت متغيرات `ZU_BOOTSTRAP_ADMIN_LOGIN` و`ZU_BOOTSTRAP_ADMIN_PASSWORD` و`ZU_BOOTSTRAP_ADMIN_NAME` موجودة، أنشئ مستخدماً `account_type='Local'` بدور `Admin` وبدون `must_change_password`. بعد أول استيراد ينشئ الأدمن حسابات الموظفين ويعيّن الأدمن الحقيقي ثم يعطّل الحساب المحلي.

## 9. تعليمات الهجرة
- هجرة أولية واحدة `InitialCreate` تنشئ كل ما سبق. ثم لا تعدّل الهجرات القديمة أبداً.
- القيود المركبة (الفهارس الجزئية والتعبيرية والـ CHECK) تُكتب في `IEntityTypeConfiguration` عبر `HasIndex(...).HasFilter(...)` و`ToTable(t => t.HasCheckConstraint(...))`، وما لا يدعمه EF مباشرة يُكتب `migrationBuilder.Sql(...)` في الهجرة نفسها.
- اختبار إلزامي: تشغيل الهجرات على PostgreSQL حقيقي (Testcontainers) والتأكد من نجاح `DbSeeder` مرتين متتاليتين بلا تكرار.
