#Requires -Version 5.1
<#
.SYNOPSIS
    Builds and packages the Light Query Profiler VS Code extension (.vsix).

.DESCRIPTION
    This script performs a full release build of the Light Query Profiler extension:
      1. Validates prerequisites (dotnet, node, npm)
      2. Cleans previous build outputs
      3. Publishes the .NET backend (JsonRpc + Shared) in Release mode (framework-dependent, AnyCPU)
      4. Converts the SVG icon to PNG 128x128 using the 'sharp' npm package
      5. Installs npm dependencies
      6. Compiles TypeScript to dist/
      7. Validates all required output files are present
      8. Packages the extension with vsce
      9. Reports the generated .vsix path, size, and install instructions

.NOTES
    Prerequisites:
      - .NET 10 SDK  : https://dotnet.microsoft.com/en-us/download/dotnet/10.0
      - Node.js 18+  : https://nodejs.org/
      - npm          : included with Node.js

    Before publishing to the VS Code Marketplace:
      - Set the correct publisher ID in vscode-extension/package.json
        (field: "publisher") matching your account at
        https://marketplace.visualstudio.com/manage/publishers
      - Obtain a Personal Access Token (PAT) from Azure DevOps and run:
        npx vsce login <your-publisher-id>

    Cross-platform support:
      The generated .vsix works on Windows, Linux, and macOS.
      The .NET backend is published as framework-dependent (AnyCPU), so users
      must have .NET 10 Runtime installed on their machine.
      Native libraries for all platforms (win-x64, linux-x64, linux-arm64,
      osx-x64, osx-arm64, etc.) are included automatically via the runtimes/
      directory produced by 'dotnet publish'.

.EXAMPLE
    .\build-vsix.ps1

.EXAMPLE
    .\build-vsix.ps1 -SkipClean -Verbose
#>

[CmdletBinding()]
param(
    # Skip cleaning previous build outputs (faster incremental builds)
    [switch]$SkipClean,

    # Skip running 'npm install' (use when node_modules is already up to date)
    [switch]$SkipNpmInstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

function Write-Step {
    param([int]$Number, [string]$Message)
    Write-Host ""
    Write-Host "[$Number/9] $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "  OK  $Message" -ForegroundColor Green
}

function Write-Fail {
    param([string]$Message)
    Write-Host "  FAIL  $Message" -ForegroundColor Red
}

function Write-Info {
    param([string]$Message)
    Write-Host "       $Message" -ForegroundColor Gray
}

function Assert-Command {
    param([string]$Name, [string]$InstallHint)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        Write-Fail "$Name not found in PATH."
        Write-Host "  Install: $InstallHint" -ForegroundColor Yellow
        exit 1
    }
    $version = & $Name --version 2>&1 | Select-Object -First 1
    Write-Success "$Name found: $version"
}

function Assert-FileExists {
    param([string]$FilePath, [string]$Description)
    if (-not (Test-Path $FilePath)) {
        Write-Fail "Missing required file: $Description"
        Write-Info "Expected at: $FilePath"
        exit 1
    }
    Write-Success "$Description"
}

# ---------------------------------------------------------------------------
# Paths
# ---------------------------------------------------------------------------

# Resolve repo root robustly: $PSScriptRoot is set when invoked as a .ps1 file,
# but falls back to the current directory when dot-sourced or called inline.
$RepoRoot       = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }
$SrcDir         = Join-Path $RepoRoot "src"
$ExtDir         = Join-Path $RepoRoot "vscode-extension"
$BinDir         = Join-Path $ExtDir "bin"
$DistDir        = Join-Path $ExtDir "dist"
$MediaDir       = Join-Path $ExtDir "media"
# Note: Join-Path in Windows PowerShell 5.1 only accepts two path arguments.
# Nesting calls is required for paths with more than one child segment.
$JsonRpcCsproj  = Join-Path (Join-Path $SrcDir "LightQueryProfiler.JsonRpc") "LightQueryProfiler.JsonRpc.csproj"
$IconSvg        = Join-Path $MediaDir "icon.svg"
$IconPng        = Join-Path $MediaDir "icon.png"

# ---------------------------------------------------------------------------
# Header
# ---------------------------------------------------------------------------

Write-Host ""
Write-Host "================================================" -ForegroundColor White
Write-Host "  Light Query Profiler - VSIX Build Script"      -ForegroundColor White
Write-Host "================================================" -ForegroundColor White
Write-Host "  Repo   : $RepoRoot"
Write-Host "  ExtDir : $ExtDir"
Write-Host ""

# ---------------------------------------------------------------------------
# STEP 1 — Validate prerequisites
# ---------------------------------------------------------------------------

Write-Step 1 "Validating prerequisites"

Assert-Command "dotnet" "https://dotnet.microsoft.com/en-us/download/dotnet/10.0"
Assert-Command "node"   "https://nodejs.org/"
Assert-Command "npm"    "https://nodejs.org/"

# Verify dotnet SDK version is 10.x
$dotnetSdkVersion = & dotnet --version 2>&1 | Select-Object -First 1
if ($dotnetSdkVersion -notmatch '^10\.') {
    Write-Host ""
    Write-Host "  WARN  dotnet SDK version is '$dotnetSdkVersion'. This project targets .NET 10." -ForegroundColor Yellow
    Write-Host "         Proceeding, but consider installing .NET 10 SDK." -ForegroundColor Yellow
}

# Verify the JsonRpc project file exists
if (-not (Test-Path $JsonRpcCsproj)) {
    Write-Fail "Project file not found: $JsonRpcCsproj"
    exit 1
}
Write-Success "LightQueryProfiler.JsonRpc.csproj found"

# Verify the SVG icon exists (needed for PNG conversion)
if (-not (Test-Path $IconSvg)) {
    Write-Fail "SVG icon not found: $IconSvg"
    exit 1
}
Write-Success "icon.svg found"

# ---------------------------------------------------------------------------
# STEP 2 — Clean previous outputs
# ---------------------------------------------------------------------------

Write-Step 2 "Cleaning previous build outputs"

if ($SkipClean) {
    Write-Info "Skipped (--SkipClean flag set)"
} else {
    # Remove bin/ (compiled .NET backend)
    if (Test-Path $BinDir) {
        Write-Info "Removing bin/ ..."
        Remove-Item $BinDir -Recurse -Force
        Write-Success "bin/ removed"
    } else {
        Write-Info "bin/ does not exist, nothing to clean"
    }

    # Remove dist/ (compiled TypeScript)
    if (Test-Path $DistDir) {
        Write-Info "Removing dist/ ..."
        Remove-Item $DistDir -Recurse -Force
        Write-Success "dist/ removed"
    } else {
        Write-Info "dist/ does not exist, nothing to clean"
    }

    # Remove any existing .vsix files in the extension directory
    $existingVsix = @(Get-ChildItem -Path $ExtDir -Filter "*.vsix" -ErrorAction SilentlyContinue)
    foreach ($vsix in $existingVsix) {
        Write-Info "Removing $($vsix.Name) ..."
        Remove-Item $vsix.FullName -Force
    }
    if ($existingVsix.Count -gt 0) {
        Write-Success "$($existingVsix.Count) old .vsix file(s) removed"
    }

    # Remove previously generated PNG icon (will be regenerated)
    if (Test-Path $IconPng) {
        Remove-Item $IconPng -Force
        Write-Info "Old icon.png removed"
    }
}

# ---------------------------------------------------------------------------
# STEP 3 — Publish .NET backend in Release mode
# ---------------------------------------------------------------------------

Write-Step 3 "Publishing .NET backend (Release, framework-dependent, AnyCPU)"
Write-Info "Projects: LightQueryProfiler.JsonRpc + LightQueryProfiler.Shared (via ProjectReference)"
Write-Info "Output  : $BinDir"
Write-Info ""

$publishArgs = @(
    "publish",
    $JsonRpcCsproj,
    "--configuration", "Release",
    "--no-self-contained",
    "--output", $BinDir
)

Write-Info "Running: dotnet $($publishArgs -join ' ')"
Write-Host ""

& dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Fail "dotnet publish failed with exit code $LASTEXITCODE"
    exit 1
}

Write-Host ""
Write-Success "dotnet publish completed"

# Quick sanity: count the DLLs published
$dllCount = (Get-ChildItem -Path $BinDir -Filter "*.dll" -Recurse).Count
Write-Info "$dllCount DLL(s) found in bin/"

# ---------------------------------------------------------------------------
# STEP 4 — Convert SVG icon to PNG 128x128
# ---------------------------------------------------------------------------

Write-Step 4 "Converting icon.svg to icon.png (128x128)"

# We use a small inline Node.js script that uses 'sharp'.
# 'sharp' may already be in node_modules after npm install, but since we run
# this step before npm install (to keep icon conversion independent), we use
# npx with --yes to auto-install sharp temporarily if needed.
# The sharp package is chosen because it works reliably on Windows/Linux/macOS
# and does not require any system-level libraries when installed via npm.

$convertScript = @"
const sharp = require('sharp');
const path  = require('path');
const src   = path.join(__dirname, 'media', 'icon.svg');
const dst   = path.join(__dirname, 'media', 'icon.png');
sharp(src)
  .resize(128, 128)
  .png()
  .toFile(dst)
  .then(() => {
    console.log('icon.png created successfully (128x128)');
    process.exit(0);
  })
  .catch(err => {
    console.error('Error converting icon:', err.message);
    process.exit(1);
  });
"@

# Write the inline script to a temp file inside the extension directory
# so that require('sharp') resolves from node_modules there (if present).
$tempScript = Join-Path $ExtDir "_icon_convert_temp.js"

try {
    Set-Content -Path $tempScript -Value $convertScript -Encoding UTF8

    Write-Info "Running icon conversion via Node.js + sharp..."

    # First attempt: use sharp from node_modules (if already installed)
    $sharpInNodeModules = Join-Path (Join-Path $ExtDir "node_modules") "sharp"
    if (Test-Path $sharpInNodeModules) {
        Write-Info "Using sharp from existing node_modules"
        & node $tempScript
    } else {
        # Install sharp temporarily via npx
        Write-Info "sharp not in node_modules, installing via npx (temporary)..."
        & npx --yes --prefix $ExtDir sharp-cli@latest --input $IconSvg --output $IconPng resize 128 128 2>$null
        if ($LASTEXITCODE -ne 0) {
            # Fallback: install sharp directly and run the script
            Write-Info "Falling back to: npm install --no-save sharp in extension dir..."
            Push-Location $ExtDir
            & npm install --no-save sharp 2>&1 | Out-Null
            Pop-Location
            & node $tempScript
        }
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Icon conversion failed. The .vsix cannot be packaged without icon.png."
        Write-Info "Manual alternative: convert media/icon.svg to a 128x128 PNG and save as media/icon.png"
        exit 1
    }

    if (-not (Test-Path $IconPng)) {
        Write-Fail "icon.png was not created at expected path: $IconPng"
        exit 1
    }

    $pngSize = (Get-Item $IconPng).Length
    Write-Success "icon.png created ($pngSize bytes)"
} finally {
    # Always clean up the temp script
    if (Test-Path $tempScript) {
        Remove-Item $tempScript -Force
    }
}

# ---------------------------------------------------------------------------
# STEP 5 — Install npm dependencies
# ---------------------------------------------------------------------------

Write-Step 5 "Installing npm dependencies"

if ($SkipNpmInstall) {
    Write-Info "Skipped (--SkipNpmInstall flag set)"
    if (-not (Test-Path (Join-Path $ExtDir "node_modules"))) {
        Write-Fail "node_modules not found and --SkipNpmInstall was set. Run without --SkipNpmInstall first."
        exit 1
    }
} else {
    Write-Info "Running: npm install"
    Push-Location $ExtDir
    try {
        & npm install
        if ($LASTEXITCODE -ne 0) {
            Write-Fail "npm install failed with exit code $LASTEXITCODE"
            exit 1
        }
    } finally {
        Pop-Location
    }
    Write-Success "npm install completed"
}

# ---------------------------------------------------------------------------
# STEP 6 — Compile TypeScript
# ---------------------------------------------------------------------------

Write-Step 6 "Bundling TypeScript with esbuild"
Write-Info "Running: npm run bundle"

Push-Location $ExtDir
try {
    & npm run bundle
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "esbuild bundle failed with exit code $LASTEXITCODE"
        Write-Info "Fix the errors above before packaging."
        exit 1
    }
} finally {
    Pop-Location
}

Write-Success "Extension bundled to dist/extension.js (all dependencies inlined)"

# ---------------------------------------------------------------------------
# STEP 7 — Validate required output files
# ---------------------------------------------------------------------------

Write-Step 7 "Validating build outputs"

$requiredFiles = @(
    @{ Path = (Join-Path $BinDir "LightQueryProfiler.JsonRpc.dll");                          Desc = "JsonRpc server DLL" },
    @{ Path = (Join-Path $BinDir "LightQueryProfiler.Shared.dll");                           Desc = "Shared library DLL" },
    @{ Path = (Join-Path $BinDir "LightQueryProfiler.JsonRpc.deps.json");                    Desc = "deps.json (dependency manifest)" },
    @{ Path = (Join-Path $BinDir "LightQueryProfiler.JsonRpc.runtimeconfig.json");           Desc = "runtimeconfig.json" },
    @{ Path = (Join-Path $BinDir "Microsoft.Data.SqlClient.dll");                            Desc = "Microsoft.Data.SqlClient.dll" },
    @{ Path = (Join-Path $BinDir "StreamJsonRpc.dll");                                       Desc = "StreamJsonRpc.dll" },
    @{ Path = (Join-Path (Join-Path (Join-Path (Join-Path $BinDir "runtimes") "win-x64") "native") "Microsoft.Data.SqlClient.SNI.dll"); Desc = "Native: win-x64 SqlClient SNI" },
    @{ Path = (Join-Path (Join-Path (Join-Path (Join-Path $BinDir "runtimes") "linux-x64") "native") "libe_sqlite3.so");               Desc = "Native: linux-x64 SQLite" },
    @{ Path = (Join-Path $DistDir "extension.js");                                           Desc = "Compiled extension entry point" },
    @{ Path = $IconPng;                                                                       Desc = "icon.png (128x128)" }
)

$allValid = $true
foreach ($item in $requiredFiles) {
    if (Test-Path $item.Path) {
        Write-Success $item.Desc
    } else {
        Write-Fail $item.Desc
        Write-Info "Missing: $($item.Path)"
        $allValid = $false
    }
}

if (-not $allValid) {
    Write-Host ""
    Write-Fail "One or more required files are missing. Aborting packaging."
    exit 1
}

Write-Host ""
Write-Info "All required files present."

# ---------------------------------------------------------------------------
# STEP 8 — Package with vsce
# ---------------------------------------------------------------------------

Write-Step 8 "Packaging extension with vsce"

# Determine version from package.json
$packageJson = Get-Content (Join-Path $ExtDir "package.json") -Raw | ConvertFrom-Json
$version     = $packageJson.version
$name        = $packageJson.name
$publisher   = $packageJson.publisher
$vsixName    = "$name-$version.vsix"
$vsixPath    = Join-Path $ExtDir $vsixName

Write-Info "Name      : $name"
Write-Info "Version   : $version"
Write-Info "Publisher : $publisher"
Write-Info "Output    : $vsixPath"

# Warn if publisher is still the placeholder
if ($publisher -eq "your-publisher-id") {
    Write-Host ""
    Write-Host "  WARN  'publisher' in package.json is still set to 'your-publisher-id'." -ForegroundColor Yellow
    Write-Host "         The .vsix will be created but cannot be published to the Marketplace" -ForegroundColor Yellow
    Write-Host "         without a valid publisher ID registered at:" -ForegroundColor Yellow
    Write-Host "         https://marketplace.visualstudio.com/manage/publishers" -ForegroundColor Yellow
    Write-Host ""
}

Write-Info "Running: npx vsce package --out $vsixName"
Write-Host ""

# vsce requires a LICENSE file in the extension directory.
# The repo has LICENSE.md at the root; copy it temporarily for packaging.
$rootLicense = Join-Path $RepoRoot "LICENSE.md"
$extLicense  = Join-Path $ExtDir "LICENSE.md"
$licenseWasCopied = $false
if ((Test-Path $rootLicense) -and (-not (Test-Path $extLicense))) {
    Copy-Item $rootLicense $extLicense
    $licenseWasCopied = $true
    Write-Info "LICENSE.md copied from repo root for packaging"
}

Push-Location $ExtDir
try {
    & npx vsce package --out $vsixName
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Fail "vsce package failed with exit code $LASTEXITCODE"
        Write-Info "Common causes:"
        Write-Info "  - Missing README.md in vscode-extension/"
        Write-Info "  - Invalid icon path in package.json"
        Write-Info "  - TypeScript errors not caught at compile time"
        exit 1
    }
} finally {
    Pop-Location
    # Clean up the temporarily copied LICENSE.md
    if ($licenseWasCopied -and (Test-Path $extLicense)) {
        Remove-Item $extLicense -Force
        Write-Info "Temporary LICENSE.md removed"
    }
}

# ---------------------------------------------------------------------------
# STEP 9 — Report result
# ---------------------------------------------------------------------------

Write-Step 9 "Build complete"

if (-not (Test-Path $vsixPath)) {
    Write-Fail ".vsix file not found at expected path: $vsixPath"
    exit 1
}

$vsixSize = (Get-Item $vsixPath).Length
$vsixSizeMB = [math]::Round($vsixSize / 1MB, 2)

Write-Host ""
Write-Host "================================================" -ForegroundColor Green
Write-Host "  VSIX created successfully!" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  File    : $vsixPath" -ForegroundColor White
Write-Host "  Size    : $vsixSizeMB MB ($vsixSize bytes)" -ForegroundColor White
Write-Host ""
Write-Host "  To install locally:" -ForegroundColor Cyan
Write-Host "    code --install-extension `"$vsixPath`"" -ForegroundColor White
Write-Host ""
Write-Host "  To publish to VS Code Marketplace:" -ForegroundColor Cyan
Write-Host "    1. Set publisher in vscode-extension/package.json" -ForegroundColor White
Write-Host "    2. npx vsce login <your-publisher-id>" -ForegroundColor White
Write-Host "    3. npx vsce publish" -ForegroundColor White
Write-Host ""
