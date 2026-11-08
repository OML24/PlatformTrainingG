// HTTPS-only loopback source emulator for ADM20-23.
// It snapshots records from the isolated UI database and never relays data externally.
import https from 'node:https';
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';

const runId=process.env.ZU_UI_RUN_ID??'20261101-141731';
const database=process.env.ZU_TEST_DB??'';
if(database!==`zu_lms_ui_${runId.replaceAll('-','_')}`) throw new Error('Refusing non-UI database.');
const pfxPath=process.env.ZU_SYNC_MOCK_PFX??'';
const passphrase=process.env.ZU_SYNC_MOCK_PFX_PASSWORD??'';
if(!pfxPath||!passphrase) throw new Error('A loopback development PFX and password are required.');
const port=Number(process.env.ZU_SYNC_MOCK_PORT??'5443');
const psql=process.env.PG_BIN?`${process.env.PG_BIN}\\psql.exe`:'C:\\Program Files\\PostgreSQL\\18\\bin\\psql.exe';
const env={...process.env,PGPASSWORD:process.env.PGPASSWORD??'',PGCLIENTENCODING:'UTF8'};
const query=`
WITH imported AS (SELECT side_code FROM zulms_rebuild.university_sides WHERE is_imported),
center_rows AS (
 SELECT COALESCE(json_agg(json_build_object('side',c.side_code,'center_id',c.center_code,
  'center_dscp',c.name_ar,'unit_id',c.unit_code,'centr_head',c.head_employee_code,
  'fac_registrar',c.registrar_employee_code) ORDER BY c.side_code,c.center_code),'[]'::json) AS rows
 FROM zulms_rebuild.centers c JOIN imported i ON i.side_code=c.side_code WHERE NOT c.source_missing
),
department_rows AS (
 SELECT COALESCE(json_agg(json_build_object('side',d.side_code,'department_id',d.department_code,
  'department_dscp',d.name_ar,'center_id',d.center_code,'reg_code',d.reg_code,
  'gate_dep_head',d.head_employee_code) ORDER BY d.side_code,d.department_code),'[]'::json) AS rows
 FROM zulms_rebuild.departments d JOIN imported i ON i.side_code=d.side_code WHERE NOT d.source_missing
),
employee_rows AS (
 SELECT COALESCE(json_agg(json_build_object('side',e.side_code,'employee_id',e.employee_code,
  'fname',COALESCE(e.first_name,tokens[1]),'sname',COALESCE(e.second_name,tokens[2]),
  'tname',COALESCE(e.third_name,tokens[3]),'lname',COALESCE(e.last_name,
    CASE WHEN array_length(tokens,1)>3 THEN array_to_string(tokens[4:array_length(tokens,1)],' ') END),
  'ename',e.full_name_en,'side_desc',NULL,'center_id',e.center_code,'department_id',e.department_code,
  'main_job_code',e.main_job_code,'main_job_dscp',e.main_job_desc,'sub_job_id',e.sub_job_code,
  'sub_job_dscp',e.sub_job_desc,'emp_status',e.emp_status,'emp_status_desc',e.emp_status_desc,
  'chk_emp_stat',e.chk_emp_stat,'start_work_date',e.start_work_date,'stop_work_date',e.stop_work_date)
  ORDER BY e.side_code,e.employee_code),'[]'::json) AS rows
 FROM (SELECT e.*,regexp_split_to_array(e.full_name_ar,'\\s+') AS tokens FROM zulms_rebuild.employees e) e
 JOIN imported i ON i.side_code=e.side_code WHERE NOT e.source_missing
),
email_rows AS (
 SELECT COALESCE(json_agg(json_build_object('side_id',e.side_code,'employee_id',e.employee_code,
   'email',e.source_email) ORDER BY e.side_code,e.employee_code),'[]'::json) AS rows
 FROM zulms_rebuild.employees e JOIN imported i ON i.side_code=e.side_code
 WHERE NOT e.source_missing AND e.source_email IS NOT NULL AND e.source_email<>''
),
program_rows AS (
 SELECT COALESCE(json_agg(json_build_object('departmentid',d.reg_code,'facultyid',COALESCE(d.center_code::text,''),
   'departmentnamee',d.name_en,'department_status',CASE WHEN d.is_active THEN 1 ELSE 0 END)
   ORDER BY d.reg_code),'[]'::json) AS rows
 FROM zulms_rebuild.departments d WHERE d.side_code=320 AND d.reg_code IS NOT NULL AND NOT d.source_missing
)
SELECT json_build_object('centers',center_rows.rows,'departments',department_rows.rows,
 'employees',employee_rows.rows,'emails',email_rows.rows,'programs',program_rows.rows)::text
FROM center_rows,department_rows,employee_rows,email_rows,program_rows`;
const baseline=JSON.parse(execFileSync(psql,['-h',process.env.PGHOST??'127.0.0.1','-p',process.env.PGPORT??'5432',
  '-U',process.env.PGUSER??'postgres','-w','-d',database,'-At','-c',query],{env,encoding:'utf8'}));
let mode='success', slowMs=0;
const hits={centers:0,departments:0,employees:0,emails:0,programs:0};
const clone=data=>JSON.parse(JSON.stringify(data));
const json=(res,status,data)=>{res.writeHead(status,{'content-type':'application/json; charset=utf-8','cache-control':'no-store'});res.end(JSON.stringify(data));};
async function body(req){let data='';for await(const chunk of req)data+=chunk;return data?JSON.parse(data):{};}
function payloadFor(source,offset){
  const rows=clone(baseline[source]);
  if(mode==='empty_suspicious'&&source==='centers') return [];
  if(mode==='missing_employee'&&source==='employees') return rows.filter(x=>x.employee_id!==911114);
  if(mode==='mixed_reviews'){
    if(source==='employees'){
      const filtered=rows.filter(x=>x.employee_id!==911114);
      const n5=filtered.find(x=>x.employee_id===911117);
      if(n5){n5.center_id=9999;n5.department_id=9999;n5.emp_status=12;n5.emp_status_desc='Test changed status';}
      filtered.push({side:91,employee_id:8000,fname:'Source',sname:'Collision',tname:null,lname:'Test',ename:'Sync Collision',side_desc:'UI test source',center_id:9101,department_id:9110,main_job_code:1,main_job_dscp:'UI fixture',sub_job_id:null,sub_job_dscp:null,emp_status:13,emp_status_desc:'Active',chk_emp_stat:null,start_work_date:null,stop_work_date:null});
      return filtered;
    }
    if(source==='emails'){
      const one=rows.find(x=>x.employee_id===911115),two=rows.find(x=>x.employee_id===911116),five=rows.find(x=>x.employee_id===911117);
      if(one)one.email='shared-adm22@example.test';
      if(two)two.email='shared-adm22@example.test';
      if(five){five.email=five.email??'adm15-n5@example.test';rows.push({...five});}
    }
  }
  if(mode==='duplicate_key'&&source==='emails'&&rows.length){rows.push({...rows[0]});}
  if(mode==='page_failure'&&source==='employees') return {notItems:[]};
  return rows;
}
const server=https.createServer({pfx:readFileSync(pfxPath),passphrase},async(req,res)=>{
  const url=new URL(req.url,'https://localhost');
  if(req.method==='GET'&&url.pathname==='/_health') return json(res,200,{ready:true,mode,hits});
  if(req.method==='POST'&&url.pathname==='/_control'){
    const input=await body(req);
    const allowed=['success','slow_success','fail_employees','empty_suspicious','page_failure','missing_employee','duplicate_key','mixed_reviews'];
    if(!allowed.includes(input.mode)) return json(res,400,{error:'MODE_INVALID'});
    mode=input.mode;slowMs=input.mode==='slow_success'?Number(input.delayMs??4000):0;
    return json(res,200,{mode,slowMs});
  }
  const segment=url.pathname.split('/').filter(Boolean).at(-1);
  const source=segment==='email'?'emails':segment==='program'?'programs':segment;
  if(!Object.hasOwn(baseline,source)) return json(res,404,{error:'SOURCE_NOT_FOUND'});
  hits[source]++;
  if(mode==='fail_employees'&&source==='employees') return json(res,503,{error:'TEST_SOURCE_FAILURE'});
  if(slowMs>0) await new Promise(resolve=>setTimeout(resolve,slowMs));
  const offset=Number(url.searchParams.get('offset')??'0');
  const payload=payloadFor(source,offset);
  if(payload&& !Array.isArray(payload)) return json(res,200,payload);
  const page=payload.slice(offset,offset+Number(url.searchParams.get('limit')??'500'));
  return json(res,200,{items:page,hasMore:offset+page.length<payload.length});
});
server.listen(port,'::',()=>console.log(JSON.stringify({status:'ready',host:'localhost',port,mode,secretsIncluded:false})));
