// Seeds only the isolated UI test database. Passwords are generated and written
// outside the repository; the evidence manifest contains identifiers only.
import { execFileSync } from 'node:child_process';
import { createCipheriv, pbkdf2Sync, randomBytes } from 'node:crypto';
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { tmpdir } from 'node:os';

const runId = process.env.ZU_UI_RUN_ID ?? '20261101-141731';
const db = process.env.ZU_TEST_DB ?? '';
if (db !== `zu_lms_ui_${runId.replaceAll('-', '_')}`)
  throw new Error(`Refusing to seed non-UI database: ${db || '(unset)'}`);

const keyConfig = JSON.parse(process.env.ZU_ENC_KEYS ?? '{}');
const keyId = keyConfig.current;
const key = keyId && keyConfig.keys?.[keyId] ? Buffer.from(keyConfig.keys[keyId], 'base64') : null;
if (!key || key.length !== 32) throw new Error('ZU_ENC_KEYS must contain the active 32-byte application key.');

const secretRoot = process.env.ZU_UI_SECRET_ROOT ?? join(tmpdir(), `zulms-ui-${runId}`);
const manifestPath = process.env.ZU_UI_MANIFEST ?? join('docs', 'ui-test-evidence', runId, 'fixtures-manifest.json');
const psql = join(process.env.PG_BIN ?? 'C:\\Program Files\\PostgreSQL\\18\\bin', 'psql.exe');
const prefix = `UI-${runId}-`;
const a2Login = `uia2-${runId.replaceAll('-', '')}`;
const aad = Buffer.from('zu-lms-v1', 'utf8');
const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%';

function password(length = 24) {
  const bytes = randomBytes(length);
  return [...bytes].map(x => alphabet[x % alphabet.length]).join('');
}
function hash(plain) {
  const salt = randomBytes(16);
  const value = pbkdf2Sync(Buffer.from(plain.normalize('NFC')), salt, 600000, 32, 'sha256');
  return `v1$600000$${salt.toString('base64')}$${value.toString('base64')}`;
}
function encrypt(plain) {
  const nonce = randomBytes(12);
  const cipher = createCipheriv('aes-256-gcm', key, nonce, { authTagLength: 16 });
  cipher.setAAD(aad);
  const value = Buffer.concat([cipher.update(Buffer.from(plain.normalize('NFC'))), cipher.final()]);
  return `v1:${keyId}:${nonce.toString('base64')}:${value.toString('base64')}:${cipher.getAuthTag().toString('base64')}`;
}
function q(value) { return `'${String(value).replaceAll("'", "''")}'`; }

const normalPassword = password();
const personalInitial = password();
const sharedInitial = password();
const unavailableInitial = password();
const normalHash = hash(normalPassword);
const normalEnc = encrypt(normalPassword);
const personalInitialEnc = encrypt(personalInitial);
const unavailableInitialEnc = encrypt(unavailableInitial);

const employees = [
  ['Q1', 911101, 1, 13, true,  'مسؤول جودة اختبار', 'Quality Fixture'],
  ['D1', 911102, 1, 13, true,  'عميد كلية الاختبار الأولى', 'Dean F1 Fixture'],
  ['D2', 911103, 2, 13, true,  'عميد كلية الاختبار الثانية', 'Dean F2 Fixture'],
  ['M1', 911104, 1, 13, true,  'عضو اختبار أول', 'Member One Fixture'],
  ['M2', 911105, 1, 13, true,  'عضو اختبار ثان', 'Member Two Fixture'],
  ['M3', 911106, 2, 13, true,  'عضو اختبار كلية ثانية', 'Member Three Fixture'],
  ['I1', 911107, 1, 13, true,  'محاضر اختبار كلية أولى', 'Instructor F1 Fixture'],
  ['I2', 911108, 2, 13, true,  'محاضر اختبار كلية ثانية', 'Instructor F2 Fixture'],
  ['N1', 911109, 1, 13, false, 'موظف دخول أولي شخصي', 'Personal Initial Fixture'],
  ['N2', 911110, 1, 13, false, 'موظف دخول أولي مشترك', 'Shared Initial Fixture'],
  ['X1', 911111, 1, 13, true,  'موظف اختبار معطل', 'Disabled Fixture'],
  ['X2', 911112, 1, 99, true,  'موظف اختبار غير مؤهل', 'Ineligible Fixture'],
  ['O1', 911113, 1, 13, true,  'عضو اختبار تجاوزات', 'Override Fixture'],
  ['EMPTY', 911114, 1, 13, true, 'عضو اختبار فارغ', 'Empty Fixture']
];
const employeeValues = employees.map(x => `(${q(x[0])},${x[1]},${x[2]},${x[3]},${x[4]},${q(prefix + x[5])},${q(x[6])})`).join(',\n');

const sql = `
BEGIN;
INSERT INTO zulms_rebuild.university_sides(side_code,name_ar,is_imported,last_seen_at)
VALUES (91,${q(prefix + 'جهة الاختبار')},true,now())
ON CONFLICT(side_code) DO UPDATE SET name_ar=excluded.name_ar,is_imported=true,last_seen_at=now();

INSERT INTO zulms_rebuild.centers(side_code,center_code,name_ar,name_en,is_faculty,short_code,source_missing,last_seen_at,is_active)
VALUES (91,9101,${q(prefix + 'كلية الاختبار الأولى')},'UI Test Faculty One',true,'UTF1',false,now(),true),
       (91,9102,${q(prefix + 'كلية الاختبار الثانية')},'UI Test Faculty Two',true,'UTF2',false,now(),true)
ON CONFLICT(side_code,center_code) DO UPDATE SET name_ar=excluded.name_ar,name_en=excluded.name_en,is_faculty=true,
 short_code=excluded.short_code,source_missing=false,last_seen_at=now(),is_active=true;

INSERT INTO zulms_rebuild.departments(side_code,department_code,center_code,center_id,name_ar,name_en,reg_code,source_missing,last_seen_at,is_active)
SELECT 91, 9110+c.center_code-9101, c.center_code, c.id,
       ${q(prefix + 'قسم الاختبار ')}||(c.center_code-9100), 'UI Test Department '||(c.center_code-9100),
       'UITD'||(c.center_code-9100), false, now(), true
FROM zulms_rebuild.centers c WHERE c.side_code=91 AND c.center_code IN (9101,9102)
ON CONFLICT(side_code,department_code) DO UPDATE SET center_code=excluded.center_code,center_id=excluded.center_id,
 name_ar=excluded.name_ar,name_en=excluded.name_en,source_missing=false,last_seen_at=now(),is_active=true;

CREATE TEMP TABLE ui_fixture(code text, employee_code int, faculty int, emp_status int, first_complete bool, name_ar text, name_en text) ON COMMIT DROP;
INSERT INTO ui_fixture VALUES ${employeeValues};

INSERT INTO zulms_rebuild.employees(side_code,employee_code,full_name_ar,full_name_en,center_code,department_code,
 center_id,department_id,main_job_code,main_job_desc,emp_status,emp_status_desc,is_academic,search_text,source_missing,last_seen_at)
SELECT 91,f.employee_code,f.name_ar,f.name_en,c.center_code,d.department_code,c.id,d.id,1,'UI fixture',f.emp_status,
 CASE WHEN f.emp_status=13 THEN 'Active' ELSE 'Ineligible' END,true,
 f.name_ar||' '||f.name_en||' '||f.employee_code,false,now()
FROM ui_fixture f JOIN zulms_rebuild.centers c ON c.side_code=91 AND c.center_code=9100+f.faculty
JOIN zulms_rebuild.departments d ON d.side_code=91 AND d.department_code=9110+f.faculty-1
ON CONFLICT(side_code,employee_code) DO UPDATE SET full_name_ar=excluded.full_name_ar,full_name_en=excluded.full_name_en,
 center_code=excluded.center_code,department_code=excluded.department_code,center_id=excluded.center_id,department_id=excluded.department_id,
 emp_status=excluded.emp_status,is_academic=excluded.is_academic,search_text=excluded.search_text,source_missing=false,last_seen_at=now();

INSERT INTO zulms_rebuild.users(account_type,login_name,employee_id,full_name_ar,full_name_en,password_hash,password_enc,
 initial_password_enc,must_change_password,first_login_completed_at,security_stamp,credentials_email_status,
 credentials_email_attempts,access_override_allow,preferred_language,failed_login_count,search_text,is_active)
SELECT 'Employee',f.employee_code::text,e.id,f.name_ar,f.name_en,${q(normalHash)},${q(normalEnc)},
 CASE f.code WHEN 'N1' THEN ${q(personalInitialEnc)} WHEN 'N2' THEN ${q(unavailableInitialEnc)} ELSE NULL END,
 NOT f.first_complete,CASE WHEN f.first_complete THEN now() ELSE NULL END,gen_random_uuid(),'NotSent',0,false,'ar',0,
 f.name_ar||' '||f.name_en||' '||f.employee_code,f.code<>'X1'
FROM ui_fixture f JOIN zulms_rebuild.employees e ON e.side_code=91 AND e.employee_code=f.employee_code
ON CONFLICT(login_name) DO UPDATE SET employee_id=excluded.employee_id,full_name_ar=excluded.full_name_ar,full_name_en=excluded.full_name_en,
 password_hash=excluded.password_hash,password_enc=excluded.password_enc,initial_password_enc=excluded.initial_password_enc,
 must_change_password=excluded.must_change_password,first_login_completed_at=excluded.first_login_completed_at,
 security_stamp=gen_random_uuid(),failed_login_count=0,locked_until=NULL,search_text=excluded.search_text,is_active=excluded.is_active;

INSERT INTO zulms_rebuild.users(account_type,login_name,full_name_ar,full_name_en,password_hash,password_enc,must_change_password,
 first_login_completed_at,security_stamp,credentials_email_status,credentials_email_attempts,access_override_allow,
 preferred_language,failed_login_count,search_text,is_active)
VALUES ('Local',${q(a2Login)},${q(prefix + 'مسؤول اختبار ثان')},'Second UI Administrator',${q(normalHash)},${q(normalEnc)},false,
 now(),gen_random_uuid(),'NotSent',0,false,'ar',0,${q(prefix + 'مسؤول اختبار ثان Second UI Administrator')},true)
ON CONFLICT(login_name) DO UPDATE SET password_hash=excluded.password_hash,password_enc=excluded.password_enc,
 security_stamp=gen_random_uuid(),failed_login_count=0,locked_until=NULL,is_active=true;

DELETE FROM zulms_rebuild.user_roles ur USING zulms_rebuild.users u
WHERE ur.user_id=u.id AND (u.login_name LIKE '9111%' OR u.login_name=${q(a2Login)});
INSERT INTO zulms_rebuild.user_roles(user_id,role_id,scope_center_id,is_active)
SELECT u.id,r.id,s.scope_id,true FROM (VALUES
 (${q(a2Login)},'Admin',NULL::bigint),('911101','Quality',NULL),('911101','Member',NULL),
 ('911102','Dean',(SELECT id FROM zulms_rebuild.centers WHERE side_code=91 AND center_code=9101)),('911102','Member',NULL),
 ('911103','Dean',(SELECT id FROM zulms_rebuild.centers WHERE side_code=91 AND center_code=9102)),('911103','Member',NULL),
 ('911104','Member',NULL),('911105','Member',NULL),('911106','Member',NULL),('911107','Member',NULL),('911108','Member',NULL),
 ('911109','Member',NULL),('911110','Member',NULL),('911111','Member',NULL),('911112','Member',NULL),
 ('911113','Member',NULL),('911114','Member',NULL)
) s(login,role_code,scope_id) JOIN zulms_rebuild.users u ON u.login_name=s.login JOIN zulms_rebuild.roles r ON r.code=s.role_code;

UPDATE zulms_rebuild.system_settings SET value=${q(encrypt(sharedInitial))},updated_at=now()
WHERE setting_key='auth.shared_initial_password';

DELETE FROM zulms_rebuild.user_permission_overrides o USING zulms_rebuild.users u WHERE o.user_id=u.id AND u.login_name='911113';
INSERT INTO zulms_rebuild.user_permission_overrides(user_id,permission_id,mode,reason)
SELECT u.id,p.id,x.mode,${q(prefix + 'تجاوز صلاحية مؤقت')}
FROM (VALUES ('audit.export','Allow'),('certificates.view.own','Deny')) x(perm_key,mode)
JOIN zulms_rebuild.users u ON u.login_name='911113' JOIN zulms_rebuild.permissions p ON p.perm_key=x.perm_key;

INSERT INTO zulms_rebuild.training_tracks(name_ar,name_en,sort_order,is_active,is_archived)
SELECT ${q(prefix + 'محور تكليف المحاضرين')},'UI Instructor Assignment Track',999,true,false
WHERE NOT EXISTS (SELECT 1 FROM zulms_rebuild.training_tracks WHERE name_ar=${q(prefix + 'محور تكليف المحاضرين')});

INSERT INTO zulms_rebuild.courses(course_no,title_ar,title_en,track_id,delivery_mode,objectives_ar,target_audience_ar,total_hours,
 planned_start_date,planned_end_date,origin,proposing_center_id,audience_type,target_center_id,registration_deadline,max_participants,
 has_exam,status,status_changed_at,approved_at,evaluation_end_date,managed_by_quality,instructor_kind,instructor_user_id,
 instructor_name_ar,instructor_name_en,is_historical,search_text,is_active,is_archived)
SELECT x.course_no,x.title_ar,x.title_en,t.id,'InPerson','هدف اختبار','موظفو الكلية',2,
 current_date+7,current_date+7,'AdminCreated',c.id,'Faculty',c.id,now()+interval '5 days',20,false,'Approved',now(),now(),current_date+14,
 false,'Internal',u.id,u.full_name_ar,u.full_name_en,false,x.title_ar||' '||x.title_en||' '||x.course_no,true,false
FROM (VALUES (${q(prefix + 'I1-ASSIGN')},${q(prefix + 'تكليف المحاضر الأول')},'Instructor One Assignment','911107',9101),
             (${q(prefix + 'I2-ASSIGN')},${q(prefix + 'تكليف المحاضر الثاني')},'Instructor Two Assignment','911108',9102))
 x(course_no,title_ar,title_en,login,center_code)
JOIN zulms_rebuild.training_tracks t ON t.name_ar=${q(prefix + 'محور تكليف المحاضرين')}
JOIN zulms_rebuild.users u ON u.login_name=x.login JOIN zulms_rebuild.centers c ON c.side_code=91 AND c.center_code=x.center_code
ON CONFLICT(course_no) DO UPDATE SET instructor_user_id=excluded.instructor_user_id,instructor_name_ar=excluded.instructor_name_ar,
 instructor_name_en=excluded.instructor_name_en,status='Approved',is_active=true,is_archived=false,updated_at=now();
COMMIT;
`;

mkdirSync(secretRoot, { recursive: true });
const sqlPath = join(secretRoot, 'seed-ui-fixtures.sql');
writeFileSync(sqlPath, sql, 'utf8');
const env = { ...process.env, PGPASSWORD: process.env.PGPASSWORD ?? '', PGCLIENTENCODING: 'UTF8' };
execFileSync(psql, ['-h', process.env.PGHOST ?? '127.0.0.1', '-p', process.env.PGPORT ?? '5432',
  '-U', process.env.PGUSER ?? 'postgres', '-w', '-d', db, '-v', 'ON_ERROR_STOP=1', '-f', sqlPath],
  { env, encoding: 'utf8', stdio: ['ignore', 'ignore', 'pipe'] });

const credentials = {
  runId, generatedAt: new Date().toISOString(), normalPassword, personalInitial, sharedInitial,
  accounts: Object.fromEntries([
    ['A2', a2Login], ...employees.map(x => [x[0], String(x[1])])
  ])
};
const credentialsPath = join(secretRoot, 'credentials.json');
writeFileSync(credentialsPath, JSON.stringify(credentials, null, 2), { encoding: 'utf8', mode: 0o600 });

const query = `SELECT u.id,u.login_name,u.account_type,u.is_active,e.center_id,e.emp_status,
 COALESCE(string_agg(DISTINCT r.code,',' ORDER BY r.code),'') roles
 FROM zulms_rebuild.users u LEFT JOIN zulms_rebuild.employees e ON e.id=u.employee_id
 LEFT JOIN zulms_rebuild.user_roles ur ON ur.user_id=u.id AND ur.is_active LEFT JOIN zulms_rebuild.roles r ON r.id=ur.role_id
 WHERE u.login_name=${q('uiadmin')} OR u.login_name=${q(a2Login)} OR u.login_name LIKE '9111%'
 GROUP BY u.id,u.login_name,u.account_type,u.is_active,e.center_id,e.emp_status ORDER BY u.login_name`;
const rows = execFileSync(psql, ['-h', process.env.PGHOST ?? '127.0.0.1', '-p', process.env.PGPORT ?? '5432',
  '-U', process.env.PGUSER ?? 'postgres', '-w', '-d', db, '-At', '-F', '|', '-c', query], { env, encoding: 'utf8' })
  .trim().split(/\r?\n/).filter(Boolean).map(line => line.split('|'));
const fixtureByLogin = new Map([['uiadmin','A1'],[a2Login,'A2'],...employees.map(x=>[String(x[1]),x[0]])]);
const accounts = rows.map(([id,loginName,accountType,isActive,centerId,empStatus,roles]) => ({
  fixture: fixtureByLogin.get(loginName), id:Number(id), loginName, accountType, isActive:isActive==='t',
  centerId:centerId?Number(centerId):null, empStatus:empStatus?Number(empStatus):null,
  roles:roles?roles.split(','):[],
  derivedRoles:['911107','911108'].includes(loginName)?['Instructor']:[], passwordIncluded:false
}));
const courseRows = execFileSync(psql, ['-h', process.env.PGHOST ?? '127.0.0.1', '-p', process.env.PGPORT ?? '5432',
  '-U', process.env.PGUSER ?? 'postgres', '-w', '-d', db, '-At', '-F', '|', '-c',
  `SELECT id,course_no,instructor_user_id FROM zulms_rebuild.courses WHERE course_no LIKE ${q(prefix + '%')} ORDER BY course_no`],
  { env, encoding:'utf8' }).trim().split(/\r?\n/).filter(Boolean).map(x=>x.split('|'));
const manifest = {
  runId,prefix,status:'READY-ACCOUNTS',reason:'Account and role fixtures are ready; the full course/file catalog and test SMTP remain pending.',
  database:db,storage:'isolated-temp',faculties:[{fixture:'F1',sideCode:91,centerCode:9101},{fixture:'F2',sideCode:91,centerCode:9102}],
  accounts,courses:courseRows.map(([id,courseNo,instructorUserId])=>({id:Number(id),courseNo,instructorUserId:Number(instructorUserId),purpose:'derived-instructor-role'})),
  overrides:{fixture:'O1',allow:'audit.export',deny:'certificates.view.own',inherit:'profile.view.own'},files:[],secretsIncluded:false,
  credentialsLocation:'outside-repository'
};
mkdirSync(dirname(manifestPath), { recursive: true });
writeFileSync(manifestPath, JSON.stringify(manifest, null, 2)+'\n', 'utf8');
console.log(JSON.stringify({status:'ok',database:db,accounts:accounts.length,courses:courseRows.length,credentialsPath,manifestPath}));
