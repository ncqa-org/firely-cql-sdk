<#
.SYNOPSIS
    Packs and publishes all NuGet packages from the local machine.

.DESCRIPTION
    Builds the solution in Release mode, packs all packable projects,
    and optionally pushes them to nuget.org or a custom NuGet feed.

.PARAMETER NuGetApiKey
    The API key for the NuGet feed. Required for push.

.PARAMETER Source
    The NuGet feed URL. Defaults to https://api.nuget.org/v3/index.json

.PARAMETER SkipPush
    If set, only packs without pushing. Useful for verifying packages locally.

.PARAMETER SkipDuplicate
    If set, silently skips packages that already exist on the feed.

.EXAMPLE
    # Pack only (no push) - inspect packages in ./artifacts
    .\publish-nuget.ps1 -SkipPush

.EXAMPLE
    # Pack and push to nuget.org
    .\publish-nuget.ps1 -NuGetApiKey "your-api-key-here"

.EXAMPLE
    # Pack and push to a custom feed
    .\publish-nuget.ps1 -NuGetApiKey "your-api-key-here" -Source "https://nuget.pkg.github.com/ncqa-org/index.json"
#>

param(
    [string]$NuGetApiKey,
    [string]$Source = "https://api.nuget.org/v3/index.json",
    [switch]$SkipPush,
    [switch]$SkipDuplicate
)

$ErrorActionPreference = "Stop"

$artifactsDir = Join-Path $PSScriptRoot "artifacts"

# Clean artifacts folder
if (Test-Path $artifactsDir) {
    Remove-Item -Recurse -Force $artifactsDir
}
New-Item -ItemType Directory -Path $artifactsDir | Out-Null

Write-Host "=== Building solution in Release mode ===" -ForegroundColor Cyan
dotnet build --configuration Release /p:ContinuousIntegrationBuild=true
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed."
    exit 1
}

Write-Host ""
Write-Host "=== Packing NuGet packages ===" -ForegroundColor Cyan
dotnet pack --configuration Release --no-build --output $artifactsDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "Pack failed."
    exit 1
}

$packages = Get-ChildItem -Path $artifactsDir -Filter "*.nupkg"
Write-Host ""
Write-Host "=== Packages created ===" -ForegroundColor Green
$packages | ForEach-Object { Write-Host "  $_" }

if ($SkipPush) {
    Write-Host ""
    Write-Host "SkipPush specified. Packages are in: $artifactsDir" -ForegroundColor Yellow
    exit 0
}

if (-not $NuGetApiKey) {
    Write-Error "NuGetApiKey is required for push. Use -SkipPush to pack without pushing, or provide -NuGetApiKey."
    exit 1
}

Write-Host ""
Write-Host "=== Pushing packages to $Source ===" -ForegroundColor Cyan

$skipDuplicateArg = if ($SkipDuplicate) { "--skip-duplicate" } else { "" }

foreach ($pkg in $packages) {
    Write-Host "Pushing $($pkg.Name)..."
    $output = dotnet nuget push $pkg.FullName --api-key $NuGetApiKey --source $Source $skipDuplicateArg 2>&1
    $pushExitCode = $LASTEXITCODE
    $outputStr = $output -join "`n"

    if ($pushExitCode -eq 0) {
        if ($outputStr -match "already exists") {
            Write-Warning "SKIPPED (already exists on feed): $($pkg.Name)"
        } else {
            Write-Host "  Pushed successfully." -ForegroundColor Green
        }
    } else {
        Write-Host $outputStr -ForegroundColor Red
        Write-Error "FAILED to push $($pkg.Name)"
        exit 1
    }
}

# Push symbol packages if they exist
$symbolPackages = Get-ChildItem -Path $artifactsDir -Filter "*.snupkg"
if ($symbolPackages.Count -gt 0) {
    Write-Host ""
    Write-Host "=== Pushing symbol packages ===" -ForegroundColor Cyan
    foreach ($pkg in $symbolPackages) {
        Write-Host "Pushing $($pkg.Name)..."
        dotnet nuget push $pkg.FullName --api-key $NuGetApiKey --source $Source $skipDuplicateArg
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Failed to push symbol package $($pkg.Name). Continuing..."
        }
    }
}

Write-Host ""
Write-Host "=== Done! All packages published. ===" -ForegroundColor Green
