// Adds one isolated pre-first-login employee fixture for ADM12/13/14.
// This script refuses to connect to anything except the named UI test database.
import { execFileSync } from 'node:child_process';
import { createCipheriv, pbkdf2Sync, randomBytes } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { mkdirSync } from 'node:fs';

const runId = process.env.ZU_UI_RUN_ID ?? '20261101-141731';
const database = process.env.ZU_TEST_DB ?? '';
if (database !== `zu_lms_ui_${runId.replaceAll('-', '_')}`)
  throw new Error(`Refusing non-UI database: ${database || '(unset)'}`);
const keyConfig = JSON.parse(process.env.ZU_ENC_KEYS ?? '{}');
const keyId = keyConfig.current;
const key = keyId && keyConfig.keys?.[keyId] ? Buffer.from(keyConfig.keys[keyId], 'base64') : null;
if (!key || key.length !== 32) throw new Error('Missing active 32-byte encryption key.');

const root = process.env.ZU_UI_SECRET_ROOT ?? join(process.env.LOCALAPPDATA ?? '.', 'Temp', 'opencode', `zulms-ui-${runId}`);
const credentialsPath = join(root, 'credentials.json');
const manifestPath = process.env.ZU_UI_MANIFEST ?? join('docs', 'ui-test-evidence', runId, 'fixtures-manifest.json');
const login = '911115';
const email = 'adm13.employee@example.test';
const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%';
const symbols = [...randomBytes(24)].map(byte => alphabet[byte % alphabet.length]);
const passwordChars = ['A', 'a', '7', '!', ...symbols];
for (let i = passwordChars.length - 1; i > 0; i--) {
  const j = randomBytes(1)[0] % (i + 1);
  [passwordChars[i], passwordChars[j]] = [passwordChars[j], passwordChars[i]];
}
const password = passwordChars.join('');
const salt = randomBytes(16);
const hash = `v1$600000$${salt.toString('base64')}$${pbkdf2Sync(Buffer.from(password.normalize('NFC')), salt, 600000, 32, 'sha256').toString('base64')}`;
const nonce = randomBytes(12);
const cipher = createCipheriv('aes-256-gcm', key, nonce, { authTagLength: 16 });
cipher.setAAD(Buffer.from('zu-lms-v1', 'utf8'));
const encryptedBytes = Buffer.concat([cipher.update(Buffer.from(password.normalize('NFC'))), cipher.final()]);
const encrypted = `v1:${keyId}:${nonce.toString('base64')}:${encryptedBytes.toString('base64')}:${cipher.getAuthTag().toString('base64')}`;
const q = value => `'${String(value).replaceAll("'", "''")}'`;
const psql = join(process.env.PG_BIN ?? 'C:\\Program Files\\PostgreSQL\\18\\bin', 'psql.exe');
const env = { ...process.env, PGPASSWORD: process.env.PGPASSWORD ?? '', PGCLIENTENCODING: 'UTF8' };
const readJson = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''));
const existing = execFileSync(psql, ['-h', process.env.PGHOST ?? '127.0.0.1', '-p', process.env.PGPORT ?? '5432',
  '-U', process.env.PGUSER ?? 'postgres', '-w', '-d', database, '-At', '-F', '|', '-c',
  `SELECT EXISTS(SELECT 1 FROM zulms_rebuild.users WHERE login_name=${q(login)}), EXISTS(SELECT 1 FROM zulms_rebuild.employees WHERE side_code=91 AND employee_code=911115)`],
{ env, encoding: 'utf8' }).trim().split('|');
if (existing[0] !== existing[1]) throw new Error('ADM fixture is only partially present; refusing to modify it.');
const fixtureExists = existing[0] === 't';
const sql = `
BEGIN;
WITH source_employee AS (
  INSERT INTO zulms_rebuild.employees(side_code,employee_code,full_name_ar,full_name_en,center_code,department_code,
    center_id,department_id,main_job_code,main_job_desc,emp_status,emp_status_desc,is_academic,source_email,
    search_text,source_missing,last_seen_at)
  SELECT 91,911115,${q(`UI-${runId}-موظف إدارة قبل أول دخول`)},'ADM UI Employee Before First Login',
    c.center_code,d.department_code,c.id,d.id,1,'UI fixture',13,'Active',true,${q(email)},
    ${q(`UI-${runId}-موظف إدارة قبل أول دخول ADM UI Employee Before First Login 911115`)},false,now()
  FROM zulms_rebuild.centers c JOIN zulms_rebuild.departments d ON d.side_code=c.side_code AND d.center_id=c.id
  WHERE c.side_code=91 AND c.center_code=9101 AND d.department_code=9110
  RETURNING id,full_name_ar,full_name_en,source_email
), new_user AS (
  INSERT INTO zulms_rebuild.users(account_type,login_name,employee_id,full_name_ar,full_name_en,password_hash,password_enc,
    initial_password_enc,must_change_password,first_login_completed_at,security_stamp,credentials_email_status,
    credentials_email_attempts,access_override_allow,preferred_language,failed_login_count,search_text,is_active)
  SELECT 'Employee',${q(login)},id,full_name_ar,full_name_en,${q(hash)},${q(encrypted)},${q(encrypted)},true,NULL,
    gen_random_uuid(),'NotSent',0,false,'ar',0,${q(`UI-${runId}-موظف إدارة قبل أول دخول 911115`)},true
  FROM source_employee RETURNING id
)
INSERT INTO zulms_rebuild.user_roles(user_id,role_id,scope_center_id,is_active)
SELECT new_user.id,roles.id,NULL,true FROM new_user CROSS JOIN zulms_rebuild.roles roles WHERE roles.code='Member';
COMMIT;
`;
const sqlPath = join(root, 'seed-adm-fixtures.sql');
mkdirSync(dirname(sqlPath), { recursive: true });
writeFileSync(sqlPath, sql, 'utf8');
if (!fixtureExists) {
  execFileSync(psql, ['-h', process.env.PGHOST ?? '127.0.0.1', '-p', process.env.PGPORT ?? '5432',
    '-U', process.env.PGUSER ?? 'postgres', '-w', '-d', database, '-v', 'ON_ERROR_STOP=1', '-f', sqlPath],
  { env, encoding: 'utf8', stdio: ['ignore', 'ignore', 'pipe'] });
}

const credentials = readJson(credentialsPath);
credentials.accounts.N3 = login;
if (!fixtureExists) credentials.adm12EmployeePassword = password;
writeFileSync(credentialsPath, JSON.stringify(credentials, null, 2), { encoding: 'utf8', mode: 0o600 });

const manifest = readJson(manifestPath);
manifest.accounts = manifest.accounts.filter(x => x.fixture !== 'N3');
manifest.accounts.push({ fixture: 'N3', loginName: login, accountType: 'Employee', firstLoginCompleted: false,
  email: 'test-only-sink-address', roles: ['Member'], purpose: 'ADM12/13/14 pre-first-login employee' });
manifest.status = 'ADM-FIXTURES-READY';
manifest.reason = 'N3 is ready for first-login and isolated SMTP UI checks; E1 remains to be created by quality workflow.';
writeFileSync(manifestPath, JSON.stringify(manifest, null, 2) + '\n', 'utf8');
console.log(JSON.stringify({ status: 'ready', fixture: 'N3', login, email: 'test-only-sink-address', alreadyPresent: fixtureExists, secretsIncluded: false }));
