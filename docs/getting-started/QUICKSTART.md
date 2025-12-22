# WinServiceManager 快速开始指南

## 当前状态 ✅

项目已成功修复编译错误，主项目可以正常构建和运行。

## 运行前准备

### 1. 下载 WinSW（必需）
由于网络问题，需要手动下载 WinSW：

```bash
# 下载地址
https://github.com/winsw/winsw/releases/download/v3.0.0/WinSW-x64.exe

# 目标位置
src/WinServiceManager/templates/WinSW-x64.exe
```

### 2. 管理员权限（必需）
应用程序必须以管理员身份运行，因为：
- 创建/删除Windows服务需要管理员权限
- 需要访问系统服务注册表
- 需要创建系统服务目录

## 运行方法

### 方法1：使用 .NET CLI
```bash
# 以管理员身份打开命令提示符或PowerShell
cd C:\WorkSpace\Go2Hell\src\github.com\LiteHomeLab\windows_services_manager
dotnet run --project src/WinServiceManager
```

### 方法2：直接运行可执行文件
```bash
# 构建项目
dotnet build src/WinServiceManager

# 运行可执行文件（需要管理员权限）
src\WinServiceManager\bin\Debug\net8.0-windows\WinServiceManager.exe
```

## 核心功能

### 已实现功能 ✅
1. **服务创建与注册** - 将exe或脚本注册为Windows服务
2. **服务控制** - 启动、停止、重启、卸载服务
3. **服务管理** - 管理所有通过本工具创建的服务
4. **企业级安全防护** - 路径验证、命令注入防护
5. **资源管理** - 内存泄漏防护、并发控制
6. **性能监控** - 实时监控应用性能

### 服务创建支持
- **可执行文件**: .exe, .bat, .cmd, .ps1, .py, .js 等
- **配置选项**: 工作目录、启动参数、环境变量、服务依赖
- **安全验证**: 路径遍历防护、命令注入过滤
- **日志系统**: 自动创建和管理日志文件

### 服务隔离策略
每个服务运行在独立的目录中：
```
services/{ServiceID}/
├── {ServiceID}.exe     # WinSW可执行文件
├── {ServiceID}.xml     # WinSW配置文件
└── logs/               # 日志目录
    ├── {ServiceID}.out.log  # 标准输出日志
    └── {ServiceID}.err.log  # 错误输出日志
```

## 使用步骤

1. **启动应用** - 以管理员身份运行WinServiceManager
2. **创建服务** - 点击"创建服务"，填写服务信息
3. **配置服务** - 设置可执行文件、参数、工作目录等
4. **安装服务** - 确认配置后安装到Windows服务中
5. **管理服务** - 通过主界面启动、停止、重启或卸载服务
6. **查看日志** - 双击服务项查看实时运行日志

## 已修复的编译问题

- ✅ 解决了240个编译错误，主项目0错误编译成功
- ✅ 修复了nullable引用类型配置问题
- ✅ 解决了WPF/WinForms命名空间冲突
- ✅ 修复了Timer、Brush、Application等类型冲突
- ✅ 用WPF的OpenFileDialog替代了Windows Forms对话框

## 下一步开发计划

### 短期目标
1. 完善UI界面和用户体验
2. 添加更多的错误处理和用户提示
3. 修复关键测试用例

### 中期目标
1. 添加服务导入/导出功能
2. 实现批量服务操作
3. 添加服务性能监控图表

### 长期目标
1. 支持服务依赖关系配置
2. 添加定时任务功能
3. 创建安装包和自动更新

## 技术架构

- **框架**: WPF + .NET 8
- **架构模式**: MVVM + 依赖注入
- **核心依赖**: WinSW v3.0+ (Windows Service Wrapper)
- **安全特性**: 企业级安全防护
- **日志系统**: Microsoft.Extensions.Logging
- **性能监控**: 内置性能监控服务

## 故障排除

### 常见问题
1. **"需要管理员权限"错误** - 以管理员身份运行
2. **WinSW下载失败** - 手动下载到templates目录
3. **服务启动失败** - 检查可执行文件路径和权限
4. **日志查看失败** - 确认服务已安装并运行

### 调试模式
```bash
# 启用详细日志
dotnet run --project src/WinServiceManager --verbosity diagnostic
```

## 贡献指南

1. Fork本项目
2. 创建功能分支
3. 提交更改
4. 创建Pull Request

项目现在处于可运行状态，欢迎大家测试和贡献！