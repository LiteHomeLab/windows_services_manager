# WinServiceManager 项目概述

## 项目名称
WinServiceManager - 基于 WinSW 封装的 Windows 服务管理工具

## 项目目标
开发一个基于 Windows Presentation Foundation (WPF) 的桌面应用程序，旨在简化将任意可执行文件（exe）或脚本（Python, Bat, Node.js 等）注册为 Windows 本地系统服务的过程。

## 核心架构
采用 **WinSW (Windows Service Wrapper)** 作为底层服务宿主容器，上层 UI 负责配置文件的生成、服务注册命令的调度以及运行状态的监控。

## 技术栈
- **开发语言**: C# / .NET 8
- **UI框架**: WPF (使用 MVVM 模式，CommunityToolkit.Mvvm)
- **核心依赖库**:
  - System.ServiceProcess.ServiceController (用于获取服务状态和控制启停)
  - Microsoft.Extensions.Logging (日志记录和监控)
  - Microsoft.Extensions.DependencyInjection (依赖注入容器)
  - WinSW v3.0+ (外部二进制文件)
- **权限要求**: 必须以 Administrator (管理员) 权限运行
- **安全特性**: 企业级安全防护，防止路径遍历、命令注入、XML注入等攻击

## 主要功能
1. **服务创建与注册** - 将任意 exe 或脚本注册为 Windows 服务
2. **服务控制** - 启动、停止、重启、卸载服务
3. **日志查看** - 实时查看服务运行日志（标准输出和错误输出）
4. **服务管理** - 管理所有通过本工具创建的服务

## 架构特点
- **每服务一目录**的隔离策略，确保配置互不干扰
- 使用 WinSW 作为底层服务宿主，提供稳定可靠的服务运行环境
- 维护元数据数据库，便于 UI 列表展示和管理
- 支持多种脚本类型和可执行文件
- **企业级安全防护**：路径验证、命令清理、XML安全生成
- **完善的日志记录**：操作审计、错误追踪、性能监控
- **资源安全管理**：内存泄漏防护、并发控制、异常处理
- **依赖注入架构**：松耦合设计，易于测试和维护

## 项目结构概览
```
WinServiceManager/
├── src/                       # 源代码目录
│   ├── WinServiceManager.sln  # 解决方案文件
│   ├── WinServiceManager/     # 主项目
│   └── WinServiceManager.Tests/ # 测试项目
├── templates/                 # 模板文件目录
│   └── WinSW-x64.exe         # WinSW 母本文件
├── services/                  # 服务存储目录
├── plans/                     # 开发计划文档
└── docs/                      # 项目文档
```

## 开发计划文档
本项目的开发计划已拆分为以下文档：

1. [项目概述](00-project-overview.md) - 本文档，项目整体介绍
2. [项目结构设计](01-project-structure.md) - 详细的项目目录结构
3. [核心模型设计](02-core-models.md) - 数据模型和实体类设计
4. [服务管理模块](03-service-management.md) - WinSW 封装和服务管理逻辑
5. [MVVM 架构设计](04-mvvm-architecture.md) - MVVM 模式的实现细节
6. [UI 界面设计](05-ui-design.md) - 用户界面设计和交互流程
7. [日志系统设计](06-logging-system.md) - 日志查看和监控功能
8. [测试策略](07-testing-strategy.md) - 测试计划和测试用例
9. [部署指南](08-deployment-guide.md) - 应用程序打包和部署

## 验收标准
1. **功能验证**：
   - 环境隔离验证：程序重启后，列表能重新加载已注册的服务
   - 生命周期验证：可以注册 Python 脚本服务，在 services.msc 中看到运行状态
   - 持久化验证：系统重启后，服务自动启动
   - 停止逻辑验证：点击停止后，进程被正确终止，无残留
   - 异常处理：脚本路径错误时，显示错误信息

2. **安全验证**：
   - 路径遍历防护：输入 `../../windows/system32` 等路径被正确拒绝
   - 命令注入防护：输入 `&calc.exe` 等命令被正确过滤
   - XML注入防护：特殊字符在配置文件中被正确转义
   - 权限验证：管理员权限检查正常工作
   - 资源管理：长期运行无内存泄漏

3. **质量验证**：
   - 编译通过：所有代码无编译错误
   - 依赖注入：服务正确注入和释放
   - 日志记录：关键操作都有日志记录
   - 并发安全：多线程操作无数据竞争

## 当前实施状态 ✅

### ✅ 已完成模块
- **核心架构**：MVVM 框架搭建完成
- **数据模型**：ServiceItem、ServiceStatus 等核心模型完成
- **服务管理**：WinSW 封装和基础服务操作完成
- **安全防护**：企业级安全措施实施完成
- **依赖注入**：服务容器和生命周期管理完成
- **异常处理**：全面的错误处理和日志记录完成

### ✅ 安全特性实施
1. **路径验证器** (`PathValidator.cs`)：防止路径遍历攻击
2. **命令验证器** (`CommandValidator.cs`)：防止命令注入攻击
3. **XML安全生成**：使用 `XElement` 和 `SecurityElement.Escape()`
4. **资源管理**：实现 `IDisposable` 和 `using` 语句
5. **并发控制**：使用 `SemaphoreSlim` 保护文件操作
6. **权限验证**：管理员权限检查和安全执行

### 🚧 待完善功能
- 服务创建对话框 UI（后端逻辑已完成）
- 日志查看器完整实现
- 单元测试和集成测试
- 安装包和部署脚本

## 后续扩展计划
1. 支持服务的导入/导出配置
2. 添加服务的定时任务功能
3. 支持服务依赖关系配置
4. 提供服务性能监控图表
5. 支持批量服务操作