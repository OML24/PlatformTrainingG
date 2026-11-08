param(
    [string]$PostgresBin = 'C:\Program Files\PostgreSQL\18\bin',
    [int]$HttpsPort = 7276,
    [int]$HttpPort = 5276
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (Get-NetTCPConnection -State Listen -LocalPort $HttpsPort,$HttpPort -ErrorAction SilentlyContinue) {
    throw 'An isolated test port is already occupied; no process was stopped.'
}
. (Join-Path $repo 'tools\api-tests\run-app.ps1') -SetupOnly
$connection = New-Object System.Data.Common.DbConnectionStringBuilder
$connection.set_ConnectionString($env:ZU_DB_CONNECTION)
if ($connection.get_Item('Host') -notin @('127.0.0.1','localhost')) { throw 'This fixture requires local PostgreSQL.' }
$stamp = Get-Date -Format 'yyyyMMddHHmmss'
$database = 'zulms_sessiontest_' + $stamp
$output = Join-Path $repo ('.local-backups\session-tests\' + $stamp)
New-Item -ItemType Directory -Force $output | Out-Null
$env:PGPASSWORD = $connection.get_Item('Password')
$pg = Join-Path $PostgresBin 'psql.exe'
& $pg -X -w -h $connection.get_Item('Host') -p $connection.get_Item('Port') -U $connection.get_Item('Username') -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE $database;"
if ($LASTEXITCODE -ne 0) { throw 'Could not create a new isolated database.' }
$connection.set_Item('Database', $database)
$env:ZU_DB_CONNECTION = $connection.get_ConnectionString()
$env:ZU_BOOTSTRAP_ADMIN_LOGIN = 'session-admin-a'
$env:ZU_BOOTSTRAP_ADMIN_NAME = 'Fixture administrator A'
$env:ZU_BOOTSTRAP_ADMIN_PASSWORD = 'Fixture#' + [Guid]::NewGuid().ToString('N') + 'aA9!'
$env:ZU_SESSION_TEST_PASSWORD = $env:ZU_BOOTSTRAP_ADMIN_PASSWORD
$env:ZU_STORAGE_ROOT = Join-Path $output 'storage'
$env:ASPNETCORE_URLS = "https://localhost:$HttpsPort;http://localhost:$HttpPort"
$env:ASPNETCORE_HTTPS_PORT = [string]$HttpsPort
$env:ZU_PUBLIC_BASE_URL = "https://localhost:$HttpsPort"
$env:ZU_SESSION_TEST_BASE = $env:ZU_PUBLIC_BASE_URL
# Trust only the installed local development certificate; do not disable TLS validation.
$certificate = Get-ChildItem Cert:\CurrentUser\My | Where-Object {
    $_.Subject -eq 'CN=localhost' -and $_.Extensions.Oid.Value -contains '1.3.6.1.4.1.311.84.1.1'
} | Sort-Object NotAfter -Descending | Select-Object -First 1
if (-not $certificate) { throw 'Install/trust an ASP.NET Core development HTTPS certificate first.' }
$publicCert = Join-Path $output 'localhost-public.pem'
('-----BEGIN CERTIFICATE-----' + "`n" + [Convert]::ToBase64String($certificate.RawData, [Base64FormattingOptions]::InsertLineBreaks) + "`n-----END CERTIFICATE-----") | Set-Content $publicCert -Encoding ascii
$env:NODE_EXTRA_CA_CERTS = $publicCert
$dll = Join-Path $repo 'src\ZuLms.Web\bin\Debug\net10.0\ZuLms.Web.dll'
if (-not (Test-Path $dll)) { throw 'Build Debug before running this isolated fixture.' }
$process = Start-Process dotnet -ArgumentList "`"$dll`"" -WorkingDirectory (Join-Path $repo 'src\ZuLms.Web') -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $output 'app.log') -RedirectStandardError (Join-Path $output 'app.err.log')
try {
    $healthy = $false
    for ($attempt = 0; $attempt -lt 120; $attempt++) {
        if ($process.HasExited) { throw 'Isolated application stopped during startup; inspect its local logs.' }
        try {
            if ((Invoke-WebRequest "http://localhost:$HttpPort/health" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200) { $healthy = $true; break }
        } catch { Start-Sleep -Milliseconds 250 }
    }
    if (-not $healthy) { throw 'Isolated application did not become healthy.' }
    $sql = @'
INSERT INTO zulms_rebuild.employees
 (side_code,employee_code,full_name_ar,main_job_code,emp_status,is_academic,search_text,source_missing)
VALUES (320,990091,'Fixture member B',2,13,false,'fixture member b',false),
 (320,990092,'Fixture first login C',2,13,false,'fixture first login c',false);
INSERT INTO zulms_rebuild.users
 (account_type,employee_id,login_name,full_name_ar,password_hash,password_enc,initial_password_enc,security_stamp,
  preferred_language,credentials_email_status,credentials_email_attempts,is_active,must_change_password,
  first_login_completed_at,password_changed_at,access_override_allow,failed_login_count,search_text)
SELECT 'Employee',e.id,e.employee_code::text,e.full_name_ar,a.password_hash,a.password_enc,a.password_enc,gen_random_uuid(),
 'en','NotSent',0,true,e.employee_code=990092,CASE WHEN e.employee_code=990092 THEN NULL ELSE now() END,now(),false,0,e.search_text
FROM zulms_rebuild.users a CROSS JOIN zulms_rebuild.employees e
WHERE a.login_name='session-admin-a' AND e.employee_code IN (990091,990092) AND e.side_code=320;
INSERT INTO zulms_rebuild.user_roles (user_id,role_id,is_active)
SELECT u.id,r.id,true FROM zulms_rebuild.users u CROSS JOIN zulms_rebuild.roles r
WHERE u.login_name IN ('990091','990092') AND r.code='Member';
'@
    & $pg -X -w -h $connection.get_Item('Host') -p $connection.get_Item('Port') -U $connection.get_Item('Username') -d $database -v ON_ERROR_STOP=1 -c $sql
    if ($LASTEXITCODE -ne 0) { throw 'Isolated fixture insertion failed.' }
    Remove-Item Env:PGPASSWORD
    node (Join-Path $PSScriptRoot 'session-api-regression.mjs')
    if ($LASTEXITCODE -ne 0) { throw 'Session API regression failed.' }
    Write-Output "Isolated fixture database retained for inspection: $database. No application database was changed."
} finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id }
}
