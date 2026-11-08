// Adds a second isolated employee for ADM13 resend and ADM14 bulk-mail checks.
import { execFileSync } from 'node:child_process';
import { createCipheriv, pbkdf2Sync, randomBytes } from 'node:crypto';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';

const runId = process.env.ZU_UI_RUN_ID ?? '20261101-141731';
const database = process.env.ZU_TEST_DB ?? '';
if (database !== `zu_lms_ui_${runId.replaceAll('-', '_')}`) throw new Error('Refusing non-UI database.');
const config = JSON.parse(process.env.ZU_ENC_KEYS ?? '{}');
const keyId = config.current, key = keyId && config.keys?.[keyId] ? Buffer.from(config.keys[keyId], 'base64') : null;
if (!key || key.length !== 32) throw new Error('Missing active encryption key.');
const root = process.env.ZU_UI_SECRET_ROOT;
const manifestPath = process.env.ZU_UI_MANIFEST;
if (!root || !manifestPath) throw new Error('Secret root and evidence manifest paths are required.');
const login = '911116', email = 'adm14.employee@example.test';
const q = x => `'${String(x).replaceAll("'", "''")}'`;
const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%';
const chars = ['A','a','7','!', ...[...randomBytes(24)].map(x => alphabet[x % alphabet.length])];
for (let i=chars.length-1;i>0;i--) { const j=randomBytes(1)[0]%(i+1); [chars[i],chars[j]]=[chars[j],chars[i]]; }
const password=chars.join(''), salt=randomBytes(16);
const hash=`v1$600000$${salt.toString('base64')}$${pbkdf2Sync(Buffer.from(password.normalize('NFC')),salt,600000,32,'sha256').toString('base64')}`;
const nonce=randomBytes(12), cipher=createCipheriv('aes-256-gcm',key,nonce,{authTagLength:16});
cipher.setAAD(Buffer.from('zu-lms-v1','utf8'));
const encryptedBytes=Buffer.concat([cipher.update(Buffer.from(password.normalize('NFC'))),cipher.final()]);
const encrypted=`v1:${keyId}:${nonce.toString('base64')}:${encryptedBytes.toString('base64')}:${cipher.getAuthTag().toString('base64')}`;
const psql=join(process.env.PG_BIN??'C:\\Program Files\\PostgreSQL\\18\\bin','psql.exe');
const env={...process.env,PGPASSWORD:process.env.PGPASSWORD??'',PGCLIENTENCODING:'UTF8'};
const present=execFileSync(psql,['-h',process.env.PGHOST??'127.0.0.1','-p',process.env.PGPORT??'5432','-U',process.env.PGUSER??'postgres','-w','-d',database,'-At','-F','|','-c',
 `SELECT EXISTS(SELECT 1 FROM zulms_rebuild.users WHERE login_name=${q(login)}),EXISTS(SELECT 1 FROM zulms_rebuild.employees WHERE side_code=91 AND employee_code=911116)`],{env,encoding:'utf8'}).trim().split('|');
if(present[0]!==present[1]) throw new Error('ADM14 fixture is partially present.');
if(present[0]!=='t') {
 const sql=`BEGIN;
 WITH source_employee AS (
  INSERT INTO zulms_rebuild.employees(side_code,employee_code,full_name_ar,full_name_en,center_code,department_code,center_id,department_id,main_job_code,main_job_desc,emp_status,emp_status_desc,is_academic,source_email,search_text,source_missing,last_seen_at)
  SELECT 91,911116,${q(`UI-${runId}-موظف بريد جماعي`)},'ADM UI Bulk Email Employee',c.center_code,d.department_code,c.id,d.id,1,'UI fixture',13,'Active',true,${q(email)},${q(`UI-${runId}-موظف بريد جماعي ADM UI Bulk Email Employee 911116`)},false,now()
  FROM zulms_rebuild.centers c JOIN zulms_rebuild.departments d ON d.side_code=c.side_code AND d.center_id=c.id WHERE c.side_code=91 AND c.center_code=9101 AND d.department_code=9110
  RETURNING id,full_name_ar,full_name_en
 ), new_user AS (
  INSERT INTO zulms_rebuild.users(account_type,login_name,employee_id,full_name_ar,full_name_en,password_hash,password_enc,initial_password_enc,must_change_password,first_login_completed_at,security_stamp,credentials_email_status,credentials_email_attempts,access_override_allow,preferred_language,failed_login_count,search_text,is_active)
  SELECT 'Employee',${q(login)},id,full_name_ar,full_name_en,${q(hash)},${q(encrypted)},${q(encrypted)},true,NULL,gen_random_uuid(),'NotSent',0,false,'ar',0,${q(`UI-${runId}-موظف بريد جماعي 911116`)},true FROM source_employee RETURNING id
 )
 INSERT INTO zulms_rebuild.user_roles(user_id,role_id,scope_center_id,is_active) SELECT new_user.id,r.id,NULL,true FROM new_user CROSS JOIN zulms_rebuild.roles r WHERE r.code='Member';
 COMMIT;`;
 const file=join(root,'seed-adm14-fixture.sql'); mkdirSync(dirname(file),{recursive:true}); writeFileSync(file,sql,'utf8');
 execFileSync(psql,['-h',process.env.PGHOST??'127.0.0.1','-p',process.env.PGPORT??'5432','-U',process.env.PGUSER??'postgres','-w','-d',database,'-v','ON_ERROR_STOP=1','-f',file],{env,encoding:'utf8',stdio:['ignore','ignore','pipe']});
}
const readJson=p=>JSON.parse(readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const manifest=readJson(manifestPath); manifest.accounts=manifest.accounts.filter(x=>x.fixture!=='N4');
manifest.accounts.push({fixture:'N4',loginName:login,accountType:'Employee',firstLoginCompleted:false,email:'test-only-sink-address',roles:['Member'],purpose:'ADM13 resend and ADM14 bulk send'});
manifest.status='ADM-FIXTURES-READY'; manifest.reason='N3/N4 employee fixtures are ready; E1 still requires the quality-request UI journey.';
writeFileSync(manifestPath,JSON.stringify(manifest,null,2)+'\n','utf8');
console.log(JSON.stringify({status:'ready',fixture:'N4',login,email:'test-only-sink-address',alreadyPresent:present[0]==='t',secretsIncluded:false}));
