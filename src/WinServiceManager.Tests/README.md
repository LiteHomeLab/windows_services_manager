# WinServiceManager 单元测试文档

## 测试概述

本项目包含对 WinServiceManager 核心组件的全面单元测试，旨在验证功能的正确性和安全性。

## 测试结构

```
WinServiceManager.Tests/
├── UnitTests/
│   ├── CommandValidatorTests.cs          # 命令验证器测试
│   ├── CommandValidatorAdditionalTests.cs # 命令验证器补充测试
│   ├── PathValidatorTests.cs             # 路径验证器测试
│   ├── ServiceItemSecurityTests.cs       # 服务项安全测试
│   ├── SecurityIntegrationTests.cs       # 安全集成测试
│   ├── Helpers/
│   │   └── FilePathAttribute.cs          # 测试辅助属性
│   ├── SecurityTestsCollection.cs        # 安全测试集合
│   ├── ViewModels/
│   │   ├── ServiceItemViewModelTests.cs  # 服务项视图模型测试
│   │   ├── CreateServiceViewModelTests.cs # 创建服务视图模型测试
│   │   ├── MainWindowViewModelTests.cs   # 主窗口视图模型测试
│   │   └── LogViewerViewModelTests.cs    # 日志查看器视图模型测试
│   └── Services/
│       └── ServiceManagerServiceTests.cs # 服务管理器测试
└── IntegrationTests/                     # 集成测试（待开发）
```

## 测试框架

- **xUnit**: 主要测试框架
- **Moq**: 用于模拟对象
- **FluentAssertions**: 提供流畅的断言语法
- **coverlet.collector**: 代码覆盖率收集

## 测试覆盖范围

### 1. ViewModels 测试

#### ServiceItemViewModel
- 服务状态管理
- 启动/停止/重启命令
- 卸载操作
- 状态颜色显示
- 异常处理

#### CreateServiceViewModel
- 服务创建流程
- 输入验证
- 配置预览
- 解释器识别
- 路径自动设置

#### MainWindowViewModel
- 服务列表管理
- 服务排序
- 搜索功能
- 日志查看器集成
- 文件操作

#### LogViewerViewModel
- 实时日志监控
- 日志过滤
- 自动滚动
- 日志保存
- 最大行数限制

### 2. Services 测试

#### ServiceManagerService
- 服务 CRUD 操作
- 服务生命周期管理
- 状态同步
- 错误处理
- 并发操作

### 3. 验证器测试

#### PathValidator
- 路径遍历攻击防护
- UNC 路径验证
- 系统文件访问控制
- 文件名验证
- 路径规范化

#### CommandValidator
- 命令注入防护
- 参数清理
- 可执行文件验证
- 危险字符检测
- 特殊模式识别

## 安全测试重点

### 路径安全
- 防止 `../` 目录遍历攻击
- 阻止访问系统关键目录
- UNC 路径访问控制
- 路径长度限制

### 命令安全
- 防止命令注入（`&&`, `||`, `;`）
- 阻止管道和重定向操作
- 环境变量展开限制
- 危险命令识别

### 输入验证
- 特殊字符过滤
- 输入长度限制
- 控制字符检测
- 脚本注入防护

## 运行测试

### 使用 Visual Studio
1. 打开解决方案
2. 在测试资源管理器中运行所有测试
3. 查看测试结果和代码覆盖率

### 使用命令行
```bash
# 运行所有测试
dotnet test

# 运行特定测试
dotnet test --filter "ClassName=ServiceItemViewModelTests"

# 运行安全相关测试
dotnet test --filter "Category=Security"

# 生成代码覆盖率报告
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
```

## 测试数据

测试使用临时目录和文件，确保：
- 测试隔离
- 自动清理
- 不影响系统文件
- 可重复执行

## 最佳实践

1. **AAA 模式**: 使用 Arrange-Act-Assert 模式组织测试
2. **描述性命名**: 测试名称应清楚描述测试内容
3. **独立性**: 每个测试应独立运行
4. **边界测试**: 包含正常、边界和异常情况
5. **模拟依赖**: 使用 Moq 隔离被测试组件

## 代码覆盖率

当前测试覆盖了以下关键区域：
- ✅ ViewModels (90%+)
- ✅ Services (85%+)
- ✅ Validators (95%+)
- 📝 Models (需要补充)

## 持续集成

测试配置为在以下情况自动运行：
- 代码提交
- Pull Request
- 夜间构建

## 贡献指南

添加新测试时，请：
1. 遵循现有命名约定
2. 包含安全边界测试
3. 添加必要的模拟
4. 更新此文档