# بيئة جولة UI — 20261101-141731

## الحالة

| البند | القيمة | الحالة |
|---|---|---|
| نظام التشغيل | Windows `10.0.26200.0` | متاح |
| .NET SDK | `10.0.403` | متاح ومطابق لـ `global.json` |
| Node.js | `v24.21.0` | متاح |
| Docker | غير متاح | بوابة PostgreSQL 17/Testcontainers فقط ما زالت `BLOCKED` |
| PostgreSQL على `127.0.0.1:5432` | PostgreSQL 18 محلي | قاعدة منفصلة للجولة؛ لا يثبت قبول PostgreSQL 17 النهائي |
| `ZU_TEST_PG` | ضُبط مؤقتاً لقاعدة منفصلة أثناء الاختبار | PostgreSQL fallback نجح ثم أزيل المتغير |
| `ZU_DB_CONNECTION` | مضبوط داخل عملية التطبيق فقط | قاعدة `zu_lms_ui_20261101_141731` المعزولة |
| `ZU_STORAGE_ROOT` | مضبوط داخل عملية التطبيق فقط | تخزين الجولة تحت مجلد temp المعتمد |
| SMTP اختباري | `127.0.0.1:2525` | sink محلي غير مرحّل؛ لا مصادقة ولا اتصال خارجي |
| تطبيق الجولة | `http://127.0.0.1:5375` | Release مخصص للجولة على قاعدة وتخزين الجولة فقط |

لم تُسجّل قيم أسرار، كلمات مرور، cookies أو storage state في الأدلة.

## خط الأساس

- المستودع الأب يعرض `No commits yet on master` ويرى المشروع كله كملفات untracked؛ لذلك لا يصلح `git diff` وحده كخط أساس.
- حُفظ `baseline/git-status.txt`.
- حُفظت قائمة **1443** ملفاً مع الحجم وSHA-256 في `baseline/source-manifest.{json,csv}`.
- الاستثناءات: `bin`, `obj`, `logs`, `.git`, `.vs`, و`docs/ui-test-evidence`.
- حُفظت قوائم Razor وJavaScript في `baseline/razor-files.txt` و`baseline/app-js-files.txt`.

## بوابات البناء والاختبار

| الأمر | النتيجة | الدليل |
|---|---|---|
| `dotnet restore ZuLms.sln` | `PASS`، exit 0 | `commands/01-dotnet-restore.txt` |
| `dotnet build ZuLms.sln -c Release --no-restore` | `PASS`، 0 تحذير، 0 خطأ | `commands/02-dotnet-build-release.txt` |
| `dotnet test ZuLms.sln -c Release --no-build` | `PASS` على 18 fallback؛ بوابة PG17 منفصلة | `commands/12-adm-final-build-tests.txt` |

نجح 120 اختباراً غير معتمد على PostgreSQL. في إعادة البناء النهائية بعد إصلاحات ADM، نجحت اختبارات الهجرة والبذرة ودمج المؤسسة على قاعدتي PostgreSQL 18 منفصلتين عبر `ZU_TEST_PG` و`ZU_ORG_TEST_PG`؛ المجموع **122 اختباراً متميزاً ناجحاً**. تبقى بوابة PostgreSQL 17 داخل Testcontainers معلقة لغياب Docker.

## بيئة استئناف UI

أُنشئت قاعدة PostgreSQL منفصلة باسم `zu_lms_ui_20261101_141731` ومجلد تخزين تحت مجلد temp المعتمد. لم تُستخدم قاعدة `zu_lms`. جُهز SMTP sink على loopback حصراً، وأضيفت Fixtures N3/N4/N5 وE1 (أنشئ E1 عبر رحلة الجودة). أجري الاختبار دون relay خارجي. قيمة `auth.shared_initial_password` أُعيدت إلى قيمتها الأصلية المولدة للجولة بعد ADM15 وتم التحقق منها دون تسجيلها.
