using System.Globalization;

namespace ZuLms.Web.Middleware;

/// <summary>
/// User-facing messages for the API error envelope (Arabic / English).
/// </summary>
public static class ApiMessages
{
    private static readonly Dictionary<string, (string Ar, string En)> Catalog = new(StringComparer.Ordinal)
    {
        ["BAD_REQUEST"] = ("الطلب غير صالح.", "The request is not valid."),
        ["VALIDATION_FAILED"] = ("تحقق من الحقول المميزة ثم أعد المحاولة.",
            "Check the highlighted fields and try again."),
        ["UNAUTHORIZED"] = ("انتهت الجلسة، سجّل الدخول من جديد.", "Your session has expired, please sign in again."),
        ["FORBIDDEN"] = ("لا تملك صلاحية تنفيذ هذا الإجراء.", "You are not allowed to perform this action."),
        ["NOT_FOUND"] = ("العنصر المطلوب غير موجود.", "The requested item was not found."),
        ["METHOD_NOT_ALLOWED"] = ("الطريقة غير مسموحة لهذا المسار.", "This method is not allowed for this route."),
        ["MANAGER_CENTER_REQUIRED"] = ("اختر مركزًا أو دائرة إدارية (ليست كلية) لمدير المركز.",
            "Choose an administrative center (not a faculty) for the center manager."),
        ["DEAN_FACULTY_REQUIRED"] = ("اختر الكلية للعميد.", "Choose the dean's faculty."),
        ["CONFLICT"] = ("تعارض مع تعديل آخر، حدّث الصفحة وأعد المحاولة.",
            "The data changed in another session. Refresh and try again."),
        ["RATE_LIMITED"] = ("محاولات كثيرة، انتظر قليلاً ثم أعد المحاولة.",
            "Too many attempts. Wait a moment and try again."),
        ["CSRF_INVALID"] = ("انتهت صلاحية رمز الحماية، أعد تحميل الصفحة.",
            "The security token expired, reload the page."),
        ["INTERNAL_ERROR"] = ("حدث خطأ غير متوقع، أعد المحاولة لاحقاً.",
            "An unexpected error occurred, try again later."),
        ["AUTH_INVALID"] = ("رقم الدخول أو كلمة المرور غير صحيحة.",
            "The login name or password is incorrect."),
        ["AUTH_LOCKED"] = ("الحساب مقفل مؤقتاً بعد محاولات خاطئة متكررة.",
            "The account is temporarily locked after repeated failed attempts."),
        ["AUTH_DISABLED"] = ("الحساب معطّل، راجع مسؤول النظام.", "The account is disabled, contact the administrator."),
        ["AUTH_NOT_ELIGIBLE"] = ("الحساب غير مؤهل للدخول حالياً.", "The account is not eligible to sign in right now."),
        ["LOGIN_RESERVED"] = ("اسم الدخول محجوز لنوع حساب آخر.", "The login name is reserved for another account type."),
        ["AUTH_FORGOT_GENERIC"] = ("إن كانت البيانات صحيحة فستصلك رسالة على بريدك الجامعي.",
            "If the details are correct, a message will be sent to your university email."),
        ["PWD_CONFIRM_MISMATCH"] = ("تأكيد كلمة المرور غير مطابق.", "The password confirmation does not match."),
        ["PWD_TOO_SHORT"] = ("كلمة المرور أقصر من الحد الأدنى.", "The password is shorter than the minimum."),
        ["PWD_TOO_LONG"] = ("كلمة المرور أطول من الحد الأقصى.", "The password is longer than the maximum."),
        ["PWD_BLOCKED"] = ("كلمة المرور شائعة أو محظورة.", "The password is common or blocked."),
        ["PWD_CONTAINS_CONTEXT"] = ("لا تحتوي كلمة المرور على بياناتك الشخصية.",
            "The password must not contain your personal details."),
        ["PWD_NO_SPACES"] = ("كلمة المرور لا تسمح بالمسافات.", "The password must not contain spaces."),
        ["PWD_REUSED"] = ("لا يمكن إعادة استخدام كلمة المرور السابقة.",
            "The previous password cannot be reused."),
        ["CONCURRENCY"] = ("تم تعديل البيانات من جلسة أخرى.", "The data was changed by another session."),
        ["REAUTH_REQUIRED"] = ("أعد إدخال كلمة مرورك للمتابعة.", "Re-enter your password to continue."),
        ["LAST_ADMIN"] = ("لا يمكن تعطيل آخر مسؤول نظام.", "The last system administrator cannot be deactivated."),
        ["COURSE_NOT_FOUND"] = ("الدورة غير موجودة.", "The course was not found."),
        ["COURSE_NOT_APPROVED"] = ("الدورة غير معتمدة بعد.", "The course is not approved yet."),
        ["HISTORICAL_DATE_REQUIRED"] = ("يجب أن يكون تاريخ الدورة السابقة قبل تاريخ اليوم.",
            "A historical course must end before today."),
        ["HISTORICAL_PARTICIPANTS_REQUIRED"] = ("اختر أعضاء الدورة السابقة وحدد حالة كل عضو.",
            "Select the historical course members and set each outcome."),
        ["HISTORICAL_PARTICIPANTS_INVALID"] = ("يجب أن ينتمي جميع الأعضاء إلى الكلية المحددة.",
            "All selected members must belong to the selected faculty."),
        ["HISTORICAL_READ_ONLY"] = ("الدورة السابقة سجل توثيقي؛ لا يمكن إنشاء أو تعديل محاضرات أو امتحانات لها.",
            "A historical course is a read-only record; lectures and exams cannot be created or changed."),
        ["IMPACT_END_REQUIRED"] = ("آخر موعد لقياس الأثر إلزامي عند وجود ناجحين.",
            "An impact deadline is required when there are successful members."),
        ["EVALUATION_REQUIRED"] = ("يجب أن يقيّم العضو الدورة أو يختار المدير إعفاءه من التقييم.",
            "The member must evaluate the course or be exempted by the administrator."),
        ["CERT_TEMPLATE_MISSING"] = ("قالب الشهادة غير متوفر أو أن ملفه مفقود.",
            "The certificate template or its file is missing."),
        ["PROPOSAL_NOT_PENDING"] = ("المقترح لم يعد بانتظار القرار.", "The proposal is no longer pending."),
        ["REG_FULL"] = ("اكتمل عدد المشاركين.", "The course is full."),
        ["REG_ALREADY"] = ("أنت مسجّل في هذه الدورة.", "You are already registered in this course."),
        ["FILE_REQUIRED"] = ("اختر ملفاً للرفع.", "Choose a file to upload."),
        ["FILE_TOO_LARGE"] = ("حجم الملف يتجاوز الحد المسموح.", "The file exceeds the allowed size."),
        ["FILE_TYPE_NOT_ALLOWED"] = ("نوع الملف غير مسموح لهذا الحقل.", "This file type is not allowed for this field."),
        ["FILE_NOT_FOUND"] = ("الملف غير موجود.", "The file was not found."),
        ["FILE_CONTENT_MISSING"] = ("محتوى الملف غير متوفر.", "The file content is missing."),
        ["LOGIN_EXISTS"] = ("رقم الدخول مستخدم مسبقاً.", "The login name is already used."),
        ["USER_NOT_FOUND"] = ("المستخدم غير موجود.", "The user was not found."),
        ["SETTING_NOT_FOUND"] = ("الإعداد غير موجود.", "The setting was not found."),
        ["SIDE_NOT_FOUND"] = ("جهة المزامنة غير موجودة.", "The sync side was not found."),
        ["REVIEW_NOT_FOUND"] = ("عنصر المراجعة غير موجود.", "The review item was not found."),
        ["REVIEW_ALREADY_RESOLVED"] = ("تمت معالجة عنصر المراجعة سابقاً.",
            "The review item was already resolved."),
        ["SYNC_RUN_NOT_FOUND"] = ("عملية المزامنة غير موجودة.", "The sync run was not found."),
        ["SYNC_ALREADY_RUNNING"] = ("توجد مزامنة قيد التنفيذ.", "A sync run is already in progress."),
        ["CENTER_NOT_FOUND"] = ("المركز غير موجود.", "The center was not found."),
        ["EXTERNAL_REQUEST_NOT_FOUND"] = ("طلب المدرب الخارجي غير موجود.",
            "The external instructor request was not found."),
        ["EXTERNAL_REQUEST_NOT_PENDING"] = ("الطلب لم يعد بانتظار المعالجة.",
            "The request is no longer pending."),
        ["EXTERNAL_ACCOUNT_PENDING"] = ("الدورة بانتظار إنشاء حساب المدرب الخارجي؛ أكمل الطلب من إدارة المستخدمين.",
            "The course is waiting for an external instructor account. Complete the request in user administration."),
        ["BLOCKED_PASSWORD_NOT_FOUND"] = ("كلمة المرور المحظورة غير موجودة.",
            "The blocked password was not found."),
        ["BLOCKED_PASSWORD_EXISTS"] = ("كلمة المرور محظورة مسبقاً.", "The password is already blocked."),
        ["CONTEXT_INVALID"] = ("السياق المطلوب غير متاح لحسابك.", "The requested context is not available to you."),
        ["HINT_NOT_FOUND"] = ("التلميح غير موجود.", "The hint was not found."),
        ["HINT_EXISTS"] = ("يوجد تلميح بهذا المفتاح والسياق.", "A hint with this key and context already exists."),
    };

    public static string Error(string code, CultureInfo culture)
    {
        if (Catalog.TryGetValue(code, out var text))
            return culture.TwoLetterISOLanguageName == "en" ? text.En : text.Ar;
        return culture.TwoLetterISOLanguageName == "en"
            ? "The request could not be completed."
            : "تعذّر إكمال الطلب.";
    }

    public static string DefaultCode(int statusCode) => statusCode switch
    {
        400 => "BAD_REQUEST",
        401 => "UNAUTHORIZED",
        403 => "FORBIDDEN",
        404 => "NOT_FOUND",
        405 => "METHOD_NOT_ALLOWED",
        409 => "CONFLICT",
        422 => "VALIDATION_FAILED",
        429 => "RATE_LIMITED",
        >= 500 => "INTERNAL_ERROR",
        _ => "BAD_REQUEST"
    };
}
