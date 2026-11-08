# النسخ الاحتياطي والاستعادة

نفّذ نسخ قاعدة PostgreSQL 17 يومياً على الخادم، وانسخ وحدة `storage` دورياً. احتفظ بالنسخ 30 يوماً، واحفظها خارج الخادم بصلاحيات مقيدة وتشفير. ملف `.env` ومفاتيح `ZU_ENC_KEYS` ضروريان لفك البيانات المشفرة بعد الاستعادة؛ احفظ نسخة آمنة منهما منفصلة عن النسخة العامة.

مثال يومي من `/opt/zu-lms` على خادم Linux، مع ضبط `BACKUP_DIR` إلى مسار نسخ آمن:

```sh
mkdir -p "$BACKUP_DIR"
docker compose exec -T db sh -c 'exec pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > "$BACKUP_DIR/zulms-$(date -u +%Y%m%d).dump"
docker run --rm -v zu-lms_storage:/storage:ro -v "$BACKUP_DIR:/backup" alpine tar -C /storage -czf "/backup/storage-$(date -u +%Y%m%d).tar.gz" .
find "$BACKUP_DIR" -type f \( -name 'zulms-*.dump' -o -name 'storage-*.tar.gz' \) -mtime +30 -delete
```

اسم وحدة Docker Compose الفعلي قد يختلف باختلاف اسم مشروع Compose؛ تحقق منه عبر `docker volume ls`. اختبر استعادة دورية على بيئة معزولة باستخدام `pg_restore` وفك أرشيف التخزين، ثم افحص `/health` وملفات نموذجية. لا تستعد فوق قاعدة إنتاج نشطة.

قبل تدوير مفتاح التشفير، أضف المفتاح الجديد إلى `ZU_ENC_KEYS` واجعله `current` مع إبقاء القديم، ثم شغّل `dotnet ZuLms.Web.dll --reencrypt-secrets` أثناء توقف نسخ التطبيق الأخرى. بعد نجاح الأمر والتحقق من الدخول وقراءة الإعدادات السرية، يمكن إزالة المفتاح القديم في نافذة صيانة لاحقة.
