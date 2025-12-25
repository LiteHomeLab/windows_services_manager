# 实现 WinSW 有效性检查和脚本自动填写工作目录

## 需求概述

### 问题 1：WinSW 假文件问题
- 当前 `templates/WinSW-x64.exe` 是一个 120 字节的占位文件，导致新建服务时失败
- 需要在程序启动时检查 WinSW 有效性，无效时提示用户

### 问题 2：脚本模式缺少自动填写工作目录
- 选择可执行文件时会自动填写工作目录
- 选择脚本文件时没有此功能

---

## 实现计划

### Task 1: 删除假的 WinSW 占位文件
**文件**: `src/WinServiceManager/templates/WinSW-x64.exe`

- 删除 120 字节的占位文件
- 不创建新的占位文件

---

### Task 2: 修改构建脚本，不创建假 WinSW 文件
**文件**: `scripts/build/build-release.bat`

**当前行为**（第 88-96 行）:
```batch
if not exist "%BIN_OUTPUT%\templates" mkdir "%BIN_OUTPUT%\templates"

if exist "%PROJECT_ROOT%src\WinServiceManager\templates\WinSW-x64.exe" (
    copy "%PROJECT_ROOT%src\WinServiceManager\templates\WinSW-x64.exe" "%BIN_OUTPUT%\templates\WinSW-x64.exe" >nul
    echo Copied WinSW executable
) else (
    echo Warning: WinSW executable not found at src\WinServiceManager\templates\WinSW-x64.exe
)
```

**修改后**:
- 仅在源文件存在且文件大小 > 100KB 时才复制
- 添加文件大小检查，避免复制假文件
- 保持警告信息不变

**发布模式部分**（第 129-138 行）需要同样修改

---

### Task 3: 添加 WinSW 验证服务
**文件**: `src/WinServiceManager/Services/WinSWValidator.cs` (新建)

**功能**:
1. 检查 WinSW 文件是否存在
2. 检查文件大小是否合理（> 500KB）
3. 检查是否为有效的 PE 文件（可选）

**接口**:
```csharp
public class WinSWValidator
{
    public (bool IsValid, string? ErrorMessage) ValidateWinSW();
    public string GetWinSWPath();
    public string GetDownloadInstructions();
}
```

---

### Task 4: 在 App.xaml.cs 启动时检查 WinSW
**文件**: `src/WinServiceManager/App.xaml.cs`

**修改位置**: `OnStartup` 方法，管理员权限检查之后

**逻辑**:
1. 注入 `WinSWValidator`
2. 调用验证方法
3. 如果验证失败，显示友好对话框
4. 对话框包含：
   - 错误原因
   - WinSW 下载链接
   - 运行下载脚本的指引
   - 确定按钮（退出程序）

**对话框内容**:
```
WinSW 组件缺失或无效

当前安装的 WinSW 文件存在问题:
[具体错误信息]

请按以下步骤解决:

方法 1 - 运行下载脚本（推荐）:
  运行 scripts\download-winsw.bat 或 scripts\download-winsw.ps1

方法 2 - 手动下载:
  1. 访问 https://github.com/winsw/winsw/releases
  2. 下载 WinSW-x64.exe (最新版本)
  3. 将文件放置到: [应用程序目录]\templates\WinSW-x64.exe

完成后重新启动本程序。
```

---

### Task 5: 增强 WinSWWrapper 的验证逻辑
**文件**: `src/WinServiceManager/Services/WinSWWrapper.cs`

**修改位置**: `InstallServiceAsync` 方法

**当前行为**（第 148-151 行）:
```csharp
if (!File.Exists(_winswTemplatePath))
{
    throw new FileNotFoundException($"WinSW template not found: {_winswTemplatePath}");
}
```

**修改后**:
```csharp
// 使用 WinSWValidator 进行完整验证
var validator = new WinSWValidator(_logger);
var (isValid, errorMessage) = validator.ValidateWinSW();
if (!isValid)
{
    throw new InvalidOperationException($"WinSW validation failed: {errorMessage}");
}
```

---

### Task 6: 脚本模式自动填写工作目录
**文件**: `src/WinServiceManager/ViewModels/CreateServiceViewModel.cs`

**修改位置**: `ScriptPath` 属性的 setter（第 163-174 行）

**当前代码**:
```csharp
public string? ScriptPath
{
    get => _scriptPath;
    set
    {
        if (SetProperty(ref _scriptPath, value))
        {
            ValidateProperty();
            OnPropertyChanged(nameof(CanCreate));
        }
    }
}
```

**修改后**:
```csharp
public string? ScriptPath
{
    get => _scriptPath;
    set
    {
        if (SetProperty(ref _scriptPath, value))
        {
            // 自动设置工作目录为脚本文件所在目录
            if (!string.IsNullOrEmpty(value))
            {
                string? dir = Path.GetDirectoryName(value);
                if (!string.IsNullOrEmpty(dir))
                {
                    WorkingDirectory = dir;
                }
            }

            ValidateProperty();
            OnPropertyChanged(nameof(CanCreate));
        }
    }
}
```

---

### Task 7: 更新项目文档
**文件**: `CLAUDE.md`

**需要更新的部分**:
1. WinSW Setup 部分 - 强调必须下载真实文件
2. Runtime Requirements 部分 - 说明 WinSW 文件大小要求

---

## 依赖关系

```
Task 1 (删除占位文件)
    |
    v
Task 2 (修改构建脚本)
    |
    v
Task 3 (创建验证服务)
    |
    v
Task 4 (启动时检查) <───┐
    |                    |
    v                    |
Task 5 (增强验证逻辑) ───┘
    |
    v
Task 6 (脚本自动目录)
    |
    v
Task 7 (更新文档)
```

---

## 验证测试

1. **构建测试**:
   - 运行 `build-release.bat`
   - 确认不会复制假 WinSW 文件
   - 确认警告信息正确显示

2. **启动测试**:
   - 删除 WinSW 文件，启动程序
   - 验证显示正确的错误对话框
   - 确认程序退出

3. **服务创建测试**:
   - 下载真实 WinSW 文件
   - 创建可执行文件服务
   - 创建脚本服务，验证工作目录自动填写

4. **回归测试**:
   - 运行现有单元测试
   - 验证现有功能不受影响
