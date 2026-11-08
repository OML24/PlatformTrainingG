param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$web = Join-Path $repo 'src\ZuLms.Web'
$exe = Join-Path $web "bin\$Configuration\net10.0\ZuLms.Web.exe"
$port = 7275
$listener = Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue | Select-Object -First 1
if ($listener) {
    $existing = Get-CimInstance Win32_Process -Filter "ProcessId=$($listener.OwningProcess)"
    if (-not $existing.ExecutablePath -or -not $existing.ExecutablePath.StartsWith($web, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Port $port is occupied by another application (PID $($listener.OwningProcess))."
    }
    $health = Invoke-WebRequest -Uri 'https://localhost:7275/health' -UseBasicParsing -TimeoutSec 5
    if ($health.StatusCode -eq 200) {
        Write-Output "ZU-LMS is already running at https://localhost:$port/login (PID $($listener.OwningProcess))."
        return
    }
    throw "ZU-LMS is listening on port $port but failed its health check."
}

if (-not (Test-Path -LiteralPath $exe)) { throw "Build the $Configuration web project first: $exe" }
$env:ZU_TEST_CONFIGURATION = $Configuration
. (Join-Path $PSScriptRoot 'run-app.ps1') -SetupOnly
$out = Join-Path $env:TEMP 'zulms-local-7275.out.log'
$err = Join-Path $env:TEMP 'zulms-local-7275.err.log'
$process = Start-Process -FilePath $exe -WorkingDirectory $web -WindowStyle Hidden `
    -RedirectStandardOutput $out -RedirectStandardError $err -PassThru
for ($attempt = 0; $attempt -lt 120; $attempt++) {
    if ($process.HasExited) { throw "ZU-LMS stopped during startup. See $err" }
    try {
        $health = Invoke-WebRequest -Uri 'https://localhost:7275/health' -UseBasicParsing -TimeoutSec 2
        if ($health.StatusCode -eq 200) {
            Write-Output "ZU-LMS is running at https://localhost:$port/login (PID $($process.Id))."
            return
        }
    } catch { Start-Sleep -Milliseconds 500 }
}
throw "ZU-LMS did not become healthy within 60 seconds. See $err"
