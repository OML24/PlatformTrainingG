// Adds one isolated pre-first-login employee fixture for the shared-initial test.
import { execFileSync } from 'node:child_process';
import { createCipheriv, pbkdf2Sync, randomBytes } from 'node:crypto';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';

const runId=process.env.ZU_UI_RUN_ID??'20261101-141731', database=process.env.ZU_TEST_DB??'';
if(database!==`zu_lms_ui_${runId.replaceAll('-','_')}`) throw new Error('Refusing non-UI database.');
const config=JSON.parse(process.env.ZU_ENC_KEYS??'{}'),keyId=config.current;
const key=keyId&&config.keys?.[keyId]?Buffer.from(config.keys[keyId],'base64'):null;
if(!key||key.length!==32) throw new Error('Missing active encryption key.');
const root=process.env.ZU_UI_SECRET_ROOT,manifestPath=process.env.ZU_UI_MANIFEST;
if(!root||!manifestPath) throw new Error('Secret root and manifest paths are required.');
const login='911117',employeeCode=911117,q=x=>`'${String(x).replaceAll("'","''")}'`;
const alphabet='ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%';
const chars=['A','a','7','!',...[...randomBytes(24)].map(x=>alphabet[x%alphabet.length])];
for(let i=chars.length-1;i>0;i--){const j=randomBytes(1)[0]%(i+1);[chars[i],chars[j]]=[chars[j],chars[i]];}
const password=chars.join(''),salt=randomBytes(16);
const hash=`v1$600000$${salt.toString('base64')}$${pbkdf2Sync(Buffer.from(password.normalize('NFC')),salt,600000,32,'sha256').toString('base64')}`;
const nonce=randomBytes(12),cipher=createCipheriv('aes-256-gcm',key,nonce,{authTagLength:16});
cipher.setAAD(Buffer.from('zu-lms-v1','utf8'));
const bytes=Buffer.concat([cipher.update(Buffer.from(password.normalize('NFC'))),cipher.final()]);
const encrypted=`v1:${keyId}:${nonce.toString('base64')}:${bytes.toString('base64')}:${cipher.getAuthTag().toString('base64')}`;
const psql=join(process.env.PG_BIN??'C:\\Program Files\\PostgreSQL\\18\\bin','psql.exe');
const env={...process.env,PGPASSWORD:process.env.PGPASSWORD??'',PGCLIENTENCODING:'UTF8'};
const exists=execFileSync(psql,['-h',process.env.PGHOST??'127.0.0.1','-p',process.env.PGPORT??'5432','-U',process.env.PGUSER??'postgres','-w','-d',database,'-At','-F','|','-c',
 `SELECT EXISTS(SELECT 1 FROM zulms_rebuild.users WHERE login_name=${q(login)}),EXISTS(SELECT 1 FROM zulms_rebuild.employees WHERE side_code=91 AND employee_code=${employeeCode})`],{env,encoding:'utf8'}).trim().split('|');
if(exists[0]!==exists[1]) throw new Error('ADM15 fixture is partially present.');
if(exists[0]!=='t'){
 const name=`UI-${runId}-موظف كلمة مشتركة جديد`;
 const sql=`BEGIN;
 WITH source_employee AS (
  INSERT INTO zulms_rebuild.employees(side_code,employee_code,full_name_ar,full_name_en,center_code,department_code,center_id,department_id,main_job_code,main_job_desc,emp_status,emp_status_desc,is_academic,source_email,search_text,source_missing,last_seen_at)
  SELECT 91,${employeeCode},${q(name)},'ADM15 Shared Initial Employee',c.center_code,d.department_code,c.id,d.id,1,'UI fixture',13,'Active',true,NULL,${q(`${name} ADM15 Shared Initial Employee ${login}`)},false,now()
  FROM zulms_rebuild.centers c JOIN zulms_rebuild.departments d ON d.side_code=c.side_code AND d.center_id=c.id WHERE c.side_code=91 AND c.center_code=9101 AND d.department_code=9110
  RETURNING id,full_name_ar,full_name_en
 ), new_user AS (
  INSERT INTO zulms_rebuild.users(account_type,login_name,employee_id,full_name_ar,full_name_en,password_hash,password_enc,initial_password_enc,must_change_password,first_login_completed_at,security_stamp,credentials_email_status,credentials_email_attempts,access_override_allow,preferred_language,failed_login_count,search_text,is_active)
  SELECT 'Employee',${q(login)},id,full_name_ar,full_name_en,${q(hash)},${q(encrypted)},${q(encrypted)},true,NULL,gen_random_uuid(),'NotSent',0,false,'ar',0,${q(`${name} ${login}`)},true FROM source_employee RETURNING id
 )
 INSERT INTO zulms_rebuild.user_roles(user_id,role_id,scope_center_id,is_active) SELECT new_user.id,r.id,NULL,true FROM new_user CROSS JOIN zulms_rebuild.roles r WHERE r.code='Member';
 COMMIT;`;
 const file=join(root,'seed-adm15-fixture.sql');mkdirSync(dirname(file),{recursive:true});writeFileSync(file,sql,'utf8');
 execFileSync(psql,['-h',process.env.PGHOST??'127.0.0.1','-p',process.env.PGPORT??'5432','-U',process.env.PGUSER??'postgres','-w','-d',database,'-v','ON_ERROR_STOP=1','-f',file],{env,encoding:'utf8',stdio:['ignore','ignore','pipe']});
}
const manifest=JSON.parse(readFileSync(manifestPath,'utf8').replace(/^\uFEFF/,''));manifest.accounts=manifest.accounts.filter(x=>x.fixture!=='N5');
manifest.accounts.push({fixture:'N5',loginName:login,accountType:'Employee',firstLoginCompleted:false,email:'none',roles:['Member'],purpose:'ADM15 shared-initial password acceptance'});
manifest.status='ADM-FIXTURES-READY';manifest.reason='N3/N4/N5 fixtures are available; E1 was created through the Dean proposal and Quality request UI.';
writeFileSync(manifestPath,JSON.stringify(manifest,null,2)+'\n','utf8');
console.log(JSON.stringify({status:'ready',fixture:'N5',login,firstLoginCompleted:false,secretsIncluded:false,alreadyPresent:exists[0]==='t'}));
