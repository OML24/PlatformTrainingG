param([switch]$SetupOnly)
$ErrorActionPreference = 'Stop'

# Starts the web app against the local zu_lms database for the API harness.
# Supply credentials and keys through environment variables before invoking.

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$storage = Join-Path $env:TEMP 'zulms-api-tests\storage'

if (-not $env:ASPNETCORE_ENVIRONMENT) { $env:ASPNETCORE_ENVIRONMENT = 'Development' }
if (-not $env:ASPNETCORE_URLS) { $env:ASPNETCORE_URLS = 'https://localhost:7275' }
foreach ($required in @('ZU_DB_CONNECTION', 'ZU_JWT_KEY', 'ZU_ENC_KEYS', 'ZU_BOOTSTRAP_ADMIN_PASSWORD')) {
    if (-not [Environment]::GetEnvironmentVariable($required)) { throw "$required is required for the API harness." }
}
if (-not $env:ZU_STORAGE_ROOT) { $env:ZU_STORAGE_ROOT = $storage }
if (-not $env:ZU_PUBLIC_BASE_URL) { $env:ZU_PUBLIC_BASE_URL = 'https://localhost:7275' }
if (-not $env:ZU_BOOTSTRAP_ADMIN_LOGIN) { $env:ZU_BOOTSTRAP_ADMIN_LOGIN = 'admin' }
if (-not $env:ZU_BOOTSTRAP_ADMIN_NAME) { $env:ZU_BOOTSTRAP_ADMIN_NAME = 'System Administrator' }

New-Item -ItemType Directory -Force -Path $env:ZU_STORAGE_ROOT | Out-Null
$configuration = if ($env:ZU_TEST_CONFIGURATION) { $env:ZU_TEST_CONFIGURATION } else { 'Debug' }
if ($SetupOnly) { return }
Set-Location (Join-Path $repo 'src\ZuLms.Web')
dotnet run -c $configuration --no-launch-profile --no-build 2>&1
