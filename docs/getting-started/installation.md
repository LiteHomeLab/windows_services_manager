# 安装说明

## 系统要求

- **操作系统**: Windows 10/11 或 Windows Server 2019/2022
- **运行时**: .NET 8 Runtime
- **权限**: 管理员权限（必需）
- **依赖**: WinSW-x64.exe v3.0+

## 安装步骤

### 1. 克隆项目

```bash
git clone https://github.com/LiteHomeLab/windows_services_manager.git
cd windows_services_manager
```

### 2. 安装 .NET 8 Runtime

从 [Microsoft .NET 官网](https://dotnet.microsoft.com/download/dotnet/8.0) 下载并安装 .NET 8 Runtime。

### 3. 配置 WinSW

#### 自动下载（推荐）
```powershell
# 使用 PowerShell 脚本
.\scripts\download-winsw.ps1

# 或使用批处理文件
.\scripts\download-winsw.bat
```

#### 手动下载
1. 访问 [WinSW 发布页面](https://github.com/winsw/winsw/releases)
2. 下载 `WinSW-x64.exe`
3. 将文件放置到 `src/WinServiceManager/templates/WinSW-x64.exe`

### 4. 构建项目

```bash
# 使用 PowerShell
.\scripts\build\build-release.ps1

# 或使用批处理文件
.\scripts\build\build-release.bat
```

### 5. 运行测试

```bash
# 使用 PowerShell
.\scripts\test\run-tests.ps1

# 或使用批处理文件
.\scripts\test\run-tests.bat
```

## 验证安装

运行应用程序并确保：

1. **管理员权限验证**: 应用程序启动时验证管理员权限
2. **WinSW 验证**: 应用程序检查 WinSW 可用性
3. **基本功能**: 能够创建和管理测试服务

```bash
# 运行测试服务示例
.\scripts\examples\test_service.py
```

## 目录权限

应用程序需要以下目录的写入权限：
- `services/` - 服务配置和日志存储
- `templates/` - WinSW 模板文件
- 临时目录 - 用于服务安装过程

## 故障排除

### 常见问题

1. **权限不足**
   - 确保以管理员身份运行
   - 检查 UAC 设置

2. **WinSW 未找到**
   - 运行下载脚本
   - 或手动下载到正确位置

3. **.NET 8 未安装**
   - 安装 .NET 8 Runtime
   - 使用 `dotnet --version` 验证

4. **服务创建失败**
   - 检查路径是否包含特殊字符
   - 确保目标可执行文件存在
   - 验证命令参数安全性

### 日志位置

- 应用程序日志: `logs/`
- 服务日志: `services/{ServiceID}/logs/`

---

*最后更新: 2025-12-20*