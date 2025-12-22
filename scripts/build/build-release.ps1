#!/usr/bin/env pwsh

# Build script for WinServiceManager
# This script builds the project in Release configuration and prepares it for distribution

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [Parameter(Mandatory=$false)]
    [ValidateSet("x64", "x86", "AnyCPU")]
    [string]$Architecture = "x64",

    [Parameter(Mandatory=$false)]
    [switch]$Publish,

    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "publish"
)

Write-Host "Starting build process..." -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
Write-Host "Architecture: $Architecture" -ForegroundColor Cyan

# Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
dotnet clean src/WinServiceManager.sln -c $Configuration -p:Platform=$Architecture

# Restore packages
Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore src/WinServiceManager.sln

# Build the project
Write-Host "Building solution..." -ForegroundColor Yellow
dotnet build src/WinServiceManager.sln -c $Configuration -p:Platform=$Architecture --no-restore

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed!"
    exit 1
}

# Publish if requested
if ($Publish) {
    Write-Host "Publishing application..." -ForegroundColor Yellow

    $rid = "win-$($Architecture.ToLower())"
    $publishPath = Join-Path $OutputPath $rid

    dotnet publish src/WinServiceManager/WinServiceManager.csproj `
        -c $Configuration `
        -r $rid `
        --self-contained false `
        -p:PublishSingleFile=true `
        -p:PublishReadyToRun=true `
        -o $publishPath

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Publish failed!"
        exit 1
    }

    # Copy additional files
    Write-Host "Copying additional files..." -ForegroundColor Yellow

    # Create templates directory
    $templatesDir = Join-Path $publishPath "templates"
    if (-not (Test-Path $templatesDir)) {
        New-Item -ItemType Directory -Path $templatesDir | Out-Null
    }

    # Copy WinSW executable if exists
    $winswSource = "templates/WinSW-x64.exe"
    $winswDest = Join-Path $templatesDir "WinSW-x64.exe"

    if (Test-Path $winswSource) {
        Copy-Item $winswSource $winswDest -Force
        Write-Host "Copied WinSW executable" -ForegroundColor Green
    } else {
        Write-Warning "WinSW executable not found at $winswSource"
    }

    # Create distribution package
    $version = (Select-String -Path "src/WinServiceManager/WinServiceManager.csproj" -Pattern "AssemblyVersion>.*?([0-9.]+)").Matches.Groups[1].Value
    $packageName = "WinServiceManager-v$version-$rid"
    $packagePath = "$OutputPath\$packageName"

    if (Test-Path $packagePath) {
        Remove-Item -Path $packagePath -Recurse -Force
    }

    New-Item -ItemType Directory -Path $packagePath | Out-Null

    # Copy published files
    Copy-Item -Path "$publishPath\*" -Destination $packagePath -Recurse

    # Create README for distribution
    $readmeContent = @"
# WinServiceManager v$version

## 系统要求
- Windows 10/11 或 Windows Server 2019/2022
- .NET 8 Runtime
- 管理员权限

## 安装说明
1. 以管理员身份运行 WinServiceManager.exe
2. 首次运行会自动创建必要的目录结构
3. 确保 templates\WinSW-x64.exe 文件存在

## 使用说明
详见项目文档：https://github.com/LiteHomeLab/windows_services_manager

## 发布日期
$(Get-Date -Format "yyyy-MM-dd")
"@

    $readmeContent | Out-File -FilePath "$packagePath\README.txt" -Encoding UTF8

    # Create ZIP package
    $zipPath = "$OutputPath\$packageName.zip"
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    Compress-Archive -Path "$packagePath\*" -DestinationPath $zipPath

    Write-Host "Distribution package created: $zipPath" -ForegroundColor Green
    Write-Host "Package contents:" -ForegroundColor Cyan
    Get-ChildItem $packagePath | ForEach-Object {
        Write-Host "  $($_.Name)" -ForegroundColor White
    }
}

Write-Host "Build process completed successfully!" -ForegroundColor Green