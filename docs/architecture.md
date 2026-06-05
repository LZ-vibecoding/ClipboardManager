# 剪贴板历史管理器 — 技术架构设计

> 版本: v1.0  
> 最后更新: 2026-06-04

---

## 1. 技术选型

| 维度 | 选择 | 版本 | 理由 |
|------|------|------|------|
| 语言 | C# | 12.0 | .NET 原生，Windows 桌面开发最佳选择 |
| 运行时 | .NET | 8.0 | 长期支持(LTS)，高性能，单文件发布 |
| UI 框架 | WPF | 8.0 | 原生 Windows 界面，系统托盘支持好 |
| ORM | Dapper | 2.1.x | 轻量，比 EF Core 启动快 5 倍，内存省 10MB+ |
| 数据库 | SQLite (Microsoft.Data.Sqlite) | 8.0.x | 本地嵌入式，零配置，无需安装 |
| 托盘图标 | Hardcodet.NotifyIcon.Wpf | 2.0.x | 成熟的 WPF 托盘方案 |
| DI 容器 | Microsoft.Extensions.DependencyInjection | 8.0.x | 标准 .NET 依赖注入 |
| 日志 | Microsoft.Extensions.Logging | 8.0.x | 标准日志抽象 |

---

## 2. 项目分层架构

```
┌─────────────────────────────────────────────────┐
│              ClipboardManager.Wpf                │
│           (WPF UI 层 - 视图 + ViewModel)          │
│  ┌─────────────┐ ┌──────────────┐ ┌───────────┐ │
│  │ HistoryWindow│ │SettingsWindow│ │  TrayIcon │ │
│  │ HistoryVM    │ │ SettingsVM   │ │           │ │
│  └──────┬──────┘ └──────┬───────┘ └───────────┘ │
└─────────┼───────────────┼────────────────────────┘
          │ 项目引用       │
┌─────────┴───────────────┴────────────────────────┐
│              ClipboardManager.Core                 │
│           (核心业务层 - 无 UI 依赖)                 │
│  ┌──────────┐ ┌──────────┐ ┌───────────────────┐ │
│  │ Models   │ │ Services │ │ Data / Helpers     │ │
│  │ 数据模型  │ │ 业务服务   │ │ 数据库 / Win32 API │ │
│  └──────────┘ └──────────┘ └───────────────────┘ │
└──────────────────────────────────────────────────┘
```

### 2.1 分层原则

| 层 | 职责 | 依赖 |
|----|------|------|
| Core | 所有业务逻辑、数据访问、Win32 API 封装 | NuGet 包 |
| Wpf | 界面渲染、用户交互、ViewModel | Core 层 + WPF 程序集 |

- **Core 层不引用任何 WPF/UWP 程序集**
- Wpf 层通过**接口**调用 Core 层服务（依赖注入）
- 所有服务注册在 DI 容器中统一管理

---

## 3. 核心组件设计

### 3.1 ClipboardMonitor — 剪贴板监控

**方式**: Win32 `SetClipboardViewer` API（事件驱动，非轮询）

```
SetClipboardViewer 注册到查看器链
  ↓
WM_DRAWCLIPBOARD 消息（剪贴板变化时系统发送）
  ↓
GetClipboardSequenceNumber() 序列号检查 → 过滤重复通知
  ↓
60ms 防抖定时器 → 过滤单次操作多次通知
  ↓
STA 线程读取剪贴板内容（WPF 主线程是 STA，可直接读取）
  ↓
计算 SHA256 哈希 → 查库去重
  ↓
触发 ClipboardDataReady 事件
```

**关键 API**:
- `SetClipboardViewer(IntPtr hWnd)` — 注册剪贴板查看器
- `ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext)` — 退出时清理
- `GetClipboardSequenceNumber()` — 获取当前序列号（去重用）
- `WM_DRAWCLIPBOARD` / `WM_CHANGECBCHAIN` — 消息常量

### 3.2 HotkeyService — 全局热键

**方式**: Win32 `RegisterHotKey` API

- 默认组合: `Ctrl + Shift + V`
- 使用 `MOD_NOREPEAT` 标志防止按住时重复触发
- 隐藏的 HwndSource 窗口接收 `WM_HOTKEY` 消息
- 注册失败时（热键冲突）抛出异常，UI 显示提示

### 3.3 StorageService — 数据存储

**方式**: SQLite + Dapper

- 数据库位置: `%LocalAppData%\ClipboardManager\clipboard.db`
- WAL 模式 (Write-Ahead Logging) — 读不阻塞写，崩溃安全
- INSERT/UPDATE/DELETE 操作使用 `AsyncLock` 保证线程安全
- 每次启动执行 `PRAGMA integrity_check`

### 3.4 ImageService — 图片处理

**方式**: WPF BitmapSource → PNG 编码 → 磁盘文件

```
BitmapSource
  → 大图缩放（最长边 > 2000px 时等比缩小）
  → 编码为 PNG
  → 保存到 data/images/{hash[:2]}/{hash}.png
  → 生成 120x120 缩略图 → data/thumbnails/{hash[:2]}/{hash}.png
  → 缩略图加入 MemoryCache（最多 100 张，15 分钟过期）
```

### 3.5 PasteService — 粘贴模拟

**方式**: Win32 `SendInput` API

流程:
1. 将目标内容写回系统剪贴板 `Clipboard.SetText/SetImage`
2. 等待 50ms（剪贴板同步）
3. 获取前台窗口句柄 `GetForegroundWindow()`
4. `SetForegroundWindow(hwnd)` 确保目标窗口在前台
5. `SendInput` 发送 Ctrl+V 按键序列

**权限检测**: 比较当前进程与目标窗口的完整性级别，管理员窗口不注入。

### 3.6 ClipboardManagerService — 总协调器

串联所有服务的中央编排器：

```csharp
ClipboardManagerService
  ├── IClipboardMonitor  ← 接收剪贴板变化通知
  ├── IStorageService    ← 读写数据库
  ├── IImageService      ← 保存图片/缩略图
  ├── IHotkeyService     ← 接收热键事件 → 弹出窗口
  ├── IPasteService      ← 模拟粘贴
  └── ISettingsService   ← 读写配置
```

---

## 4. 数据模型

### SQLite 表结构

```sql
CREATE TABLE IF NOT EXISTS clipboard_items (
    id              TEXT PRIMARY KEY,           -- UUID (Guid.NewGuid().ToString("N"))
    content_hash    TEXT NOT NULL,              -- SHA256 十六进制字符串
    type            INTEGER NOT NULL,           -- 0=Text, 1=Image
    text_content    TEXT,                       -- 文本内容 (type=0)
    image_path      TEXT,                       -- 图片相对路径 (type=1)
    thumbnail_path  TEXT,                       -- 缩略图相对路径
    is_pinned       INTEGER NOT NULL DEFAULT 0, -- 0=否, 1=是
    created_at      TEXT NOT NULL,              -- ISO 8601 (yyyy-MM-dd HH:mm:ss.fff)
    updated_at      TEXT NOT NULL
);

CREATE INDEX idx_items_created_at ON clipboard_items(updated_at DESC);
CREATE INDEX idx_items_content_hash ON clipboard_items(content_hash);
CREATE UNIQUE INDEX idx_items_hash_type ON clipboard_items(content_hash, type);

CREATE TABLE IF NOT EXISTS settings (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);
```

### C# 模型

```csharp
public enum ClipboardType { Text = 0, Image = 1 }

public class ClipboardItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ContentHash { get; set; } = "";
    public ClipboardType Type { get; set; }
    public string? TextContent { get; set; }
    public string? ImagePath { get; set; }
    public string? ThumbnailPath { get; set; }
    public bool IsPinned { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // 非数据库映射的 UI 辅助属性
    public string PreviewText  => ...;     // 预览文本（截断）
    public string RelativeTime => ...;     // "3 分钟前" 格式
    public bool IsImage => Type == ClipboardType.Image;
}
```

---

## 5. 去重策略（三重保障）

| 层级 | 手段 | 解决什么问题 |
|------|------|-------------|
| 1 | `GetClipboardSequenceNumber()` | 过滤同一变化的重复 `WM_DRAWCLIPBOARD` 通知 |
| 2 | 60ms 防抖定时器 | 过滤 Office 等应用一次复制多次触发通知 |
| 3 | SHA256 内容哈希 | 过滤真正重复的内容（相同内容复制两次） |

**典型流程**:
1. 用户复制 "Hello World"
2. 序列号 100 → 防抖 60ms → 读取内容 → 哈希 = A1B2C3
3. 查库: 未找到 → INSERT → UI 刷新
4. 用户再次复制 "Hello World"
5. 序列号 101 → 防抖 60ms → 读取内容 → 哈希 = A1B2C3
6. 查库: 已存在 → 仅 UPDATE updated_at = NOW()
7. UI 刷新: 条目"弹"到列表顶部（因为 ORDER BY updated_at DESC）

---

## 6. 存储文件布局

```
%LocalAppData%\ClipboardManager\
├── clipboard.db              # SQLite 数据库
├── clipboard.db-wal          # SQLite WAL 文件
├── clipboard.db-shm          # SQLite 共享内存文件
├── images\                   # 原始图片
│   ├── a1\                   # 哈希前两位做子目录
│   │   └── a1b2c3d4...e5f6.png
│   └── ...
├── thumbnails\               # 缩略图（120x120 正方形）
│   └── ...
└── logs\                     # 日志文件
    └── clipboard.2026-06-04.log
```

---

## 7. 错误处理策略

### 7.1 全局异常捕获
- App.xaml 中注册 `DispatcherUnhandledException` → 记录日志 + 弹出提示
- Task 级异常: `TaskScheduler.UnobservedTaskException` → 记录日志
- 内核级异常: `AppDomain.CurrentDomain.UnhandledException` → 记录日志

### 7.2 数据库异常
- 所有数据库操作 try-catch，失败重试 1 次
- 连接失败 → 等待 1 秒重试，最多 3 次
- 死锁检测 → SQLite 返回 SQLITE_BUSY，重试最多 5 次

### 7.3 剪贴板异常
- COM 异常 (CLIPBRD_E_CANT_OPEN) → 等待 200ms 重试
- 空剪贴板 → 跳过

---

## 8. 性能设计

| 场景 | 手段 |
|------|------|
| 快速启动 | DI 容器懒加载 + 异步初始化 |
| 窗口快速弹出 | 数据库预读 + VirtualizingStackPanel（只渲染可见项） |
| 低内存图片 | MemoryCache+SlidingExpiration 管理缩略图缓存 |
| 列表流畅滚动 | UI 虚拟化 + 延迟加载图片 |
| 数据库不阻塞 UI | 所有数据库操作在后台 Task 执行 |
