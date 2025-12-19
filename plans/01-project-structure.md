# 项目结构设计

## 目录结构

```
WinServiceManager/
├── src/                                    # 源代码根目录
│   ├── WinServiceManager.sln              # 解决方案文件
│   │
│   ├── WinServiceManager/                  # 主应用程序项目
│   │   ├── WinServiceManager.csproj        # 项目配置文件
│   │   ├── app.manifest                    # 管理员权限配置
│   │   ├── App.xaml                       # WPF 应用程序入口
│   │   ├── App.xaml.cs                    # 应用程序启动逻辑
│   │   │
│   │   ├── Models/                        # 数据模型
│   │   │   ├── ServiceItem.cs             # 服务实体模型
│   │   │   └── ServiceStatus.cs           # 服务状态枚举
│   │   │
│   │   ├── Views/                         # 视图层 (XAML)
│   │   │   ├── MainWindow.xaml            # 主窗口
│   │   │   ├── MainWindow.xaml.cs
│   │   │   ├── ServiceCreateDialog.xaml   # 创建服务对话框
│   │   │   ├── ServiceCreateDialog.xaml.cs
│   │   │   ├── LogViewerWindow.xaml       # 日志查看窗口
│   │   │   └── LogViewerWindow.xaml.cs
│   │   │
│   │   ├── ViewModels/                    # 视图模型
│   │   │   ├── MainWindowViewModel.cs     # 主窗口视图模型
│   │   │   ├── ServiceItemViewModel.cs    # 服务项视图模型
│   │   │   ├── ServiceCreateViewModel.cs  # 创建服务视图模型
│   │   │   └── LogViewerViewModel.cs      # 日志查看视图模型
│   │   │
│   │   ├── Services/                      # 业务服务
│   │   │   ├── ServiceManagerService.cs   # 服务管理核心逻辑
│   │   │   ├── WinSWWrapper.cs            # WinSW 命令封装
│   │   │   ├── LogReaderService.cs        # 日志读取服务
│   │   │   └── DataService.cs             # 数据持久化服务
│   │   │
│   │   ├── Utilities/                     # 工具类
│   │   │   ├── FileUtils.cs               # 文件操作工具
│   │   │   └── AdminHelper.cs             # 管理员权限检查
│   │   │
│   │   ├── Resources/                     # 资源文件
│   │   │   ├── Icons/                     # 图标资源
│   │   │   └── Styles/                    # 样式文件
│   │   │
│   │   └── Properties/                    # 项目属性
│   │       ├── AssemblyInfo.cs
│   │       └── Settings.settings
│   │
│   └── WinServiceManager.Tests/           # 单元测试项目
│       ├── WinServiceManager.Tests.csproj
│       ├── Services/
│       │   ├── ServiceManagerTests.cs
│       │   └── WinSWWrapperTests.cs
│       └── Utilities/
│           └── FileUtilsTests.cs
│
├── templates/                             # 运行时模板目录
│   └── WinSW-x64.exe                     # WinSW 母本文件（需预先下载）
│
├── services/                              # 运行时服务存储目录
│   └── {Service_Unique_ID}/               # 单个服务的沙盒目录
│       ├── {ServiceID}.exe               # (复制并重命名的 WinSW.exe)
│       ├── {ServiceID}.xml               # WinSW 配置文件
│       └── logs/                         # 日志目录
│           ├── {ServiceID}.out.log       # 标准输出日志
│           └── {ServiceID}.err.log       # 错误输出日志
│
├── plans/                                 # 开发计划文档
│   ├── 00-project-overview.md
│   ├── 01-project-structure.md
│   ├── 02-core-models.md
│   ├── ...
│
├── docs/                                  # 项目文档
│   ├── 系统设计规格说明书.md
│   └── README.md
│
├── build/                                 # 构建脚本
│   ├── build.ps1                         # PowerShell 构建脚本
│   └── publish.ps1                       # 发布脚本
│
├── .gitignore                            # Git 忽略文件
├── README.md                              # 项目说明
└── LICENSE                                # 许可证
```

## 关键文件说明

### 1. WinServiceManager.csproj
项目配置文件，包含：
- .NET 8 目标框架
- NuGet 包依赖
- 资源文件配置（WinSW.exe 作为嵌入式资源）
- 发布配置

### 2. app.manifest
管理员权限清单文件：
```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="WinServiceManager"/>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
    <security>
      <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
        <requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
      </requestedPrivileges>
    </security>
  </trustInfo>
</assembly>
```

### 3. Services 目录隔离策略
每个服务都有独立的目录，确保：
- 配置文件隔离
- 日志文件隔离
- 服务进程隔离
- 便于管理和服务卸载

### 4. 数据存储位置
- **服务元数据**: `AppData/WinServiceManager/services.json`
- **应用配置**: `AppData/WinServiceManager/appsettings.json`
- **临时文件**: 系统临时目录

## NuGet 包依赖

### 必需包
```xml
<ItemGroup>
  <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
  <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
  <PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
  <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.0.0" />
  <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
</ItemGroup>
```

### 可选包（UI 增强）
```xml
<ItemGroup>
  <PackageReference Include="MaterialDesignThemes" Version="4.9.0" />
  <PackageReference Include="Microsoft.Xaml.Behaviors.Wpf" Version="1.1.77" />
</ItemGroup>
```

### 测试包
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
  <PackageReference Include="xunit" Version="2.6.2" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
  <PackageReference Include="Moq" Version="4.20.69" />
</ItemGroup>
```

## 项目配置要点

### 1. 管理员权限
- 必须配置 app.manifest 要求管理员权限
- 启动时进行权限检查

### 2. WinSW 集成
- WinSW.exe 作为嵌入式资源或外部文件
- 首次运行时复制到 templates 目录

### 3. 数据持久化
- 使用 JSON 文件存储服务元数据
- 考虑使用 LiteDB 作为更强大的替代方案

### 4. 安全考虑
- 验证文件路径，防止路径遍历攻击
- 加密存储敏感配置信息
- 限制服务创建权限

## 构建和发布

### 开发构建
```powershell
dotnet build src/WinServiceManager.sln
```

### 发布构建
```powershell
dotnet publish src/WinServiceManager/WinServiceManager.csproj -c Release -r win-x64 --self-contained true
```

### 发布包内容
```
WinServiceManager/
├── WinServiceManager.exe
├── templates/
│   └── WinSW-x64.exe
├── services/ (空目录)
└── config/
    └── appsettings.json
```

## 版本控制策略

### Git 忽略规则
```
# Build outputs
bin/
obj/
dist/
out/

# User specific files
*.user
*.suo
*.userosscache
*.sln.docstates

# Runtime directories
services/
*.log

# IDE files
.vs/
.vscode/

# OS files
Thumbs.db
Desktop.ini
```

### 分支策略
- `main`: 主分支，稳定版本
- `develop`: 开发分支
- `feature/*`: 功能分支
- `release/*`: 发布分支
- `hotfix/*`: 热修复分支