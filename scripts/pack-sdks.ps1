<#
.SYNOPSIS
    Automated Packaging Script for Official SGrowing Client SDKs.
    Packs both the .NET 10 NuGet package and the TypeScript npm package into artifacts/packages/.

.DESCRIPTION
    1. Packs SGrowing.Client NuGet package to artifacts/packages/.
    2. Builds and packs @sgrowing/sdk npm package into artifacts/packages/.
#>

[CmdletBinding()]
param (
    [string]$Configuration = "Release",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = (Resolve-Path "$scriptDir\..").Path

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $rootDir "artifacts\packages"
} elseif (-not [System.IO.Path]::IsPathRooted($OutputDir)) {
    $OutputDir = Join-Path $rootDir $OutputDir
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  SGrowing Official Client SDK Packaging Pipeline" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Root Directory:       $rootDir"
Write-Host "Output Directory:     $OutputDir"
Write-Host "Build Configuration:  $Configuration"
Write-Host ""

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    Write-Host "[OK] Created artifacts directory: $OutputDir" -ForegroundColor Green
}

# -----------------------------------------------------------------------------
# 1. Pack .NET 10 Client SDK (NuGet)
# -----------------------------------------------------------------------------
$dotnetCsproj = Join-Path $rootDir "src\sdk\SGrowing.Client\SGrowing.Client.csproj"

Write-Host "----------------------------------------------------------" -ForegroundColor Yellow
Write-Host " 1/2 Packing .NET 10 SDK: SGrowing.Client..." -ForegroundColor Yellow
Write-Host "----------------------------------------------------------" -ForegroundColor Yellow

if (-not (Test-Path $dotnetCsproj)) {
    Write-Error "Project file not found: $dotnetCsproj"
}

$dotnetPackCmd = "dotnet pack `"$dotnetCsproj`" -c $Configuration -o `"$OutputDir`""
Write-Host "Executing: $dotnetPackCmd" -ForegroundColor DarkGray
Invoke-Expression $dotnetPackCmd

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to pack .NET SDK: SGrowing.Client (exit code: $LASTEXITCODE)"
}

Write-Host "[OK] .NET NuGet package successfully built and placed in $OutputDir" -ForegroundColor Green
Write-Host ""

# -----------------------------------------------------------------------------
# 2. Pack TypeScript / Node.js SDK (NPM)
# -----------------------------------------------------------------------------
$tsSdkDir = Join-Path $rootDir "packages\sdk-ts"

Write-Host "----------------------------------------------------------" -ForegroundColor Yellow
Write-Host " 2/2 Packing TypeScript SDK: @sgrowing/sdk..." -ForegroundColor Yellow
Write-Host "----------------------------------------------------------" -ForegroundColor Yellow

if (-not (Test-Path $tsSdkDir)) {
    Write-Error "TypeScript SDK directory not found: $tsSdkDir"
}

Push-Location $tsSdkDir
try {
    Write-Host "Building TypeScript distribution..." -ForegroundColor DarkGray
    npm run build
    if ($LASTEXITCODE -ne 0) {
        Write-Error "TypeScript compilation failed."
    }

    Write-Host "Packaging npm tarball..." -ForegroundColor DarkGray
    npm pack --pack-destination "$OutputDir"
    if ($LASTEXITCODE -ne 0) {
        Write-Error "npm pack failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

Write-Host "[OK] TypeScript npm tarball successfully created in $OutputDir" -ForegroundColor Green
Write-Host ""

# -----------------------------------------------------------------------------
# 3. Summary of Generated Packages
# -----------------------------------------------------------------------------
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Artifacts Generated in $OutputDir" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$packages = Get-ChildItem -Path $OutputDir -File | Where-Object { $_.Extension -in ".nupkg", ".snupkg", ".tgz" }
foreach ($pkg in $packages) {
    $sizeKb = [Math]::Round($pkg.Length / 1KB, 2)
    Write-Host "  * $($pkg.Name) ($sizeKb KB)" -ForegroundColor Green
}

Write-Host ""
Write-Host "[SUCCESS] Both client SDKs packaged successfully!" -ForegroundColor Green
