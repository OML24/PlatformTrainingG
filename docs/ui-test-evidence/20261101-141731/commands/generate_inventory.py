import csv
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
OUT = Path(__file__).resolve().parents[1]
WEB = ROOT / "src" / "ZuLms.Web"

required = [
    ("PUB-LOGIN", "08 §2.1", "/login", "Public", "anonymous", "Global"),
    ("PUB-FORGOT", "08 §2.1", "/forgot-password", "Public", "anonymous", "Global"),
    ("PUB-CHANGE", "08 §2.1", "/change-password", "Public", "authenticated", "Self"),
    ("PUB-VERIFY", "08 §2.1", "/verify?cert=", "Public", "anonymous", "Global"),
    ("PUB-DENIED", "08 §2.1", "/access-denied", "Public", "anonymous", "Global"),
    ("PUB-ERROR", "08 §2.1", "/error", "Public", "anonymous", "Global"),
    ("ADM-DASH", "08 §2.2", "/admin/dashboard", "Admin", "page.admin.dashboard", "Global"),
    ("ADM-USERS", "08 §2.2", "/admin/users", "Admin", "page.admin.users", "Global"),
    ("ADM-USER", "08 §2.2", "/admin/users/{id}", "Admin", "page.admin.users", "Global"),
    ("ADM-CREDS", "08 §2.2", "/admin/credentials", "Admin", "page.admin.credentials", "Global"),
    ("ADM-COURSES", "08 §2.2", "/admin/courses", "Admin", "page.admin.courses", "Global"),
    ("ADM-COURSE-NEW", "08 §2.2", "/admin/courses/new", "Admin", "course.create_direct", "Global"),
    ("ADM-COURSE", "08 §2.2", "/admin/courses/{id}", "Admin", "page.admin.courses", "Global"),
    ("VAULT", "08 §2.2", "/vault", "Admin/Quality", "page.vault", "Global"),
    ("ADM-AUDIT", "08 §2.2", "/admin/audit", "Admin", "page.admin.audit", "Global"),
    ("ADM-LIVE", "08 §2.2", "/admin/live-feed", "Admin", "page.admin.live_feed", "Global"),
    ("ADM-SETTINGS", "08 §2.2", "/admin/settings", "Admin", "page.admin.settings", "Global"),
    ("ADM-POLICY", "08 §2.2", "/admin/password-policy", "Admin", "page.admin.password_policy", "Global"),
    ("ADM-HINTS", "08 §2.2", "/admin/hints", "Admin", "page.admin.hints", "Global"),
    ("ADM-ROLES", "08 §2.2", "/admin/roles", "Admin", "page.admin.roles", "Global"),
    ("ADM-CENTERS", "08 §2.2", "/admin/centers", "Admin", "page.admin.centers", "Global"),
    ("ADM-SYNC", "08 §2.2", "/admin/sync", "Admin", "page.admin.sync", "Global"),
    ("ADM-TEMPLATE", "08 §2.2", "/admin/certificate-template", "Admin", "cert_template.upload", "Global"),
    ("ADM-STATS", "08 §2.2", "/admin/stats", "Admin", "page.admin.stats", "Global"),
    ("ADM-NEEDS", "08 §2.2", "/admin/needs", "Admin", "page.admin.needs", "Global"),
    ("QUA-DASH", "08 §2.3", "/quality/dashboard", "Quality", "page.quality.dashboard", "Global"),
    ("QUA-PROPOSALS", "08 §2.3", "/quality/proposals", "Quality", "page.quality.proposals", "Global"),
    ("QUA-CANCEL", "08 §2.3", "/quality/cancellations", "Quality", "cancellation.decide", "Global"),
    ("QUA-COURSES", "08 §2.3", "/quality/courses", "Quality", "page.quality.courses", "Global"),
    ("QUA-NEW", "08 §2.3", "/quality/courses/new", "Quality", "course.create_direct", "Global"),
    ("QUA-HIST", "08 §2.3", "/quality/courses/new-historical", "Quality", "course.create_direct", "Global"),
    ("QUA-COURSE", "08 §2.3", "/quality/courses/{id}", "Quality", "page.quality.courses", "Global"),
    ("QUA-TRACKS", "08 §2.3", "/quality/tracks", "Quality", "page.quality.tracks", "Global"),
    ("QUA-EXT", "08 §2.3", "/quality/external-instructors", "Quality", "page.quality.external_instructors", "Global"),
    ("QUA-NEEDS", "08 §2.3", "/quality/needs", "Quality", "page.quality.needs", "Global"),
    ("QUA-EVALS", "08 §2.3", "/quality/evaluations", "Quality", "page.quality.evaluations", "Global"),
    ("QUA-EVALFORM", "08 §2.3", "/quality/eval-form", "Quality", "page.quality.eval_form", "Global"),
    ("QUA-CERTS", "08 §2.3", "/quality/certificates", "Quality", "page.quality.certificates", "Global"),
    ("QUA-PROCERTS", "08 §2.3", "/quality/pro-certs", "Quality", "page.quality.pro_certs", "Global"),
    ("QUA-PROSTATS", "08 §2.3", "/quality/pro-cert-stats", "Quality", "page.quality.pro_cert_stats", "Global"),
    ("DEA-DASH", "08 §2.4", "/dean/dashboard", "Dean", "page.dean.dashboard", "Faculty"),
    ("DEA-COURSES", "08 §2.4", "/dean/courses", "Dean", "page.dean.courses", "Faculty"),
    ("DEA-PROPOSE", "08 §2.4", "/dean/propose", "Dean", "page.dean.propose", "Faculty"),
    ("DEA-MEMBERS", "08 §2.4", "/dean/members", "Dean", "page.dean.members", "Faculty"),
    ("DEA-NEEDS", "08 §2.4", "/dean/needs", "Dean", "page.dean.needs", "Faculty"),
    ("DEA-STATS", "08 §2.4", "/dean/stats", "Dean", "page.dean.stats", "Faculty"),
    ("INS-COURSES", "08 §2.5", "/instructor/courses", "Instructor", "page.instructor.courses", "InstructedCourse"),
    ("INS-COURSE", "08 §2.5", "/instructor/courses/{id}", "Instructor", "page.instructor.courses", "InstructedCourse"),
    ("INS-LECT", "08 §2.5", "/instructor/courses/{id}/lectures", "Instructor", "lecture.manage", "InstructedCourse"),
    ("INS-ATT", "08 §2.5", "/instructor/courses/{id}/attendance", "Instructor", "attendance.edit_manual", "InstructedCourse"),
    ("INS-PART", "08 §2.5", "/instructor/courses/{id}/participants", "Instructor", "page.instructor.participants", "InstructedCourse"),
    ("INS-EXAM", "08 §2.5", "/instructor/courses/{id}/exam", "Instructor", "exam.manage", "InstructedCourse"),
    ("INS-IMPACT", "08 §2.5", "/instructor/courses/{id}/impact", "Instructor", "impact.review", "InstructedCourse"),
    ("INS-EVAL", "08 §2.5", "/instructor/courses/{id}/evaluation", "Instructor", "evaluation.results.course", "InstructedCourse"),
    ("MEM-DASH", "08 §2.6", "/member/dashboard", "Member", "page.member.dashboard", "Self"),
    ("MEM-AVAILABLE", "08 §2.6", "/member/available", "Member", "participants.self_register", "Self"),
    ("MEM-ENROLLED", "08 §2.6", "/member/enrolled", "Member", "page.member.enrolled", "Self"),
    ("MEM-COURSE", "08 §2.6", "/member/courses/{id}", "Member", "page.member.enrolled", "Self"),
    ("MEM-CODE", "08 §2.6", "/member/enter-code", "Member", "attendance.enter_code", "Self"),
    ("MEM-CERTS", "08 §2.6", "/member/certificates", "Member", "page.member.certificates", "Self"),
    ("MEM-IMPACT", "08 §2.6", "/member/impact", "Member", "page.member.impact", "Self"),
    ("MEM-PROCERTS", "08 §2.6", "/member/pro-certs", "Member", "page.member.pro_certs", "Self"),
    ("MEM-TEACH", "08 §2.6", "/member/teaching", "Member", "page.member.teaching", "Self"),
    ("SHR-DIR", "08 §2.7", "/shared/directory", "Authenticated", "page.shared.directory", "Global"),
    ("SHR-PROFILE", "08 §2.7", "/shared/profile/{userId}", "Authenticated", "page.shared.profile", "Global"),
    ("ACC-PASSWORD", "08 §2.7", "/account/password", "Employee", "authenticated", "Self"),
    ("ACC-SETTINGS", "08 §2.7", "/account/settings", "Authenticated", "authenticated", "Self"),
]

def norm(route):
    route = route.split("?", 1)[0].lower().rstrip("/") or "/"
    route = re.sub(r"\{([^}:]+)(?::[^}]+)?\}", r"{\1}", route)
    route = route.replace("{courseid}", "{id}")
    return route

program = (WEB / "Program.cs").read_text(encoding="utf-8-sig")
permissions = {}
for page, permission in re.findall(r'AuthorizePage\("([^"]+)"(?:,\s*"permission:([^"]+)")?', program):
    permissions[page.lower()] = permission or "authenticated"

actual = []
actual_by_page = {}
for file in sorted(WEB.rglob("*.cshtml")):
    text = file.read_text(encoding="utf-8-sig")
    match = re.search(r'^@page(?:\s+"([^"]+)")?', text, re.M)
    if not match:
        continue
    rel = file.relative_to(WEB).as_posix()
    route = match.group(1)
    if not route:
        stem = file.stem
        if rel.startswith("Areas/Public/Pages/"):
            route = {"Login": "/login", "ForgotPassword": "/forgot-password", "ChangePassword": "/change-password"}.get(stem, "/" + stem.lower())
        else:
            part = file.relative_to(WEB / "Pages").with_suffix("").as_posix()
            route = "/" if part == "Index" else "/" + part.replace("/Index", "").lower()
    scripts = ";".join(sorted(set(re.findall(r'(?:~/|/)js/([^"\']+\.js)', text))))
    page_key = "/" + file.relative_to(WEB / "Pages").with_suffix("").as_posix() if "Pages" in file.parts and not rel.startswith("Areas/") else ""
    permission = permissions.get(page_key.lower(), "anonymous" if rel.startswith("Areas/Public/") else "TBD")
    item = {"route": route, "norm": norm(route), "file": rel, "js": scripts, "permission": permission}
    actual.append(item)
    if page_key:
        actual_by_page[page_key.lower()] = item

for page, route in re.findall(r'AddPageRoute\("([^"]+)",\s*"([^"]+)"\)', program):
    target = actual_by_page.get(page.lower())
    if target and not any(x["norm"] == norm(route) for x in actual):
        actual.append({"route": route, "norm": norm(route), "file": target["file"],
            "js": target["js"], "permission": target["permission"]})

rows = []
matched = set()
for rid, section, route, role, permission, scope in required:
    candidates = [a for a in actual if a["norm"] == norm(route)]
    if candidates:
        a = candidates[0]
        matched.add(a["file"])
        rows.append([rid, section, route, a["route"], a["file"], a["js"], role, permission, scope, "STATIC-FOUND", a["file"]])
    else:
        rows.append([rid, section, route, "", "", "", role, permission, scope, "MISSING-ROUTE", "Static Razor inventory"])
for index, a in enumerate(actual, 1):
    if a["file"] not in matched:
        rows.append([f"ACT-{index:03}", "Actual inventory", "", a["route"], a["file"], a["js"], "TBD", a["permission"], "TBD", "ACTUAL-UNMAPPED", a["file"]])
with (OUT / "route-matrix.csv").open("w", newline="", encoding="utf-8-sig") as f:
    writer = csv.writer(f)
    writer.writerow("id packSection requiredRoute actualRoute razorFile jsFile role permission scope status evidence".split())
    writer.writerows(rows)

route_by_file = {a["file"]: next((r[0] for r in rows if r[4] == a["file"]), "UNMAPPED") for a in actual}
controls = []
tag_pattern = re.compile(r'<(form|input|select|textarea|button|a|dialog)\b([^>]*)>', re.I | re.S)
attr_pattern = re.compile(r'([:@\w-]+)\s*=\s*["\']([^"\']*)["\']')
for file in sorted(WEB.rglob("*.cshtml")):
    rel = file.relative_to(WEB).as_posix()
    text = file.read_text(encoding="utf-8-sig")
    labels = {k: re.sub(r'<[^>]+>', '', v).strip() for k, v in re.findall(r'<label[^>]*for=["\']([^"\']+)["\'][^>]*>(.*?)</label>', text, re.I | re.S)}
    route_id = route_by_file.get(rel, "PARTIAL")
    for number, match in enumerate(tag_pattern.finditer(text), 1):
        tag, attrs_text = match.groups()
        attrs = dict(attr_pattern.findall(attrs_text))
        ident = attrs.get("id") or attrs.get("name") or attrs.get("href")
        if not ident:
            continue
        selector = "#" + attrs["id"] if attrs.get("id") else (f'[name="{attrs["name"]}"]' if attrs.get("name") else attrs.get("href", ""))
        label = labels.get(attrs.get("id", ""), attrs.get("aria-label") or attrs.get("title") or ident)
        event = "submit" if tag.lower() == "form" else ("navigate" if tag.lower() == "a" else "click/change")
        controls.append([route_id, f"{Path(rel).stem}-{number:03}", re.sub(r'\s+', ' ', label), selector, "TBD-runtime", "TBD-runtime", event, "TBD-JS", "TBD-JS", "TBD", "STATIC-INVENTORIED"])
with (OUT / "control-matrix.csv").open("w", newline="", encoding="utf-8-sig") as f:
    writer = csv.writer(f)
    writer.writerow("routeId controlId label selector visibleWhen enabledWhen event requestMethod requestPath caseIds status".split())
    writer.writerows(controls)

js_rows = []
for file in sorted((WEB / "wwwroot/js").rglob("*.js")):
    text = file.read_text(encoding="utf-8-sig")
    rel = file.relative_to(WEB).as_posix()
    ids = sorted(set(re.findall(r'getElementById\(["\'`]([^"\'`]+)', text)))
    events = sorted(set(re.findall(r'addEventListener\(["\'`]([^"\'`]+)', text)))
    requests = sorted(set(" ".join(x) for x in re.findall(r'(?:api\.request|request)\(["\'`](GET|POST|PUT|PATCH|DELETE)["\'`]\s*,\s*["\'`]([^"\'`]+)', text)))
    js_rows.append([rel, ";".join(ids), ";".join(events), ";".join(requests)])
with (OUT / "baseline/js-static-inventory.csv").open("w", newline="", encoding="utf-8-sig") as f:
    writer = csv.writer(f)
    writer.writerow(["jsFile", "literalElementIds", "events", "literalRequests"])
    writer.writerows(js_rows)

print(f"routes={len(rows)} required={len(required)} missing={sum(r[9] == 'MISSING-ROUTE' for r in rows)} controls={len(controls)} js={len(js_rows)}")
