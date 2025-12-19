# WinServiceManager 开发计划

## 概述

本目录包含了 WinServiceManager（Windows 服务管理器）的详细开发计划。WinServiceManager 是一个基于 WinSW 封装的 WPF 桌面应用程序，旨在简化将任意可执行文件或脚本注册为 Windows 系统服务的过程。

## 文档结构

| 文档 | 描述 |
|------|------|
| [00-project-overview.md](00-project-overview.md) | 项目整体介绍和概述 |
| [01-project-structure.md](01-project-structure.md) | 详细的项目目录结构设计 |
| [02-core-models.md](02-core-models.md) | 数据模型和实体类设计 |
| [03-service-management.md](03-service-management.md) | WinSW 封装和服务管理逻辑 |
| [04-mvvm-architecture.md](04-mvvm-architecture.md) | MVVM 模式的实现细节 |
| [05-ui-design.md](05-ui-design.md) | 用户界面设计和交互流程 |
| [06-logging-system.md](06-logging-system.md) | 日志查看和监控功能 |
| [07-testing-strategy.md](07-testing-strategy.md) | 测试计划和测试用例 |
| [08-deployment-guide.md](08-deployment-guide.md) | 应用程序打包和部署 |
| [09-security-implementation.md](09-security-implementation.md) | 安全实施报告和漏洞修复 |

## 技术栈

- **开发语言**: C# / .NET 8
- **UI框架**: WPF (MVVM模式，CommunityToolkit.Mvvm)
- **核心依赖**: WinSW v3.0+ (Windows Service Wrapper)
- **数据存储**: JSON 文件
- **权限要求**: 管理员权限
- **安全特性**: 企业级安全防护（路径验证、命令清理、XML安全生成）
- **日志框架**: Microsoft.Extensions.Logging
- **依赖注入**: Microsoft.Extensions.DependencyInjection

## 核心功能

1. **服务创建与注册** - 将任意 exe 或脚本注册为 Windows 服务
2. **服务控制** - 启动、停止、重启、卸载服务
3. **日志查看** - 实时查看服务运行日志
4. **服务管理** - 管理所有通过本工具创建的服务
5. **安全防护** - 企业级安全措施，防止各种攻击
6. **资源管理** - 内存泄漏防护和并发控制

## 开发阶段

### 阶段 1：项目初始化（1-2 天）
- 创建解决方案和项目文件
- 配置 NuGet 包依赖
- 设置项目基础结构

### 阶段 2：核心模型和数据层（1-2 天）
- 定义 ServiceItem 和相关模型
- 实现数据持久化服务
- 创建工具类

### 阶段 3：WinSW 封装和服务管理（3-4 天）
- 实现 WinSWWrapper 类
- 实现 ServiceManagerService 类
- 处理管理员权限

### 阶段 4：MVVM 基础架构（2-3 天）
- 创建各个 ViewModel
- 实现数据绑定
- 设置依赖注入

### 阶段 5：UI 界面开发（4-5 天）
- 设计主窗口界面
- 实现创建服务对话框
- 实现日志查看窗口

### 阶段 6：日志系统集成（2-3 天）
- 实现 LogReaderService
- 集成日志功能到 UI
- 优化日志读取性能

### 阶段 7：测试和优化（2-3 天）
- 编写单元测试
- 进行集成测试
- 性能优化

## 快速开始

### 环境要求
- Windows 10/11 或 Windows Server 2019/2022
- .NET 8 SDK
- Visual Studio 2022 或 Visual Studio Code
- 管理员权限（用于调试和测试）

### 获取代码
```bash
git clone https://github.com/LiteHomeLab/windows_services_manager.git
cd windows_services_manager
```

### 构建项目
```bash
# 恢复 NuGet 包
dotnet restore

# 构建解决方案
dotnet build

# 运行应用程序
dotnet run --project src/WinServiceManager
```

## 贡献指南

1. Fork 本项目
2. 创建功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 创建 Pull Request

## 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](../LICENSE) 文件了解详情。

## 联系方式

如有问题或建议，请通过以下方式联系：

- 创建 Issue
- 发送邮件至：[您的邮箱]
- 访问项目主页：https://github.com/LiteHomeLab/windows_services_manager

## 致谢

- [WinSW](https://github.com/winsw/winsw) - Windows Service Wrapper
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM 框架