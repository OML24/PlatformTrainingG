# 11 — النشر (Docker على Ubuntu + GitHub Actions)

## 1. الملفات (`deploy/`)
**`Dockerfile`** (متعدد المراحل): `mcr.microsoft.com/dotnet/sdk:10.0` للبناء (`dotnet publish src/ZuLms.Web -c Release -o /app`) ← `mcr.microsoft.com/dotnet/aspnet:10.0` للتشغيل؛ يُثبَّت `fontconfig` و`libfreetype6` (لازمان لـ QuestPDF)؛ مستخدم غير جذري؛ `EXPOSE 8080`؛ `ENV ASPNETCORE_URLS=http://+:8080`؛ `HEALTHCHECK` على `/health`.
**`docker-compose.yml`:**
```yaml
services:
  db:
    image: postgres:17
    restart: unless-stopped
    environment: { POSTGRES_DB: zulms, POSTGRES_USER: zulms, POSTGRES_PASSWORD: ${POSTGRES_PASSWORD} }
    volumes: [ "pgdata:/var/lib/postgresql/data" ]
    networks: [ internal ]          # لا منافذ منشورة للخارج
  app:
    image: ghcr.io/<org>/zu-lms:${TAG:-latest}
    restart: unless-stopped
    depends_on: [ db ]
    env_file: .env
    volumes: [ "storage:/var/zu-lms/storage" ]
    ports: [ "127.0.0.1:8080:8080" ] # للبروكسي العكسي فقط
    networks: [ internal ]
volumes: { pgdata: {}, storage: {} }
networks: { internal: {} }
```
**`.env.sample`** (يُنسخ إلى `.env` على الخادم ولا يُلتزم): `POSTGRES_PASSWORD`, `ZU_DB_CONNECTION=Host=db;Database=zulms;Username=zulms;Password=…`, `ZU_ENC_KEYS`, `ZU_JWT_KEY`, `ZU_SMTP_HOST|PORT|USER|PASSWORD|SSL`, `ZU_STORAGE_ROOT=/var/zu-lms/storage`, `ZU_PUBLIC_BASE_URL=https://portal.zu.edu.jo`, `ZU_BOOTSTRAP_ADMIN_LOGIN|PASSWORD|NAME`, `KnownProxies`.
**`nginx.conf.sample`:** TLS على `portal.zu.edu.jo`، `proxy_pass http://127.0.0.1:8080`، ترويسات `X-Forwarded-For/Proto/Host`، `client_max_body_size 100m` (≥ أكبر `files.max_mb`)، `proxy_read_timeout 120s` (المزامنة/التوليد).

## 2. أوامر التشغيل
- الهجرات تُطبَّق عند بدء التطبيق (`Database.MigrateAsync`) مع قفل استشاري لمنع التزاحم عند أكثر من نسخة، ثم `DbSeeder`.
- أوامر الصيانة: `dotnet ZuLms.Web.dll --reencrypt-secrets` (تدوير مفتاح التشفير)، `--sync` (تشغيل المزامنة من سطر الأوامر عند الإعداد الأول).
- **أول تشغيل:** (1) اضبط `ZU_BOOTSTRAP_ADMIN_*` ← الحساب المحلي. (2) ادخل، افتح `/admin/sync`، شغّل «تحديث بيانات الجامعة». (3) عيّن دور Admin للأدمن الحقيقي، ثم Quality والعمداء (يدوياً). (4) عيّن `short_code` للكليات وراجع `is_faculty`. (5) ارفع قالب الشهادة. (6) راجع الإعدادات. (7) عطّل الحساب المحلي. (8) أرسل بيانات الدخول الأولية بالزر الجماعي.

## 3. GitHub Actions (`.github/workflows/deploy.yml`)
1. المشغّل: `push` على `main`.
2. مهمة `test`: `dotnet restore/build/test` مع `ZU_REQUIRE_PG17_CONTAINER=1`؛ يجب أن ينجح Testcontainers على صورة `postgres:17`، ولا يُقبل بديل `ZU_TEST_PG` في GitHub Actions.
3. مهمة `build`: بناء الصورة ودفعها إلى `ghcr.io` بوسمين (`sha` و`latest`).
4. مهمة `deploy`: SSH إلى الخادم بمفتاح من `secrets.SSH_KEY` (مستخدم نشر محدود)، ثم: `cd /opt/zu-lms && docker compose pull && TAG=<sha> docker compose up -d && docker image prune -f`.
5. الأسرار على GitHub: `SSH_HOST`, `SSH_USER`, `SSH_KEY`, `GHCR_TOKEN`. **لا أسرار تطبيق** (تبقى في `.env` على الخادم).
6. نسخ احتياطي: `pg_dump` يومي (cron على الخادم) + نسخ مجلد `storage` الدوري، والاحتفاظ 30 يوماً. وثّق الأمر في `docs/backup.md`.

## 4. نقاط تشغيل
- `/health` (فحص قاعدة البيانات والتخزين) بلا بيانات حساسة.
- سجلات Serilog ملفات دوّارة يومية في `/var/zu-lms/logs` (volume)، مع حجب الحقول الحساسة (`09 §3`).
- المنطقة الزمنية للحاوية UTC؛ التحويل لعمّان في التطبيق (`IClock`).
- الوصول: الواجهة عبر HTTPS فقط؛ HSTS مفعّل.
