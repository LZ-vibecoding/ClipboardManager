; 剪贴板历史管理器 — Inno Setup 安装脚本
;
; 使用方法:
; 1. 先执行 dotnet publish 发布应用
; 2. 用 Inno Setup 打开此脚本编译安装包
;    (https://jrsoftware.org/isdl.php)
;
; 或使用命令行编译:
;   iscc build/setup.iss

#define MyAppName "剪贴板历史管理器"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "ClipboardManager"
#define MyAppURL "https://github.com/user/clipboard-manager"
#define MyAppExeName "ClipboardManager.Wpf.exe"

[Setup]
; 安装包基本信息
AppId={{8F4B2A1E-3D8C-4E7F-9A1B-2C3D4E5F6789}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; 安装目录
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes

; 输出
OutputDir=..\build
OutputBaseFilename=ClipboardManager-Setup-{#MyAppVersion}
;SetupIconFile=..\src\ClipboardManager.Wpf\App.ico

; 压缩
Compression=lzma2/ultra
SolidCompression=yes
LZMAUseSeparateProcess=yes
DiskSpanning=no

; 运行时
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible

; 卸载特性
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："; Flags: checkedonce
Name: "autostart"; Description: "开机自动启动"; GroupDescription: "启动选项："; Flags: checkedonce

[Files]
; 主程序（单文件发布）
Source: "..\publish\ClipboardManager.Wpf.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: autostart

[Run]
; 安装完成后运行
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; \
    Flags: postinstall nowait skipifsilent shellexec; Tasks:

[UninstallRun]
; 卸载前关闭应用
Filename: "taskkill"; Parameters: "/f /im {#MyAppExeName}"; Flags: runhidden

[Code]
{ 卸载前清理数据库和图片文件 }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    { 询问是否删除用户数据 }
    if MsgBox('是否同时删除所有剪贴板历史数据和设置？' + #13#10 +
              '(如果不删除，重新安装后会保留历史数据)',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      var dataDir := ExpandConstant('{localappdata}\ClipboardManager');
      if DirExists(dataDir) then
      begin
        DelTree(dataDir, True, True, True);
      end;
    end;
  end;
end;
