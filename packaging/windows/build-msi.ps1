$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot | Split-Path -Parent
$csproj = Join-Path $repoRoot "fuji-barcode.csproj"
$publishRoot = Join-Path $repoRoot "artifacts\publish"
$publishDir = Join-Path $publishRoot ("win-x64-" + [guid]::NewGuid().ToString("N"))
$installerDir = Join-Path $repoRoot "artifacts\installers"
$wxsFile = Join-Path $PSScriptRoot "Product.wxs"

if (-not (Test-Path $csproj)) {
    Write-Error "Project file not found: $csproj"
    exit 1
}

try {
    New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null

    Write-Host "Restoring win-x64 assets..."
    dotnet restore $csproj -r win-x64
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet restore failed"
        exit 1
    }

    Write-Host "Publishing self-contained win-x64..."
    dotnet publish $csproj -c Release -r win-x64 --self-contained true -o $publishDir
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet publish failed"
        exit 1
    }

    $appsettingsDefaultPath = Join-Path $publishDir "appsettings.default.json"
    if (-not (Test-Path $appsettingsDefaultPath)) {
        Write-Error "appsettings.default.json not found in publish output: $appsettingsDefaultPath"
        exit 1
    }

    Write-Host "Restoring local tools..."
    Push-Location $repoRoot
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        Pop-Location
        Write-Error "dotnet tool restore failed"
        exit 1
    }
    Pop-Location

    New-Item -ItemType Directory -Path $installerDir -Force | Out-Null

    $msiOutput = Join-Path $installerDir "fuji-barcode-win-x64.msi"

    Write-Host "Building MSI..."
    Push-Location $repoRoot
    dotnet tool run wix eula accept wix7
    if ($LASTEXITCODE -ne 0) {
        Pop-Location
        Write-Error "WiX EULA acceptance failed"
        exit 1
    }

    dotnet tool run wix build $wxsFile -o $msiOutput -d "PublishDir=$publishDir"
    if ($LASTEXITCODE -ne 0) {
        Pop-Location
        Write-Error "WiX build failed"
        exit 1
    }
    Pop-Location

    Write-Host "MSI created: $msiOutput"
}
finally {
    if (Test-Path $publishDir) {
        Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
