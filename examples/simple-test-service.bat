@echo off
setlocal enabledelayedexpansion

REM 简单的测试服务 - 每秒输出时间戳
REM 用于WinServiceManager测试

echo ========================================
echo 简单测试服务启动
echo ========================================
echo 进程ID: %%
echo 启动时间: %date% %time%
echo.

set /a counter=0

:service_loop
set /a counter+=1
echo 服务运行中 - 计数器: !counter!, 时间: %date% %time%

REM 每10次循环输出状态报告
if !counter! EQU 10 (
    echo.
    echo === 服务状态报告 (!counter!次循环) ===
    echo 运行时间: !counter!秒
    echo 内存使用: 正常
    echo 服务状态: 正常运行
    echo.
)

REM 等待5秒
ping 127.0.0.1 -n 6 > nul

REM 继续循环
goto service_loop

:cleanup
echo.
echo 简单测试服务停止
echo.
pause