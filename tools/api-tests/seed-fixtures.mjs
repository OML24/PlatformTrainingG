// Seeds the three employee fixture accounts the API harness expects
// (900001 Dean, 900002 Member, 900003 Quality) plus a faculty scope.
// Passwords use the same PBKDF2/AES-GCM formats the application writes, so the
// accounts work through the normal login path and the admin credential screens.
//
// Usage: node tools/api-tests/seed-fixtures.mjs
// Environment: ZU_TEST_DB (default zu_lms), PGUSER/PGPASSWORD/PGHOST/PGPORT,
//              PG_BIN (default "C:\Program Files\PostgreSQL\18\bin"),
//              ZU_FIXTURE_PASSWORD and ZU_ENC_KEYS (required)
import { execFileSync } from 'node:child_process';
import { pbkdf2Sync, randomBytes, createCipheriv } from 'node:crypto';
import { writeFileSync, mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const db = process.env.ZU_TEST_DB ?? 'zu_lms';
const password = process.env.ZU_FIXTURE_PASSWORD;
if (!password) throw new Error('ZU_FIXTURE_PASSWORD is required.');
if (!process.env.PGPASSWORD) throw new Error('PGPASSWORD is required.');
const psql = join(process.env.PG_BIN ?? 'C:\\Program Files\\PostgreSQL\\18\\bin', 'psql.exe');

const keyConfig = JSON.parse(process.env.ZU_ENC_KEYS ?? '{}');
const keyId = keyConfig.current;
const keyBytes = Buffer.from(keyConfig.keys?.[keyId] ?? '', 'base64');
if (!keyId || keyBytes.length !== 32) throw new Error('ZU_ENC_KEYS must contain the active 32-byte key.');
const aad = Buffer.from('zu-lms-v1', 'utf8');

function hashPassword(plain) {
  const salt = randomBytes(16);
  const hash = pbkdf2Sync(Buffer.from(plain.normalize('NFC'), 'utf8'), salt, 600000, 32, 'sha256');
  return `v1$600000$${salt.toString('base64')}$${hash.toString('base64')}`;
}

function encrypt(plain) {
  const nonce = randomBytes(12);
  const cipher = createCipheriv('aes-256-gcm', keyBytes, nonce, { authTagLength: 16 });
  cipher.setAAD(aad);
  const data = Buffer.concat([cipher.update(Buffer.from(plain.normalize('NFC'), 'utf8')), cipher.final()]);
  return `v1:${keyId}:${nonce.toString('base64')}:${data.toString('base64')}:${cipher.getAuthTag().toString('base64')}`;
}

const passwordHash = hashPassword(password);
const passwordEnc = encrypt(password);

const fixtures = [
  ['900001', 'عميد اختبار', 'Dean Test'],
  ['900002', 'عضو اختبار', 'Member Test'],
  ['900003', 'جودة اختبار', 'Quality Test']
];

const values = fixtures
  .map(([code, ar, en]) => `(${code}, ${q(ar)}, ${q(en)}, ${q(`${en} ${code}`)})`)
  .join(',\n      ');

function q(text) { return `'${text.replaceAll("'", "''")}'`; }

const sql = `
BEGIN;

CREATE TEMP TABLE fixture_employee (code int, name_ar text, name_en text, search text) ON COMMIT DROP;
INSERT INTO fixture_employee (code, name_ar, name_en, search) VALUES
      ${values};

WITH scope AS (
    SELECT s.side_code, c.id AS center_id, c.center_code
    FROM (SELECT side_code FROM zulms_rebuild.university_sides WHERE is_imported ORDER BY side_code LIMIT 1) s
    CROSS JOIN (SELECT id, center_code FROM zulms_rebuild.centers
                WHERE is_faculty AND is_active ORDER BY side_code, name_ar LIMIT 1) c
)
INSERT INTO zulms_rebuild.employees
    (side_code, employee_code, full_name_ar, full_name_en, center_code, center_id,
     emp_status, is_academic, search_text, source_missing, created_at)
SELECT scope.side_code, f.code, f.name_ar, f.name_en,
       scope.center_code, scope.center_id, 13, false, f.search, false, now()
FROM fixture_employee f CROSS JOIN scope
ON CONFLICT (side_code, employee_code) DO UPDATE
    SET full_name_ar = EXCLUDED.full_name_ar, full_name_en = EXCLUDED.full_name_en,
        center_code = EXCLUDED.center_code, center_id = EXCLUDED.center_id,
        emp_status = EXCLUDED.emp_status, source_missing = false, updated_at = now();

INSERT INTO zulms_rebuild.users
    (account_type, login_name, employee_id, full_name_ar, full_name_en, password_hash, password_enc,
     must_change_password, credentials_email_status, credentials_email_attempts, access_override_allow,
     preferred_language, failed_login_count, search_text, is_active, first_login_completed_at, created_at)
SELECT 'Employee', f.code::text, e.id, f.name_ar, f.name_en,
       ${q(passwordHash)}, ${q(passwordEnc)}, false, 'NotSent', 0, false, 'ar', 0, f.search, true, now(), now()
FROM fixture_employee f
JOIN zulms_rebuild.employees e ON e.employee_code = f.code
ON CONFLICT (login_name) DO UPDATE
    SET employee_id = EXCLUDED.employee_id, full_name_ar = EXCLUDED.full_name_ar,
        full_name_en = EXCLUDED.full_name_en, password_hash = EXCLUDED.password_hash,
        password_enc = EXCLUDED.password_enc, must_change_password = false,
        is_active = true, failed_login_count = 0, locked_until = NULL, updated_at = now();

COMMIT;

SELECT u.login_name, u.employee_id, e.employee_code, c.name_ar AS faculty, c.is_faculty
FROM zulms_rebuild.users u
JOIN zulms_rebuild.employees e ON e.id = u.employee_id
LEFT JOIN zulms_rebuild.centers c ON c.id = e.center_id
WHERE u.login_name IN ('900001', '900002', '900003')
ORDER BY u.login_name;
`;

const file = join(mkdtempSync(join(tmpdir(), 'zulms-fixtures-')), 'fixtures.sql');
writeFileSync(file, sql, 'utf8');

const env = {
  ...process.env,
  PGPASSWORD: process.env.PGPASSWORD,
  PGCLIENTENCODING: 'UTF8'
};
const args = [
  '-h', process.env.PGHOST ?? '127.0.0.1',
  '-p', process.env.PGPORT ?? '5432',
  '-U', process.env.PGUSER ?? 'postgres',
  '-w', '-d', db, '-v', 'ON_ERROR_STOP=1', '-f', file
];
process.stdout.write(execFileSync(psql, args, { env, encoding: 'utf8' }));

