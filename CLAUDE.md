# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 开发规则 (Development Rules)

1. **语言要求**: 使用中文回答用户问题和进行交流
2. **操作系统**: 当前开发环境为 Windows 系统，所有操作和命令都应基于 Windows
3. **批处理脚本规范**: 开发和编辑 bat 脚本文件时不要包含中文注释，仅使用英文注释
4. **脚本修复原则**: 修复现有脚本时，优先在原有脚本文件基础上进行修改，非必需情况不要创建新的脚本文件

## Project Overview

WinServiceManager is a WPF desktop application that simplifies registering any executable or script as a Windows service using WinSW (Windows Service Wrapper). The application requires Administrator privileges and implements enterprise-grade security features.

## Development Commands

### Build and Test Commands

```bash
# Build the entire solution
dotnet build src/WinServiceManager.sln

# Run all tests
dotnet test

# Run tests using provided scripts
./scripts/test/run-tests.bat      # Windows batch
./scripts/test/run-tests.ps1      # PowerShell

# Build release version
./scripts/build/build-release.bat          # Windows batch
./scripts/build/build-release.ps1          # PowerShell

# Publish distribution package
./scripts/build/build-release.bat --publish -o publish

# Run application (requires Administrator privileges)
dotnet run --project src/WinServiceManager
```

### Project Structure

```
src/
├── WinServiceManager.sln              # Solution file
├── WinServiceManager/                  # Main WPF application
│   ├── Models/                         # Data models and validators
│   │   ├── ServiceItem.cs             # Service entity
│   │   ├── PathValidator.cs           # Security: path traversal protection
│   │   └── CommandValidator.cs        # Security: command injection protection
│   ├── Services/                       # Business logic services
│   │   ├── ServiceManagerService.cs   # Core service management
│   │   ├── WinSWWrapper.cs            # WinSW command wrapper
│   │   └── LogReaderService.cs        # Log monitoring
│   ├── ViewModels/                     # MVVM view models
│   ├── Views/                          # WPF views (XAML)
│   └── App.xaml.cs                     # Application entry point with DI setup
└── WinServiceManager.Tests/           # Unit tests
    ├── UnitTests/                       # Unit tests for core components
    └── IntegrationTests/               # Integration tests
```

## Architecture Notes

### MVVM Pattern
- Uses CommunityToolkit.Mvvm for MVVM implementation
- Dependency injection configured in App.xaml.cs
- All ViewModels inherit from BaseViewModel (INotifyPropertyChanged)

### Security Implementation
The application implements enterprise-grade security measures:
- **PathValidator**: Prevents directory traversal attacks (`../`)
- **CommandValidator**: Prevents command injection (`&&`, `||`, `;`, `|`, `>`, `<`)
- **XML Security**: Uses XElement and SecurityElement.Escape() for safe XML generation
- **Administrator Privileges**: Required and validated at startup
- **Resource Management**: Full IDisposable implementation with SemaphoreSlim for concurrency

### Service Isolation Strategy
Each service runs in its own isolated directory:
```
services/{ServiceID}/
├── {ServiceID}.exe     # (renamed WinSW.exe)
├── {ServiceID}.xml     # WinSW configuration
└── logs/
    ├── {ServiceID}.out.log
    └── {ServiceID}.err.log
```

### Core Components
- **ServiceManagerService**: Manages service lifecycle using ServiceController
- **WinSWWrapper**: Wraps WinSW commands with security validation and logging
- **JsonDataStorageService**: Persistent storage for service metadata
- **ServiceStatusMonitor**: Real-time service status monitoring with events

## Key Dependencies
- **WinSW v3.0+**: External binary that runs as the service host
- **CommunityToolkit.Mvvm**: MVVM framework
- **Microsoft.Extensions.***: DI, Configuration, Logging
- **System.ServiceProcess.ServiceController**: Windows service control

## Testing Strategy
- Unit tests cover ViewModels, Services, and Validators
- Security tests have 95%+ coverage on validation components
- Tests use temporary directories for isolation
- Run with `dotnet test` or use provided test scripts

## Runtime Requirements
- Windows 10/11 or Windows Server 2019/2022
- .NET 8 Runtime
- Administrator privileges (required and enforced)
- WinSW-x64.exe (downloaded automatically or manually to templates/ directory)

## WinSW Setup (First Time Only)

### Automatic Download (Recommended)
```bash
# Run the download script (PowerShell)
.\scripts\download-winsw.ps1

# Or run the batch file version
.\scripts\download-winsw.bat
```

### Manual Download
1. Download WinSW-x64.exe from: https://github.com/winsw/winsw/releases
2. Place the file in: `src/WinServiceManager/templates/WinSW-x64.exe`

### Verification
After setup, the application will verify WinSW availability on startup.

## Important Notes
- Always run as Administrator - the app validates this on startup
- Services are managed through WinSW configuration files
- All file paths go through security validation
- Each service has complete isolation (separate config, logs, executable)
- Application maintains metadata database for UI service listing