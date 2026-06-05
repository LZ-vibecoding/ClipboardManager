@echo off
chcp 65001 >nul

title 剪贴板历史管理器 — 发布工具

echo ===================================
echo  剪贴板历史管理器 — 发布工具
echo ===================================
echo.

rem 清理旧编译输出
echo [1/3] 清理编译缓存...
dotnet clean ..\src\ClipboardManager.slnx >nul 2>&1
rmdir /s /q ..\src\ClipboardManager.Wpf\obj 2>nul
rmdir /s /q ..\src\ClipboardManager.Wpf\bin 2>nul

echo [2/3] 编译发布版本...
echo.
dotnet publish ..\src\ClipboardManager.Wpf\ClipboardManager.Wpf.csproj ^
    -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true
echo.

if %ERRORLEVEL% NEQ 0 (
    echo [错误] 编译失败，请检查错误信息。
    pause
    exit /b 1
)

echo [3/3] 复制发布文件到 dist 目录...
if not exist ..\dist mkdir ..\dist
if exist ..\dist\portable rmdir /s /q ..\dist\portable
xcopy ..\src\ClipboardManager.Wpf\bin\Release\net10.0-windows\win-x64\publish ^
    ..\dist\portable\ /E /I /Q >nul

echo.
echo ===================================
echo  发布完成！
echo ===================================
echo.
echo  绿色版路径: dist\portable\ClipboardManager.Wpf.exe
echo  大小: ~40-60 MB (单文件，自包含 .NET 10)
echo.
echo  如需制作安装包:
echo  1. 下载 Inno Setup: https://jrsoftware.org/isdl.php
echo  2. 用 iscc build\setup.iss 编译
echo.
pause
