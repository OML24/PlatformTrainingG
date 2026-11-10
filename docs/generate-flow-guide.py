"""Generate the Arabic, source-linked implementation guide from the current web code."""
from __future__ import annotations

import html
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
WEB = ROOT / "src/ZuLms.Web"
OUT = Path(__file__).with_name("flow-guide.html")


def esc(value):
    return html.escape(str(value), quote=True)


def source(path, line=1):
    return f'../{path.relative_to(ROOT).as_posix()}#L{line}'


def link(path, line, label=None):
    return f'<a href="{esc(source(path, line))}" target="_blank">{esc(label or path.name)}:{line}</a>'


def anchor(value):
    return re.sub(r"[^a-z0-9]+", "-", value.lower()).strip("-")


def route_key(value):
    value = re.sub(r"\$\{[^}]+\}", "{}", value)
    value = re.sub(r"\{[^}]+\}", "{}", value)
    value = re.sub(r":(long|int|guid)", "", value)
    return "/" + value.strip("/")


controllers = []
for path in sorted((WEB / "Api").glob("*Controller.cs")):
    lines = path.read_text(encoding="utf-8-sig").splitlines()
    base = ""
    owner = ""
    service = ""
    permission = ""
    pending = None
    for n, line in enumerate(lines, 1):
        m = re.search(r'\[Route\("([^"]+)"\)\]', line)
        if m: base = m.group(1)
        m = re.search(r'public sealed class (\w+Controller)\((I\w+Service)\s+(\w+)\)', line)
        if m: owner, service, variable = m.groups()
        m = re.search(r'\[Http(Get|Post|Put|Patch|Delete)(?:\("([^"]*)"\))?\]', line)
        if m: pending = (m.group(1).upper(), m.group(2) or "", n)
        m = re.search(r'\[RequirePermission\("([^"]+)"\)\]', line)
        if m: permission = m.group(1)
        if pending and re.search(r'public\s+(?:async\s+)?(?:Task<[^>]+>|IActionResult|ActionResult[^\s]*|Task|FileResult)\s+(\w+)\s*\(', line):
            method = re.search(r'\s(\w+)\s*\(', line).group(1)
            verb, suffix, attrline = pending
            body = "\n".join(lines[n-1:min(n+32, len(lines))])
            calls = re.findall(r'await\s+(\w+)\.(\w+Async)\s*\(', body)
            appcall = next((f"{a}.{b}" for a, b in calls if a not in ("db", "HttpContext")), "")
            controllers.append(dict(verb=verb, route=(base.rstrip("/") + ("/" + suffix if suffix else "")), method=method,
                owner=owner, service=service, appcall=appcall, permission=permission, path=path, line=n))
            pending = None
            permission = ""


def find_endpoint(verb, url):
    key = route_key(url.split("?")[0])
    matches = [x for x in controllers if x["verb"] == verb and route_key(x["route"]) == key]
    return matches[0] if matches else None


service_files = {p.stem: p for p in (ROOT / 'src/ZuLms.Application').rglob('*Service.cs')}
def service_detail(ep):
    if not ep or not ep['appcall'] or not ep['service']: return ''
    name = ep['service'].removeprefix('I')
    path = service_files.get(name)
    method = ep['appcall'].split('.')[-1]
    if not path: return ''
    lines = path.read_text(encoding='utf-8-sig').splitlines()
    start = next((i for i, line in enumerate(lines) if re.search(r'\b' + re.escape(method) + r'\s*\(', line)), None)
    if start is None: return ''
    selected = []
    for i in range(start, min(start + 85, len(lines))):
        raw = lines[i].strip()
        if i > start + 2 and re.search(r'^public\s+(?:async\s+)?(?:Task|Result|void)', raw): break
        if any(token in raw for token in ('if (', 'Failure(', 'SaveChangesAsync(', 'InSerializableTransactionAsync(',
             'audit.LogAsync(', 'Notify(', '.Add(', '.AddRange(', '.Status =', '.IsActive =', 'return Result')):
            selected.append((i+1, raw))
        if len(selected) >= 9: break
    if not selected: return ''
    excerpt = '\n'.join(f'{n}: {line}' for n, line in selected)
    return f'<details class="service-detail"><summary>افتح شروط وآثار {esc(method)} من الكود</summary><p>{link(path, start+1, name + "." + method)}</p><pre><code>{esc(excerpt)}</code></pre></details>'


pages = []
page_files = list((WEB / "Pages").rglob("*.cshtml")) + list((WEB / "Areas").rglob("*.cshtml"))
for path in sorted(page_files):
    if path.name.startswith("_"): continue
    contents = path.read_text(encoding="utf-8-sig")
    route = re.search(r'^@page(?:\s+"([^"]+)")?', contents, re.M)
    if not route: continue
    if route.group(1): page_route = route.group(1)
    elif (WEB / 'Pages') in path.parents: page_route = "/" + path.relative_to(WEB / "Pages").with_suffix("").as_posix()
    else: page_route = "/" + path.stem.lower()
    scripts = re.findall(r'~/js/([\w-]+\.js)', contents)
    title = re.search(r'ViewData\["Title"\]\s*=\s*"([^"]+)"', contents)
    controls = []
    for n, line in enumerate(contents.splitlines(), 1):
        for tag, label in re.findall(r'<(button|a)[^>]*>([^<]{2,80})</(?:button|a)>', line):
            label = re.sub(r'\s+', ' ', label).strip()
            if label and not label.startswith('@'): controls.append((label, n))
    pages.append(dict(path=path, route=page_route, title=title.group(1) if title else path.stem,
                      scripts=scripts, controls=controls))

js_to_pages = {}
for page in pages:
    for script in page["scripts"]:
        js_to_pages.setdefault(script, []).append(page)

calls = []
pattern = re.compile(r'(?:ZuLms\.api\.|\brequest\s*\()(get|post|put|patch|del|upload|request)?')
for path in sorted((WEB / "wwwroot/js").glob("*.js")):
    lines = path.read_text(encoding="utf-8-sig").splitlines()
    for n, line in enumerate(lines, 1):
        m = re.search(r'(?:ZuLms\.api\.(get|post|put|patch|del|upload|request)|\brequest)\s*\(\s*(?:["\'`]((?:GET|POST|PUT|PATCH|DELETE))["\'`]\s*,\s*)?(["\'`])(/api/v1/[^"\'`]+)\3', line, re.I)
        if not m: continue
        operation, explicit, _, url = m.groups()
        trailing = line[m.end():]
        after_url_verb = re.match(r'\s*,\s*["\'`](GET|POST|PUT|PATCH|DELETE)["\'`]', trailing, re.I)
        verb = (explicit or (after_url_verb.group(1) if after_url_verb else None) or {"get":"GET", "post":"POST", "put":"PUT", "patch":"PATCH", "del":"DELETE", "upload":"POST"}.get((operation or "").lower(), "GET")).upper()
        endpoint = find_endpoint(verb, url)
        preceding = "\n".join(lines[max(0, n-35):n])
        fn = re.findall(r'(?:async\s+)?function\s+(\w+)\s*\(|(?:const|let)\s+(\w+)\s*=\s*async\s*\(', preceding)
        event = re.findall(r'addEventListener\(\s*["\'`](click|submit|change|input)["\'`]', preceding)
        labels = re.findall(r'(?:textContent\s*=|actionButton\(|link\()\s*["\'`]([^"\'`]{2,70})', preceding)
        controls = js_to_pages.get(path.name, [])
        calls.append(dict(path=path, line=n, verb=verb, url=url, endpoint=endpoint,
            fn=next((a or b for a,b in reversed(fn)), ""), event=event[-1] if event else "",
            label=labels[-1] if labels else "", pages=controls,
            code=line.strip()))

story = [
 ("الدخول والجلسة", "auth", "نموذج الدخول يرسل بياناته من auth.js إلى /api/v1/auth/login. النتيجة تعطي sessionId وredirectUrl؛ يتم تفعيل تبويب الجلسة قبل الانتقال. كل طلب لاحق يمر من api.js الذي يضيف الكوكي وCSRF، ويجدد الجلسة مرة واحدة عند 401، ثم يحوّل إلى الدخول عند الفشل. تغيير كلمة المرور الإجباري يمر عبر ForcedPasswordChangeMiddleware.", "auth.js", "AuthService"),
 ("التنقل والصلاحيات", "routing", "Razor Pages تعرض الصفحة وفق مسارها، بما فيه aliases للدورات حسب الدور. Program.cs يربط المسارات ويطبق سياسات permission:*. واجهة القائمة في _Layout.cshtml، وPermissionTagHelper يضبط ظهور العناصر؛ الخادم يعيد فحص الصلاحية عند كل API. 403 في طلب HTML يوجّه إلى access-denied، و404 يعرض not-found مع بقاء كود 404.", "shell.js", "PermissionService"),
 ("مقترح العميد واعتماد الدورة", "proposals", "العميد ينشئ المقترح ويرفق الوثائق، ثم الجودة تراجع الطلب. قرار الإرجاع يعيد المسودة للتعديل، والاعتماد ينقل الدورة إلى التشغيل. حساب المحاضر الخارجي يمر بطلب مستقل إذا لزم. صفحات Dean/Propose وQuality/Proposals وQuality/ExternalInstructors تقود هذا المسار.", "dean-proposals.js", "CourseProposalService"),
 ("إنشاء الدورة وإدارتها", "courses", "الجودة أو الأدمن يختار البيانات والمحاضر والفئة ويُنشئ دورة مباشرة؛ هناك مسار خاص للدورات التاريخية. دليل الدورات يقرأ حسب الدور والفلاتر. صفحة التفاصيل تجمع تعديل البيانات، تغيير الحالة، المشاركين، المحاضرات، التقييم، الأثر والملفات، وتظهر الأفعال حسب الحالة والصلاحية.", "course-details.js", "CourseDirectoryService"),
 ("التسجيل والمشاركون", "participants", "العضو يفتح الدورات المتاحة ويطلب التسجيل الذاتي. يمكن للمصرّح له إضافة مشارك يدويًا. الـ Service يتحقق من أهلية الدورة والفئة وعدم تكرار المشاركة ثم يحفظ السجل؛ تظهر النتيجة في قائمة المسجلين والتقدم.", "available-courses.js", "SelfRegistrationService"),
 ("المحاضرات والحضور", "attendance", "المحاضر ينشئ المحاضرة ويولّد كود حضور، والعضو يدخل الكود ضمن شروط الوقت والدورة. يمكن تصحيح الحضور يدويًا حسب الصلاحية. تحديث الحضور يؤثر في نسبة التقدم وأهلية الشهادة.", "course-lectures.js", "AttendanceCodeService"),
 ("الامتحان والتقييم", "evaluation", "من صفحة المحاضرات يدير المحاضر الامتحان والنتائج. الجودة تنشر نسخة نموذج التقييم، والعضو يجيب على النسخة المرتبطة بدورته. التقارير تجمع الإجابات حسب النسخ؛ استحقاق الشهادة يعيد فحص شرط الامتحان والتقييم.", "quality-eval-form.js", "EvaluationService"),
 ("الأثر والشهادات", "certificates", "المشارك يرفع ملف الأثر، والمحاضر يراجعه. بعد اكتمال شروط الحضور والامتحان والتقييم والأثر، تعرض الجودة الشهادات المعلقة لإصدارها. العضو يرى شهاداته ويمكن التحقق من رقم الشهادة علنًا؛ القالب يدار من صفحة الأدمن.", "member-impact.js", "CertificateService"),
 ("طلب الإلغاء والقرار", "cancellation", "العميد يملأ السبب ويرفع الكتاب أولًا عبر ملفات API ثم يرسل طلب الإلغاء. تظل الدورة سارية إلى أن تفتح الجودة قائمة الطلبات وتختار موافقة أو رفض. الرفض يطلب ملاحظة، والموافقة تعرض تأكيد إلغاء الدورة وشهاداتها، ثم تنادي DecideAsync. الإلغاء المباشر مسار منفصل مع كتاب إلزامي.", "cancel-request.js", "CourseCancellationService"),
 ("الاحتياجات والشهادات المهنية", "needs", "العميد يرفع احتياجًا تدريبيًا والجودة أو الأدمن يراجعان الحالة. الشهادات المهنية لها طلبات وقرارات وإحصاءات مستقلة. كل شاشة تتعامل مع API مخصص وتُحدَّث القوائم بعد الحفظ.", "dean-needs.js", "NeedsService"),
 ("الإدارة والرقابة", "admin", "الأدمن يدير المستخدمين، الأدوار، الإعدادات، المراكز، مزامنة الجامعة، سياسة كلمة المرور وقالب الشهادة. سجل التدقيق والبث المباشر يعرضان ما حدث. العمليات الحساسة تُفحص صلاحياتها في Controller وService وتُسجّل نتائجها.", "admin-roles.js", "UserService"),
]

def card_call(x, idx):
    ep = x["endpoint"]
    page_links = ", ".join(link(p["path"], 1, p["title"]) for p in x["pages"]) or "يُستخدم من واجهة مشتركة/ديناميكية"
    label = x["label"] or ("إرسال نموذج" if x["event"] == "submit" else "تحميل/تنفيذ")
    service = (f'{esc(ep["service"])} → <b>{esc(ep["appcall"] or ep["method"])}</b>' if ep else "راجع endpoint يدويًا؛ قد يكون Minimal API أو مسارًا محسوبًا وقت التشغيل")
    endpoint_link = link(ep["path"], ep["line"], ep["owner"] + "." + ep["method"]) if ep else "—"
    perm = esc(ep["permission"]) if ep and ep["permission"] else "حسب سياسة المسار/الجلسة"
    search = esc(" ".join([x["path"].stem, x["url"], x["fn"], label, ep["service"] if ep else "", " ".join(p["title"] for p in x["pages"])]).lower())
    return f'''<article class="call" data-search="{search}" data-group="{esc(x['path'].stem)}">
      <div class="call-head"><span class="method {x['verb'].lower()}">{x['verb']}</span><h4>{esc(label)}</h4><span class="call-no">#{idx:03d}</span></div>
      <div class="call-route"><code>{esc(x['url'])}</code></div>
      <ol class="steps"><li><b>الصفحة/الحدث:</b> {page_links}؛ {esc(x['event'] or 'تحميل الصفحة/استدعاء داخلي')}.</li>
      <li><b>واجهة JavaScript:</b> {link(x['path'], x['line'], x['path'].name + ('.' + x['fn'] if x['fn'] else ''))} تستدعي <code>ZuLms.api</code>. <code>{esc(x['code'])}</code></li>
      <li><b>الراوت والميثود:</b> {endpoint_link}؛ الصلاحية: <code>{perm}</code>.</li>
      <li><b>التطبيق:</b> {service}. بعدها تعود الاستجابة إلى Promise في الواجهة؛ النجاح يحدّث الشاشة وفق الكود المرتبط، والفشل يمر عبر ApiError/رسالة الصفحة.</li></ol>{service_detail(ep)}</article>'''

sections = []
for title, key, description, js, service in story:
    matching = [x for x in calls if key in x["path"].stem or (key == "courses" and "course" in x["path"].stem)]
    js_path = WEB / 'wwwroot/js' / js
    js_ref = link(js_path, 1, js) if js_path.exists() else esc(js) + ' (ملف/مسار مفهومي)'
    sections.append(f'<article class="story" id="flow-{key}"><div class="story-icon">{len(sections)+1:02d}</div><div><h3>{esc(title)}</h3><p>{esc(description)}</p><div class="source-line">اقرأ: {js_ref} · {esc(service)}</div></div></article>')

page_html = []
for p in pages:
    buttons = ", ".join(f'{esc(label)} {link(p["path"], n, "↗")}' for label, n in p["controls"][:16]) or "عناصر تفاعلية تُنشأ عبر JavaScript أو صفحة عرض"
    scripts = ", ".join(esc(s) for s in p["scripts"]) or "السكربتات المشتركة في Layout"
    page_html.append(f'<tr><td><a href="{esc(source(p["path"]))}">{esc(p["title"])}</a></td><td><code>{esc(p["route"])}</code></td><td>{scripts}</td><td>{buttons}</td></tr>')

endpoint_html = []
for ep in controllers:
    endpoint_html.append(f'<tr><td><span class="method {ep["verb"].lower()}">{ep["verb"]}</span></td><td><code>{esc(ep["route"])}</code></td><td>{link(ep["path"], ep["line"], ep["owner"] + "." + ep["method"])}</td><td>{esc(ep["appcall"] or ep["service"])}</td><td><code>{esc(ep["permission"] or "—")}</code></td></tr>')

deep_dive = f'''<section class="section" id="examples"><div class="section-title"><span>03 / TRACE EXAMPLES</span><h2>تتبّع عملي مفصل</h2><p>أمثلة تبيّن كيف تقرأ أي كبسة من أول حدث إلى أثرها النهائي.</p></div>
<div class="trace"><h3>مثال 1: كبسة «تسجيل الدخول»</h3><ol>
<li><b>الصفحة:</b> نموذج الدخول في {link(WEB/'Pages/Areas/Public/Pages/Login.cshtml' if (WEB/'Pages/Areas/Public/Pages/Login.cshtml').exists() else WEB/'Areas/Public/Pages/Login.cshtml', 1, 'Login.cshtml')} يحدد <code>data-api</code>. عند submit، {link(WEB/'wwwroot/js/auth.js', 1, 'auth.js')} يمنع إرسال HTML الافتراضي ويمنع الضغط المكرر ويجمع FormData مع rememberMe.</li>
<li><b>طلب الشبكة:</b> <code>ZuLms.api.post(form.dataset.api,payload)</code> يمر على {link(WEB/'wwwroot/js/api.js', 1, 'api.js')}؛ يضيف CSRF والكوكي، ويحوّل الجسم إلى JSON. الراوت <code>POST /api/v1/auth/login</code> معرّف في {link(WEB/'Security/AuthEndpoints.cs', 20, 'AuthEndpoints.cs')}.</li>
<li><b>القواعد:</b> <code>AuthService.LoginAsync</code> في {link(ROOT/'src/ZuLms.Application/Features/Auth/AuthService.cs', 21, 'AuthService.cs')} يطبّع اسم الدخول، يراجع القفل وكلمة المرور ونشاط المستخدم وأهلية الموظف، ثم يصدر جلسة محدودة عند أول دخول أو الحاجة لتغيير كلمة المرور. يسجل نجاح الدخول ويحدّث LastLoginAt.</li>
<li><b>الرجوع:</b> endpoint يضبط جلسة المصادقة ويرجع <code>sessionId</code> و<code>redirectUrl</code>. {link(WEB/'wwwroot/js/auth.js', 16, 'auth.js')} يفعل sessionId للتبويب ثم ينقل المستخدم. الخطأ يظهر في <code>auth-message</code> ويُعاد تمكين الزر.</li></ol></div>
<div class="trace"><h3>مثال 2: كبسة «إرسال طلب الإلغاء»</h3><ol>
<li><b>الواجهة:</b> {link(WEB/'Pages/Courses/CancelRequest.cshtml', 1, 'CancelRequest.cshtml')} تعرض السبب والكتاب والزر. {link(WEB/'wwwroot/js/cancel-request.js', 5, 'cancel-request.js')} يمنع submit الافتراضي، يعطّل الزر، ويخفي الرسائل القديمة.</li>
<li><b>رفع الملف:</b> إن اختير كتاب، يبني <code>FormData(kind=CancellationLetter,file)</code> ويرسله إلى <code>POST /api/v1/files</code>. نتيجة الرفع تعطي <code>fileId</code>. من دون ملف يبقى <code>null</code>؛ الخادم يقرر إلزاميته حسب الإعداد.</li>
<li><b>إرسال الطلب:</b> يرسل <code>POST /api/v1/courses/{{courseId}}/cancellation/request</code> مع <code>reason,letterFileId</code>. {link(WEB/'Api/CourseCancellationController.cs', 49, 'CourseCancellationActionController.SubmitRequest')} يستخرج actor ثم ينادي <code>CourseCancellationService.RequestAsync</code>.</li>
<li><b>داخل Service:</b> {link(ROOT/'src/ZuLms.Application/Features/Courses/CourseCancellationService.cs', 16, 'RequestAsync')} يتحقق من الإذن والسبب والكتاب، ثم في transaction يفحص أن العميد ضمن نطاق الكلية، والدورة Approved، ولا يوجد طلب Pending. يحفظ طلبًا جديدًا، يربط الكتاب بالدورة، ينشئ إشعارات الجودة، ويسجّل Audit.</li>
<li><b>النتيجة:</b> Controller يرجع <code>201 Created</code> مع رقم الطلب. الواجهة تظهر نجاحًا وتخفي النموذج. الدورة تبقى Approved حتى قرار الجودة.</li></ol></div>
<div class="trace"><h3>مثال 3: كبستا «موافقة / رفض» عند الجودة</h3><ol>
<li><b>قراءة القائمة:</b> {link(WEB/'wwwroot/js/quality-cancellations.js', 10, 'quality-cancellations.js')} ينادي <code>GET /api/v1/cancellations/pending</code>. <code>PendingAsync</code> يعيد الطلبات المعلقة مرتبة زمنيًا مع بيانات الدورة والعميد والكتاب.</li>
<li><b>الكبسة:</b> الزران يُنشآن لكل صف. الرفض يطلب سببًا إلزاميًا؛ الموافقة تعرض تأكيدًا يذكر إلغاء الدورة وشهاداتها. بعد التأكيد يرسل <code>POST /api/v1/cancellations/{{id}}/decision</code> مع <code>approve,notes</code>.</li>
<li><b>قرار الخادم:</b> {link(WEB/'Api/CourseCancellationController.cs', 23, 'Decide')} ينادي {link(ROOT/'src/ZuLms.Application/Features/Courses/CourseCancellationService.cs', 94, 'DecideAsync')}. يتحقق من صلاحية القرار، حالة الطلب Pending، وحالة الدورة Approved. الرفض يحفظ Rejected؛ الموافقة تستدعي <code>CancelAsync</code>.</li>
<li><b>الأثر عند الموافقة:</b> {link(ROOT/'src/ZuLms.Application/Features/Courses/CourseCancellationService.cs', 171, 'CancelAsync')} يغيّر حالة الدورة إلى Cancelled ويؤرشفها، يضيف سجل انتقال الحالة، يسحب الشهادات الصادرة، ويبلغ المشاركين والمحاضر والعميد. يُسجّل Audit ثم SaveChanges داخل transaction.</li>
<li><b>الرجوع:</b> الواجهة تعرض «حُفظ القرار» وتعيد <code>load()</code> حتى تختفي الطلبات التي حُسمت؛ الخطأ يعرض code/message ويعيد تمكين الزر.</li></ol></div>
</section>'''

html_doc = f'''<!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>دليل فلو منصة ZU LMS</title><link rel="stylesheet" href="flow-guide.css"></head><body>
<aside class="sidebar"><div class="brand"><span class="brand-mark">ZU</span><div><strong>دليل المنصة</strong><small>Developer flow atlas</small></div></div><nav aria-label="فهرس الدليل"><a href="#intro">نظرة عامة</a><a href="#foundation">قبل الكبسات: الأساس</a><a href="#flows">الفلو حسب المجال</a><a href="#examples">أمثلة تفصيلية</a><a href="#actions">كل استدعاءات الواجهة</a><a href="#pages">خريطة الصفحات</a><a href="#endpoints">خريطة API</a><a href="#limits">طريقة القراءة وحدودها</a></nav><div class="side-note">مبني من كود <code>newnew/zu-lms</code><br>توليد: <time>2026-11-08</time></div></aside>
<main><header class="hero" id="intro"><div class="eyebrow">ZU LMS · SOURCE-BASED GUIDE</div><h1>كيف يمشي كل شيء<br><em>من الكبسة إلى قاعدة البيانات</em></h1><p>مرجع عربي للمبرمج: أين تبدأ الواجهة، أي ميثود تنادي، كيف يعالج الخادم الطلب، وماذا يحدث بعد الاستجابة. مبني على ملفات التطبيق الفعلية، مع روابط للصفحات والأسطر.</p><div class="stats"><div><strong>{len(pages)}</strong><span>صفحة Razor</span></div><div><strong>{len(calls)}</strong><span>استدعاء API من JS</span></div><div><strong>{len(controllers)}</strong><span>ميثود Controller</span></div><div><strong>{len(story)}</strong><span>فلو رئيسي</span></div></div></header>
<section id="foundation" class="section"><div class="section-title"><span>01 / ARCHITECTURE</span><h2>قبل أي كبسة: الأساس المشترك</h2><p>هذه الطبقات تتكرر في معظم عمليات الموقع، لذلك اقرأها أولًا.</p></div>
<div class="architecture"><div><b>01 · Browser</b><p>Razor Page تبني HTML وبيانات الصفحة، ثم سكربت الصفحة يربط الأحداث. <code>_Layout.cshtml</code> يحمّل api.js وshell.js والإشعارات والترجمة.</p></div><div><b>02 · api.js</b><p><code>ZuLms.api.request(method,url,body)</code> يرسل JSON أو FormData مع الكوكي وCSRF. يعيد محاولة CSRF مرة، ويجدّد الجلسة مرة عند 401. يفك <code>{{ok,data}}</code> إلى data ويحوّل الخطأ إلى <code>ApiError</code>.</p></div><div><b>03 · Routing & security</b><p><code>Program.cs</code> يضبط aliases وAuthorizePage، ثم middleware للجلسة وتجديدها وتغيير كلمة المرور، فالمصادقة والتفويض. <code>MapControllers</code> يطابق Route + HttpVerb؛ <code>RequirePermission</code> يفحص الإذن مجددًا.</p></div><div><b>04 · Application → Persistence</b><p>Controller يستخرج actor/input وينادي Service في Application. الـ Service يطبق القواعد ثم يستخدم Infrastructure/EF Core للحفظ والقراءة. النتيجة ترجع HTTP؛ JavaScript يعرض نجاحًا أو خطأً ويعيد تحميل الجزء المناسب.</p></div></div>
<div class="note"><b>تسلسل الطلب النموذجي</b><code>button / form → event listener → ZuLms.api → HTTP route → Controller.Method → ApplicationService.MethodAsync → DbContext → response → render/toast/navigation</code><p>بعض الصفحات تولّد الأزرار ديناميكيًا؛ لذلك الرابط بين اسم الكبسة والـ API ظاهر في بطاقة الاستدعاء مع سطر JS نفسه.</p></div>
<div class="foundation-grid"><div><h3>الجلسة والأمان</h3><p>المصادقة تعتمد كوكي JWT وCSRF للطلبات المعدّلة. <code>TabSessionMiddleware</code> يعزل سياق التبويب، و<code>SilentRefreshMiddleware</code> يجدد الجلسة. صفحات HTML الممنوعة تتحول لصفحة منع الوصول؛ API يحتفظ بكود الخطأ.</p><p>{link(WEB/'Program.cs', 1, 'Program.cs')} · {link(WEB/'wwwroot/js/api.js', 1, 'api.js')}</p></div><div><h3>الصلاحيات والمظهر</h3><p>وجود زر لا يساوي إذنًا. Razor policy وController permission وService rules طبقات متعاقبة. القائمة تتغير حسب الدور النشط؛ حقول الأخطاء تُربط بالاسم عبر <code>applyFieldErrors</code>.</p><p>{link(WEB/'Pages/Shared/_Layout.cshtml', 1, '_Layout.cshtml')} · {link(WEB/'TagHelpers/PermissionTagHelper.cs', 1, 'PermissionTagHelper.cs')}</p></div><div><h3>مسارات إضافية</h3><p>هناك Minimal API للمصادقة والسياق والتحقق والصحة عبر <code>MapAuthEndpoints/MapContextEndpoints/MapVerifyEndpoints/MapHealthEndpoints</code>، إلى جانب Controllers. ولهذا لا يعني غياب Controller في فهرس الاستدعاءات أن الطلب بلا تنفيذ.</p><p>{link(WEB/'Program.cs', 1, 'Program.cs')}</p></div></div></section>
<section id="flows" class="section"><div class="section-title"><span>02 / BUSINESS FLOWS</span><h2>الفلو الكامل حسب المجال</h2><p>اقرأ القصة، ثم افتح بطاقات الاستدعاءات أسفلها لتتبع الميثودات الدقيقة.</p></div><div class="stories">{''.join(sections)}</div></section>{deep_dive}
<section id="actions" class="section"><div class="section-title"><span>03 / UI → API</span><h2>كل استدعاء من الواجهة</h2><p>هذه البطاقات مولّدة من استدعاءات JS الصريحة. ابحث باسم الصفحة، الميثود، المسار أو السكربت. كل بطاقة تعرض الواجهة ← Controller ← Service ← النتيجة.</p></div><div class="toolbar"><input id="search" type="search" placeholder="ابحث: إلغاء، courses، approve، auth…" aria-label="بحث الاستدعاءات"><select id="group"><option value="">كل السكربتات</option>{''.join(f'<option value="{esc(name)}">{esc(name)}.js</option>' for name in sorted(set(x['path'].stem for x in calls)))}</select><span id="visible-count"></span></div><div class="calls">{''.join(card_call(x,i) for i,x in enumerate(calls,1))}</div><p class="empty" id="empty" hidden>لا توجد نتائج مطابقة.</p></section>
<section id="pages" class="section"><div class="section-title"><span>04 / RAZOR ROUTES</span><h2>خريطة الصفحات والكبسات الظاهرة</h2><p>المسار من <code>@page</code> أو اصطلاح Razor. مسارات alias الإضافية مذكورة في Program.cs، وبعض الأزرار تُنشأ داخل JavaScript وتظهر في بطاقات الاستدعاءات.</p></div><div class="table-wrap"><table><thead><tr><th>الصفحة</th><th>المسار</th><th>سكربت الصفحة</th><th>الأزرار/الروابط في HTML</th></tr></thead><tbody>{''.join(page_html)}</tbody></table></div></section>
<section id="endpoints" class="section"><div class="section-title"><span>05 / SERVER METHODS</span><h2>خريطة Controllers والميثودات</h2><p>المسارات التالية مستخرجة من Attributes على Controllers. راجع الملفات المرتبطة لفهم شروط كل Service وإرجاعاتها.</p></div><div class="table-wrap"><table><thead><tr><th>HTTP</th><th>Route</th><th>Controller.Method</th><th>Service call</th><th>إذن</th></tr></thead><tbody>{''.join(endpoint_html)}</tbody></table></div></section>
<section id="limits" class="section"><div class="section-title"><span>06 / READ ME</span><h2>طريقة استخدام الدليل</h2></div><div class="note"><p>ابدأ بالأساس، اختر الفلو، ثم ابحث عن الكبسة في الاستدعاءات. افتح سطر JavaScript لتقرأ الحقول وشروط العرض وتحديث الواجهة، ثم Controller ثم Service. الفهرس آلي ويغطي الاستدعاءات ذات URL صريح؛ المسارات المبنية في متغيرات، الروابط المباشرة، وMinimal APIs تحتاج مراجعة الملف المرتبط. وصف الفلو يلخّص السلوك ولا يحل محل شروط الكود التنفيذية.</p><p>إعادة التوليد بعد تغيير الكود: <code>python docs/generate-flow-guide.py</code>.</p></div></section><footer>ZU LMS · دليل تنفيذ للمطورين · 2026</footer></main><script src="flow-guide.js"></script></body></html>'''
OUT.write_text(html_doc, encoding="utf-8")
print(f"Generated {OUT}: {len(pages)} pages, {len(calls)} JS calls, {len(controllers)} endpoints")
