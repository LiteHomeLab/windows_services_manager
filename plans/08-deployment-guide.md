# 部署指南

## 部署概述

本文档描述了 WinServiceManager 的部署流程，包括应用程序打包、依赖项配置、安装程序创建和发布流程。

## 1. 应用程序打包

### 1.1 发布配置

#### 单文件自包含发布
```powershell
# build/publish.ps1
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDir = "publish"
)

Write-Host "Publishing WinServiceManager..." -ForegroundColor Green

# 清理输出目录
if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
}

# 发布应用程序
dotnet publish src/WinServiceManager/WinServiceManager.csproj `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    --output $OutputDir `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishReadyToRun=true `
    -p:TrimUnusedDependencies=true

# 复制 WinSW 可执行文件
if (-not (Test-Path $OutputDir/templates)) {
    New-Item -ItemType Directory -Path $OutputDir/templates
}

Write-Host "Copying WinSW executable..." -ForegroundColor Yellow
# 下载 WinSW（如果不存在）
$winSWPath = $OutputDir/templates/WinSW-x64.exe
if (-not (Test-Path $winSWPath)) {
    Write-Host "Downloading WinSW..." -ForegroundColor Yellow
    Invoke-WebRequest -Uri "https://github.com/winsw/winsw/releases/download/v3.0.0/WinSW-x64.exe" `
                    -OutFile $winSWPath
}

# 创建 services 目录
New-Item -ItemType Directory -Path $OutputDir/services -Force

# 创建配置目录
New-Item -ItemType Directory -Path $OutputDir/config -Force

# 复制默认配置文件
Copy-Item "config/appsettings.json" "$OutputDir/config/" -Force

Write-Host "Publish completed successfully!" -ForegroundColor Green
```

#### 依赖框架发布（更小的体积）
```powershell
dotnet publish src/WinServiceManager/WinServiceManager.csproj `
    --configuration Release `
    --runtime win-x64 `
    --output publish/framework-dependent `
    --no-self-contained
```

### 1.2 清理不必要的文件
```powershell
# build/cleanup.ps1
param(
    [string]$PublishDir = "publish"
)

# 移除不必要的文件
$filesToRemove = @(
    "*.pdb",
    "*.xml",
    "*.dev.json",
    "*.Development.json"
)

foreach ($pattern in $filesToRemove) {
    Get-ChildItem -Path $PublishDir -Filter $pattern -Recurse | Remove-Item -Force
}

# 移除语言资源（除了英语）
Get-ChildItem -Path $PublishDir -Recurse -Directory `
    | Where-Object { $_.Name -match "^[a-z]{2}(-[A-Z]{2})?$" -and $_.Name -ne "en-US" } `
    | Remove-Item -Recurse -Force

Write-Host "Cleanup completed" -ForegroundColor Green
```

## 2. 创建安装程序

### 2.1 使用 WiX Toolset

#### 安装 WiX Toolset
```powershell
# 安装 WiX Toolset
dotnet tool install --global wix

# 或下载并安装 WiX Toolset Visual Studio Extension
# https://marketplace.visualstudio.com/items?itemName=WixToolset.WixToolsetVisualStudioExtension
```

#### WiX 项目文件 (WinServiceManager.Setup.wixproj)
```xml
<Project Sdk="WixToolset.Sdk/4.0.0">
  <PropertyGroup>
    <OutputType>Package</OutputType>
    <TargetFramework>net472</TargetFramework>
    <OutputName>WinServiceManagerSetup</OutputName>
    <PackageLicenseFile>license.rtf</PackageLicenseFile>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="WixToolset.UI.wixext" Version="4.0.0" />
    <PackageReference Include="WixToolset.Util.wixext" Version="4.0.0" />
  </ItemGroup>

  <ItemGroup>
    <None Include="license.rtf" Pack="true" PackagePath="\" />
  </ItemGroup>
</Project>
```

#### 主安装程序脚本 (Product.wxs)
```xml
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package Name="WinServiceManager"
           Manufacturer="LiteHomeLab"
           Version="1.0.0.0"
           UpgradeCode="PUT-YOUR-GUID-HERE">

    <!-- 定义安装程序界面 -->
    <UIRef Id="WixUI_InstallDir" />
    <Property Id="WIXUI_INSTALLDIR" Value="INSTALLFOLDER" />

    <!-- 定义安装目录 -->
    <StandardDirectory Id="ProgramFiles64Folder">
      <Directory Id="INSTALLFOLDER" Name="WinServiceManager">
        <!-- 应用程序文件 -->
        <Component Id="MainExecutable" Guid="PUT-UNIQUE-GUID-HERE">
          <File Id="WinServiceManager.exe"
                Source="publish/WinServiceManager.exe"
                KeyPath="yes" />

          <!-- 设置应用程序为以管理员身份运行 -->
          <RegistryKey Root="HKCU" Key="SOFTWARE\LiteHomeLab\WinServiceManager">
            <RegistryValue Name="RunAsAdmin" Type="integer" Value="1" />
          </RegistryKey>

          <!-- 创建开始菜单快捷方式 -->
          <Shortcut Id="ApplicationStartMenuShortcut"
                    Name="WinServiceManager"
                    Description="Windows Service Manager"
                    Target="[#WinServiceManager.exe]"
                    WorkingDirectory="INSTALLFOLDER"
                    Icon="ProductIcon" />
        </Component>

        <!-- WinSW 模板文件 -->
        <Component Id="WinSWM Templates" Guid="PUT-UNIQUE-GUID-HERE">
          <Directory Id="TemplatesFolder" Name="templates">
            <File Id="WinSW.exe"
                  Source="publish/templates/WinSW-x64.exe" />
          </Directory>
        </Component>

        <!-- services 目录 -->
        <Component Id="ServicesFolder" Guid="PUT-UNIQUE-GUID-HERE">
          <CreateFolder>
            <Permission User="Everyone" GenericAll="yes" />
          </CreateFolder>
        </Component>

        <!-- 配置文件 -->
        <Component Id="ConfigFiles" Guid="PUT-UNIQUE-GUID-HERE">
          <Directory Id="ConfigFolder" Name="config">
            <File Id="AppSettings"
                  Source="publish/config/appsettings.json" />
          </Directory>
        </Component>
      </Directory>
    </StandardDirectory>

    <!-- 开始菜单程序组 -->
    <Directory Id="ProgramMenuFolder">
      <Directory Id="ApplicationProgramsFolder" Name="WinServiceManager">
        <Component Id="ApplicationShortcuts" Guid="PUT-UNIQUE-GUID-HERE">
          <Shortcut Id="UninstallProduct"
                    Name="Uninstall WinServiceManager"
                    Description="Uninstall WinServiceManager"
                    Target="[System64Folder]msiexec.exe"
                    Arguments="/x [ProductCode]" />
          <RemoveFolder Id="ApplicationProgramsFolder" On="uninstall" />
          <RegistryValue Root="HKCU"
                         Key="Software\Microsoft\WinServiceManager"
                         Name="installed"
                         Type="integer"
                         Value="1"
                         KeyPath="yes" />
        </Component>
      </Directory>
    </Directory>

    <!-- 安装程序特性 -->
    <Feature Id="ProductFeature" Title="WinServiceManager" Level="1">
      <ComponentRef Id="MainExecutable" />
      <ComponentRef Id="WinSWM Templates" />
      <ComponentRef Id="ServicesFolder" />
      <ComponentRef Id="ConfigFiles" />
      <ComponentRef Id="ApplicationShortcuts" />
    </Feature>

    <!-- 升级规则 -->
    <MajorUpgrade DowngradeErrorMessage="A newer version is already installed." />

    <!-- 启动管理员权限检查 -->
    <CustomAction Id="CheckAdminRights"
                  BinaryKey="WixCA"
                  DllEntry="WixRequirePrivilege"
                  Execute="immediate" />

    <InstallUISequence>
      <Custom Action="CheckAdminRights" Before="LaunchConditions" />
    </InstallUISequence>

  </Package>
</Wix>
```

### 2.2 使用 Inno Setup（替代方案）

#### Inno Setup 脚本 (WinServiceManager.iss)
```inno
[Setup]
AppName=WinServiceManager
AppVersion=1.0.0
AppPublisher=LiteHomeLab
DefaultDirName={pf64}\WinServiceManager
DefaultGroupName=WinServiceManager
OutputDir=installer
OutputBaseFilename=WinServiceManagerSetup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
SetupIconFile=icon.ico

[Files]
Source: "publish\WinServiceManager.exe"; DestDir: "{app}"
Source: "publish\*.dll"; DestDir: "{app}"
Source: "publish\templates\*"; DestDir: "{app}\templates"
Source: "publish\config\*"; DestDir: "{app}\config"

[Dirs]
Name: "{app}\services"; Permissions: users-modify

[Icons]
Name: "{group}\WinServiceManager"; Filename: "{app}\WinServiceManager.exe"
Name: "{group}\Uninstall"; Filename: "{uninstallexe}"
Name: "{commondesktop}\WinServiceManager"; Filename: "{app}\WinServiceManager.exe"

[Registry]
Root: HKCU; Subkey: "Software\LiteHomeLab\WinServiceManager"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"

[Run]
Filename: "{app}\WinServiceManager.exe"; Description: "Launch WinServiceManager"; Flags: nowait postinstall skipifsilent
```

## 3. 版本管理

### 3.1 版本号管理策略

#### AssemblyVersion.cs
```csharp
// Properties/AssemblyInfo.cs
using System.Reflection;

[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]
```

#### 自动版本更新脚本 (build/update-version.ps1)
```powershell
param(
    [string]$Version = "1.0.0",
    [string]$PreRelease = ""
)

# 更新项目文件
$projectFile = "src/WinServiceManager/WinServiceManager.csproj"
[xml]$projectXml = Get-Content $projectFile

$versionElement = $projectXml.Project.PropertyGroup.Version
if ($versionElement) {
    $versionElement.InnerText = if ($PreRelease) { "$Version-$PreRelease" } else { $Version }
}
else {
    $propertyGroup = $projectXml.CreateElement("PropertyGroup")
    $versionElem = $projectXml.CreateElement("Version")
    $versionElem.InnerText = if ($PreRelease) { "$Version-$PreRelease" } else { $Version }
    $propertyGroup.AppendChild($versionElem) | Out-Null
    $projectXml.AppendChild($propertyGroup) | Out-Null
}

$projectXml.Save($projectFile)

Write-Host "Version updated to $Version$($PreRelease ? '-$PreRelease' : '')" -ForegroundColor Green
```

### 3.2 发布标签管理
```powershell
# build/create-release-tag.ps1
param(
    [string]$Version = "1.0.0",
    [string]$PreRelease = "",
    [string]$Changelog = ""
)

$tagName = if ($PreRelease) { "v$Version-$PreRelease" } else { "v$Version" }

# 创建发布标签
git tag -a $tagName -m "Release $tagName"

# 推送标签
git push origin $tagName

Write-Host "Created release tag: $tagName" -ForegroundColor Green
```

## 4. 发布流程

### 4.1 GitHub Actions 自动发布
```yaml
# .github/workflows/release.yml
name: Release

on:
  push:
    tags:
      - 'v*'

jobs:
  release:
    runs-on: windows-latest

    steps:
    - uses: actions/checkout@v3
      with:
        fetch-depth: 0

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'

    - name: Extract version
      id: version
      run: |
        $VERSION = "${{ github.ref }}" -replace 'refs/tags/v', ''
        echo "version=$VERSION" >> $env:GITHUB_OUTPUT

    - name: Restore dependencies
      run: dotnet restore

    - name: Build
      run: dotnet build --configuration Release -p:Version=${{ steps.version.outputs.version }}

    - name: Publish
      run: |
        .\build\publish.ps1 -Configuration Release -OutputDir "publish"

    - name: Create installer
      run: |
        # 使用 WiX 创建安装程序
        dotnet build src/WinServiceManager.Setup/WinServiceManager.Setup.wixproj -p:Version=${{ steps.version.outputs.version }}

    - name: Create release
      uses: softprops/action-gh-release@v1
      with:
        files: |
          publish/WinServiceManager.zip
          src/WinServiceManager.Setup/bin/Release/WinServiceManagerSetup.msi
        draft: false
        prerelease: ${{ contains(steps.version.outputs.version, '-') }}
        generate_release_notes: true
      env:
        GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

### 4.2 手动发布流程

#### 准备发布
```powershell
# 1. 更新版本号
.\build\update-version.ps1 -Version "1.0.0" -PreRelease "beta"

# 2. 构建和打包
dotnet build --configuration Release
.\build\publish.ps1 -Configuration Release

# 3. 运行测试
dotnet test --configuration Release

# 4. 创建安装程序
dotnet build src/WinServiceManager.Setup/WinServiceManager.Setup.wixproj
```

#### 发布检查清单
- [ ] 版本号已更新
- [ ] 所有测试通过
- [ ] 编译无警告
- [ ] 文档已更新
- [ ] 更新日志已准备
- [ ] 安装程序测试通过
- [ ] 卸载测试通过
- [ ] 签名证书有效（如果需要）

## 5. 部署配置

### 5.1 应用程序配置

#### appsettings.json
```json
{
  "AppSettings": {
    "LogDirectory": "logs",
    "MaxLogFiles": 10,
    "MaxLogFileSize": 10485760,
    "RefreshInterval": 30,
    "BackupDirectory": "backups"
  },
  "WinSW": {
    "DownloadUrl": "https://github.com/winsw/winsw/releases/download/v3.0.0/WinSW-x64.exe",
    "DefaultArguments": {
      "SizeThreshold": 10240,
      "KeepFiles": 8,
      "StopParentProcessFirst": true
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

#### appsettings.Development.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "WinServiceManager": "Trace"
    }
  },
  "AppSettings": {
    "RefreshInterval": 5
  }
}
```

### 5.2 环境变量配置
```powershell
# 设置环境变量
$env:WINSERVICEMANAGER__LOGDIRECTORY = "C:\Logs\WinServiceManager"
$env:WINSERVICEMANAGER__REFRESHINTERVAL = "60"

# 使用配置文件
$env:DOTNET_ENVIRONMENT = "Production"
```

## 6. 部署后验证

### 6.1 安装验证脚本
```powershell
# scripts/verify-installation.ps1
param(
    [string]$InstallPath = "C:\Program Files\WinServiceManager"
)

Write-Host "Verifying WinServiceManager installation..." -ForegroundColor Green

# 检查文件存在
$requiredFiles = @(
    "$InstallPath\WinServiceManager.exe",
    "$InstallPath\templates\WinSW-x64.exe",
    "$InstallPath\config\appsettings.json"
)

$allFilesExist = $true
foreach ($file in $requiredFiles) {
    if (Test-Path $file) {
        Write-Host "✓ Found: $file" -ForegroundColor Green
    } else {
        Write-Host "✗ Missing: $file" -ForegroundColor Red
        $allFilesExist = $false
    }
}

# 检查目录权限
$servicesDir = "$InstallPath\services"
if (Test-Path $servicesDir) {
    try {
        $testFile = "$servicesDir\test.txt"
        "test" | Out-File $testFile
        Remove-Item $testFile
        Write-Host "✓ Services directory writable" -ForegroundColor Green
    } catch {
        Write-Host "✗ Services directory not writable" -ForegroundColor Red
        $allFilesExist = $false
    }
} else {
    Write-Host "✗ Services directory not found" -ForegroundColor Red
    $allFilesExist = $false
}

# 检查注册表
$regPath = "HKCU:\Software\LiteHomeLab\WinServiceManager"
if (Test-Path $regPath) {
    Write-Host "✓ Registry entries found" -ForegroundColor Green
} else {
    Write-Host "✗ Registry entries missing" -ForegroundColor Red
}

# 测试程序启动
try {
    $process = Start-Process "$InstallPath\WinServiceManager.exe" -PassThru
    Start-Sleep -Seconds 3

    if (!$process.HasExited) {
        $process.Kill()
        Write-Host "✓ Application starts successfully" -ForegroundColor Green
    } else {
        Write-Host "✗ Application failed to start" -ForegroundColor Red
        $allFilesExist = $false
    }
} catch {
    Write-Host "✗ Error starting application: $_" -ForegroundColor Red
    $allFilesExist = $false
}

if ($allFilesExist) {
    Write-Host "`nInstallation verification PASSED" -ForegroundColor Green
} else {
    Write-Host "`nInstallation verification FAILED" -ForegroundColor Red
    exit 1
}
```

### 6.2 卸载验证
```powershell
# scripts/verify-uninstall.ps1
$installPath = "C:\Program Files\WinServiceManager"
$regPath = "HKCU:\Software\LiteHomeLab\WinServiceManager"
$startMenuPath = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\WinServiceManager"

Write-Host "Verifying WinServiceManager uninstallation..." -ForegroundColor Green

# 检查程序目录
if (Test-Path $installPath) {
    Write-Host "✗ Installation directory still exists" -ForegroundColor Red
} else {
    Write-Host "✓ Installation directory removed" -ForegroundColor Green
}

# 检查注册表
if (Test-Path $regPath) {
    Write-Host "✗ Registry entries still exist" -ForegroundColor Red
} else {
    Write-Host "✓ Registry entries removed" -ForegroundColor Green
}

# 检查开始菜单
if (Test-Path $startMenuPath) {
    Write-Host "✗ Start menu shortcuts still exist" -ForegroundColor Red
} else {
    Write-Host "✓ Start menu shortcuts removed" -ForegroundColor Green
}

# 检查服务是否已卸载
$services = Get-WmiObject -Class Win32_Service | Where-Object { $_.Name -like "WinServiceManager-*" }
if ($services) {
    Write-Host "✗ Some services still exist:" -ForegroundColor Red
    $services | ForEach-Object { Write-Host "  - $($_.Name)" }
} else {
    Write-Host "✓ All services uninstalled" -ForegroundColor Green
}
```

## 7. 持续部署

### 7.1 Azure DevOps Pipeline
```yaml
# azure-pipelines.yml
trigger:
- main

pool:
  vmImage: 'windows-latest'

variables:
  solution: '**/*.sln'
  buildPlatform: 'Any CPU'
  buildConfiguration: 'Release'

stages:
- stage: Build
  displayName: 'Build Stage'
  jobs:
  - job: Build
    displayName: 'Build Job'
    steps:
    - task: NuGetToolInstaller@1
      displayName: 'Install NuGet'

    - task: NuGetCommand@2
      displayName: 'Restore NuGet packages'
      inputs:
        command: 'restore'
        restoreSolution: '$(solution)'

    - task: VSBuild@1
      displayName: 'Build solution'
      inputs:
        solution: '$(solution)'
        platform: '$(buildPlatform)'
        configuration: '$(buildConfiguration)'

    - task: VSTest@2
      displayName: 'Run tests'
      inputs:
        platform: '$(buildPlatform)'
        configuration: '$(buildConfiguration)'

- stage: Deploy
  displayName: 'Deploy Stage'
  dependsOn: Build
  condition: succeeded()
  jobs:
  - deployment: Deploy
    displayName: 'Deploy Job'
    environment: 'production'
    strategy:
      runOnce:
        deploy:
          steps:
          - download: current
            artifact: drop

          - task: PowerShell@2
            displayName: 'Deploy to server'
            inputs:
              filePath: 'scripts/deploy.ps1'
              arguments: '-SourcePath $(Pipeline.Workspace)/drop -TargetPath C:\Deployments\WinServiceManager'
```

### 7.2 部署脚本
```powershell
# scripts/deploy.ps1
param(
    [Parameter(Mandatory=$true)]
    [string]$SourcePath,

    [Parameter(Mandatory=$true)]
    [string]$TargetPath,

    [string]$BackupPath = "C:\Backups\WinServiceManager",

    [switch]$DryRun
)

Write-Host "Starting deployment..." -ForegroundColor Green

# 创建备份
if (Test-Path $TargetPath) {
    $backupDir = Join-Path $BackupPath "backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    Write-Host "Creating backup to $backupDir" -ForegroundColor Yellow

    if (-not $DryRun) {
        New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
        Copy-Item -Path "$TargetPath\*" -Destination $backupDir -Recurse
    }
}

# 停止服务（如果存在）
$serviceName = "WinServiceManager"
$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service) {
    Write-Host "Stopping service $serviceName..." -ForegroundColor Yellow
    if (-not $DryRun) {
        Stop-Service -Name $serviceName -Force
    }
}

# 复制文件
Write-Host "Copying files from $SourcePath to $TargetPath" -ForegroundColor Yellow
if (-not $DryRun) {
    if (-not (Test-Path $TargetPath)) {
        New-Item -ItemType Directory -Path $TargetPath -Force | Out-Null
    }
    Copy-Item -Path "$SourcePath\*" -Destination $TargetPath -Recurse -Force
}

# 启动服务
if ($service) {
    Write-Host "Starting service $serviceName..." -ForegroundColor Yellow
    if (-not $DryRun) {
        Start-Service -Name $serviceName
    }
}

Write-Host "Deployment completed successfully!" -ForegroundColor Green
```

## 8. 故障排除

### 8.1 常见安装问题

#### 问题1：权限不足
```powershell
# 症状：安装程序提示权限不足
# 解决：以管理员身份运行安装程序
```

#### 问题2：防病毒软件阻止
```powershell
# 症状：安装过程中文件被删除
# 解决：添加排除规则或使用代码签名证书
```

#### 问题3：.NET 运行时未安装
```powershell
# 症状：程序无法启动，提示缺少运行时
# 解决：检查是否需要安装 .NET 8 Runtime
```

### 8.2 调试安装程序
```xml
<!-- 在 WiX 项目中启用日志记录 -->
<Property Id="MsiLogging" Value="voicewarmup" />
<CustomActionRef Id="Wix4FailWhenDeferred" />
```

### 8.3 日志收集
```powershell
# scripts/collect-logs.ps1
$logsDir = "C:\Program Files\WinServiceManager\logs"
$outputFile = "WinServiceManager_logs.zip"

if (Test-Path $logsDir) {
    Compress-Archive -Path "$logsDir\*" -DestinationPath $outputFile
    Write-Host "Logs collected to $outputFile"
} else {
    Write-Host "No logs directory found"
}
```

这份部署指南涵盖了从应用程序打包到最终部署的所有步骤，确保 WinServiceManager 能够可靠地安装和运行在目标系统上。