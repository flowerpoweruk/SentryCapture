<#
    Sentry Capture - setup script
    - Ensures the .NET 8 SDK is installed (via winget if missing).
    - Publishes the WPF app as a single self-contained win-x64 executable.
    - Creates a desktop shortcut for one-click launching.
#>

$ErrorActionPreference = 'Stop'

function Write-Step($msg)  { Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)    { Write-Host "    $msg" -ForegroundColor Green }
function Write-Warn2($msg) { Write-Host "    $msg" -ForegroundColor Yellow }

# --- Resolve paths ---------------------------------------------------------
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot  = Split-Path -Parent $ScriptDir
$Project   = Join-Path $RepoRoot 'src\SentryCapture\SentryCapture.csproj'
$PublishDir = Join-Path $RepoRoot 'publish'

if (-not (Test-Path $Project)) {
    Write-Host "ERROR: Could not find project at $Project" -ForegroundColor Red
    exit 1
}

# --- Locate a usable dotnet (>= 8) -----------------------------------------
function Get-DotnetPath {
    # 1) On PATH?
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    # 2) Default install location?
    $candidate = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
    if (Test-Path $candidate) { return $candidate }

    return $null
}

function Test-Net8Sdk($dotnet) {
    if (-not $dotnet) { return $false }
    try {
        $sdks = & $dotnet --list-sdks 2>$null
        foreach ($line in $sdks) {
            if ($line -match '^\s*8\.') { return $true }
        }
    } catch { }
    return $false
}

Write-Step "Checking for the .NET 8 SDK..."
$dotnet = Get-DotnetPath

if (-not (Test-Net8Sdk $dotnet)) {
    Write-Warn2 ".NET 8 SDK not found. Installing via winget (this may take a few minutes)..."

    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if (-not $winget) {
        Write-Host "ERROR: winget is not available on this system." -ForegroundColor Red
        Write-Host "Please install the .NET 8 SDK manually from:" -ForegroundColor Red
        Write-Host "  https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Red
        exit 1
    }

    & winget install --id Microsoft.DotNet.SDK.8 --silent `
        --accept-package-agreements --accept-source-agreements
    # winget can return non-zero even on success (e.g. already installed); re-detect instead of trusting exit code.

    $dotnet = Get-DotnetPath
    if (-not (Test-Net8Sdk $dotnet)) {
        # PATH may not be refreshed in this session; try the default location explicitly.
        $candidate = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
        if (Test-Net8Sdk $candidate) { $dotnet = $candidate }
    }

    if (-not (Test-Net8Sdk $dotnet)) {
        Write-Host "ERROR: .NET 8 SDK still not detected after install." -ForegroundColor Red
        Write-Host "Please open a NEW terminal and run setup.bat again, or install manually:" -ForegroundColor Red
        Write-Host "  https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Red
        exit 1
    }
}

Write-Ok "Using dotnet: $dotnet"

# --- Build (publish single-file self-contained exe) ------------------------
Write-Step "Building Sentry Capture (this can take a minute on first run)..."

if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force -ErrorAction SilentlyContinue
}

& $dotnet publish $Project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed (dotnet publish exit code $LASTEXITCODE)." -ForegroundColor Red
    exit $LASTEXITCODE
}

$exe = Join-Path $PublishDir 'SentryCapture.exe'
if (-not (Test-Path $exe)) {
    Write-Host "ERROR: Build reported success but $exe was not found." -ForegroundColor Red
    exit 1
}

Write-Ok "Built: $exe"

# --- Desktop shortcut (best effort) ----------------------------------------
try {
    Write-Step "Creating a desktop shortcut..."
    $desktop  = [Environment]::GetFolderPath('Desktop')
    $lnkPath  = Join-Path $desktop 'Sentry Capture.lnk'
    $shell    = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($lnkPath)
    $shortcut.TargetPath       = $exe
    $shortcut.WorkingDirectory = $PublishDir
    $shortcut.Description       = 'Sentry Capture - traffic camera image collector'
    $shortcut.Save()
    Write-Ok "Shortcut created: $lnkPath"
} catch {
    Write-Warn2 "Could not create desktop shortcut (non-fatal): $($_.Exception.Message)"
}

Write-Host ""
Write-Ok "All done. Launch with launch.bat or the 'Sentry Capture' desktop shortcut."
exit 0
