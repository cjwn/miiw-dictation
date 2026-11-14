@echo off
setlocal enableextensions
cd /d "%~dp0"

rem 用 Windows 自带的 .NET Framework 编译器，不需要安装 Visual Studio。
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [!] 找不到 csc.exe，说明系统缺少 .NET Framework 4.x。
    echo     Windows 10/11 一般自带；缺失时可从这里安装：https://dotnet.microsoft.com/download/dotnet-framework
    pause
    exit /b 1
)

echo 正在编译...
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /warn:4 ^
    /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll ^
    /out:"MIIW Voice.exe" MIIWVoiceWin.cs

if errorlevel 1 (
    echo.
    echo [!] 编译失败。请把上面的错误信息发给我。
    pause
    exit /b 1
)

echo.
echo [OK] 已生成 "MIIW Voice.exe"。
echo      双击运行后，托盘会出现一个麦克风图标。
pause
