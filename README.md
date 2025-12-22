# WinServiceManager

A powerful WPF desktop application that simplifies registering any executable or script as a Windows service using WinSW (Windows Service Wrapper). The application requires Administrator privileges and implements enterprise-grade security features.

## 🚀 Features

- **Simple Service Registration**: Convert any executable or script to a Windows service
- **Enterprise Security**: Protection against path traversal and command injection attacks
- **Service Management**: Install, start, stop, restart, and uninstall services
- **Log Management**: Real-time log viewing and monitoring
- **MVVM Architecture**: Clean, maintainable codebase with modern .NET 8
- **Dependency Injection**: Configurable and testable architecture

## 📋 Requirements

- Windows 10/11 or Windows Server 2019/2022
- .NET 8 Runtime
- Administrator privileges (required and enforced)

## ⚙️ Quick Start

### 1. Clone the Repository
```bash
git clone https://github.com/LiteHomeLab/windows_services_manager.git
cd windows_services_manager
```

### 2. Download WinSW (Required)
WinSW is an external dependency that must be downloaded once:

```powershell
# PowerShell (recommended)
.\scripts\download-winsw.ps1

# Or batch file
.\scripts\download-winsw.bat
```

### 3. Build and Run
```bash
# Build the project
dotnet build src/WinServiceManager.sln

# Run (requires Administrator privileges)
dotnet run --project src/WinServiceManager
```

### 4. Create Your First Service
1. Run the application as Administrator
2. Click "新建服务" (Create Service)
3. Select an executable file
4. Configure service settings
5. Click "创建" (Create)
6. Your service is now installed and ready to use!

## 🏗️ Architecture

### Core Components

- **WinSWWrapper**: Secure wrapper around WinSW commands
- **ServiceManagerService**: Core service lifecycle management
- **JsonDataStorageService**: Persistent metadata storage
- **Security Validators**: PathValidator and CommandValidator for attack prevention
- **ServiceStatusMonitor**: Real-time service status tracking

### Service Isolation

Each service runs in its own isolated directory:
```
services/{ServiceID}/
├── {ServiceID}.exe     # (renamed WinSW.exe)
├── {ServiceID}.xml     # WinSW configuration
└── logs/
    ├── {ServiceID}.out.log
    └── {ServiceID}.err.log
```

## 🔒 Security Features

- **Path Traversal Protection**: Prevents `../` attacks in file paths
- **Command Injection Prevention**: Blocks dangerous command patterns
- **XML Security**: Safe XML configuration generation
- **Administrator Validation**: Requires elevated privileges
- **Input Sanitization**: All user inputs are validated and cleaned

## 🧪 Testing

```bash
# Run all tests
dotnet test

# Run with coverage
.\scripts\test\run-tests.ps1 -Coverage

# Run specific test categories
dotnet test --filter "Category=Security"
dotnet test --filter "Category=Integration"
```

## 📁 Project Structure

```
src/
├── WinServiceManager.sln          # Solution file
├── WinServiceManager/              # Main WPF application
│   ├── Models/                     # Data models and validators
│   ├── Services/                   # Business logic services
│   ├── ViewModels/                 # MVVM view models
│   ├── Views/                      # WPF views (XAML)
│   ├── Converters/                 # Data converters
│   └── Dialogs/                    # Dialog interfaces
├── WinServiceManager.Tests/        # Unit and integration tests
└── scripts/                       # Setup and utility scripts
```

## 🛠️ Development

### Build Scripts

```bash
# Debug build
dotnet build

# Release build
.\scripts\build\build-release.ps1

# Publish distribution
.\scripts\build\build-release.ps1 --publish
```

### Test Scripts

```bash
# Quick test
.\scripts\test\run-tests.bat

# Detailed test with coverage
.\scripts\test\run-tests.ps1 -Coverage -Verbose
```

## 📚 Documentation

- **[📖 文档中心](docs/)**: 完整的项目文档
  - [快速开始](docs/getting-started/QUICKSTART.md) - 快速上手指南
  - [安装说明](docs/getting-started/installation.md) - 详细安装说明
  - [开发文档](docs/development/) - 开发相关文档
- **[CLAUDE.md](CLAUDE.md)**: Claude AI 开发指南和项目架构说明
- **[📜 脚本工具](scripts/)**: 构建和测试脚本工具

## 🔗 Dependencies

- **WinSW v3.0+**: Windows Service Wrapper
- **CommunityToolkit.Mvvm**: MVVM framework
- **Microsoft.Extensions.***: DI, Configuration, Logging
- **xUnit**: Testing framework
- **Moq**: Mocking framework

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 🐛 Issues

If you find any issues or have suggestions, please create an issue on the GitHub repository.

## 🙏 Acknowledgments

- **WinSW Project**: For providing the excellent Windows Service Wrapper
- **.NET Community**: For the amazing tools and frameworks
- **WPF Community**: For the desktop application guidance

---

**WinService Manager** - Making Windows Service Management Simple and Secure 🚀