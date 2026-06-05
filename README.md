# 📋 剪贴板历史管理器

Windows 11 剪贴板历史管理工具 — 自动记录复制内容，随时搜索、粘贴历史记录。

![dotnet](https://img.shields.io/badge/.NET-10.0-512BD4)
![platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6)
![language](https://img.shields.io/badge/language-C%23-239120)

---

## ✨ 功能

- **自动记录** — 监控 `Ctrl + C` 复制，自动保存文本和图片
- **历史查看** — 按 `Ctrl + Shift + V` 弹出历史窗口
- **搜索过滤** — 即时搜索历史内容
- **固定/删除** — 重要内容可固定不被清理，不需要的可以删除
- **吸附侧边栏** — 点击 `─` 按钮，窗口隐藏为屏幕右侧的精致手柄
- **系统托盘** — 后台运行，不占用任务栏
- **开机自启** — 可在设置中开启
- **本地存储** — 所有数据存在本地 SQLite，不联网不上传

## 🚀 快速开始

### 直接使用（绿色版）

从 [Releases](https://github.com/LZ-vibecoding/ClipboardManager/releases) 下载 `ClipboardManager.Wpf.exe`，双击运行即可。

### 从源码构建

```bash
# 克隆
git clone https://github.com/LZ-vibecoding/ClipboardManager.git
cd ClipboardManager

# 构建
dotnet build src/ClipboardManager.Wpf

# 运行
dotnet run --project src/ClipboardManager.Wpf

# 发布单文件
dotnet publish src/ClipboardManager.Wpf -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

### 安装包

用 Inno Setup 编译 `build/setup.iss` 生成安装包。

## 🏗 项目结构

```
ClipboardManager/
├── src/
│   ├── ClipboardManager.Core/       # 核心层（无 UI 依赖）
│   │   ├── Models/                  # 数据模型
│   │   ├── Services/                # 监控、存储、热键、粘贴等核心服务
│   │   └── Helpers/                 # Win32 API 声明
│   │
│   ├── ClipboardManager.Wpf/        # WPF 界面层
│   │   ├── Views/                   # 历史窗口、设置窗口
│   │   ├── ViewModels/              # MVVM ViewModel
│   │   └── Converters/              # 值转换器
│   │
│   └── ClipboardManager.Tests/      # 单元测试
│
├── docs/                            # 文档（需求、架构、开发计划）
├── build/                           # 安装包脚本
└── data/                            # 运行时数据（数据库、图片缓存）
```

## 🛠 技术栈

| 技术 | 用途 |
|------|------|
| C# WPF (.NET 10) | 桌面 UI 框架 |
| SQLite + Dapper | 本地数据存储 |
| Win32 API | 剪贴板监控、全局热键 |
| Hardcodet.NotifyIcon.Wpf | 系统托盘图标 |
| MVVM | UI 架构模式 |

## ⚠️ 已知限制

- **粘贴按钮**：点击后内容复制到剪贴板，窗口自动关闭，需要手动按 `Ctrl+V` 粘贴。这是由于 Windows UIPI（用户界面特权隔离）限制，从非管理员进程无法自动向管理员窗口发送键盘输入。

## 📄 许可

[MIT](LICENSE)
