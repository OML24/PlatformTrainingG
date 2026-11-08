# Backup & restore (11 §3.6)

## Daily database backup (cron on the server)

```bash
# /etc/cron.d/zu-lms-backup — daily 02:30, keep 30 days
30 2 * * * root mkdir -p /opt/zu-lms/backups && \
  docker compose -f /opt/zu-lms/docker-compose.yml exec -T db \
  pg_dump -U zulms zulms | gzip > /opt/zu-lms/backups/zulms-$(date +\%F).sql.gz && \
  find /opt/zu-lms/backups -name 'zulms-*.sql.gz' -mtime +30 -delete
```

## Storage backup (uploaded files + certificate PDFs)

```bash
# Weekly copy of the storage volume, keep 30 days
0 3 * * 0 root tar -czf /opt/zu-lms/backups/storage-$(date +\%F).tgz \
  -C /var/lib/docker/volumes/zu-lms_storage/_data . && \
  find /opt/zu-lms/backups -name 'storage-*.tgz' -mtime +30 -delete
```

## Restore

```bash
gunzip -c /opt/zu-lms/backups/zulms-2026-10-29.sql.gz | \
  docker compose -f /opt/zu-lms/docker-compose.yml exec -T db psql -U zulms zulms
tar -xzf /opt/zu-lms/backups/storage-2026-10-29.tgz \
  -C /var/lib/docker/volumes/zu-lms_storage/_data
```
