# 剪贴板历史管理器 — 工作指南

> 项目路径: `C:\Users\86173\Desktop\111`

---

## 📂 标准文档路径

| 文档 | 路径 | 用途 |
|------|------|------|
| 需求规格 | `docs/requirements.md` | 所有功能需求、非功能需求、边界场景定义 |
| 技术架构 | `docs/architecture.md` | 分层架构、组件设计、数据模型、API 选型 |
| 开发计划 | `docs/development-plan.md` | 分 Phase 的执行步骤、每步验证方法 |
| 每日日志 | `devlog/YYYY-MM-DD.md` | 每日完成事项、验证结果、明日计划 |
| 日志模板 | `devlog/template.md` | 开发日志的标准化格式 |

---

## 🔄 工作流程

### 每次开发前
1. 查看 `devlog/` 中最近一日的日志，确认进展
2. 查看 `docs/development-plan.md` 确认当前应进行的步骤
3. 阅读 `docs/architecture.md` 确认相关组件的设计

### 开发过程中
1. 每次只做一个功能点（参考 `docs/development-plan.md` 中的步骤粒度）
2. 完成一个步骤后立即验证（验证方法见开发计划）
3. 验证通过后再进入下一步

### 每次开发后
1. 更新 `devlog/YYYY-MM-DD.md`（今日日志）
2. 如遇计划外调整，同步更新 `docs/development-plan.md`
3. 确保代码可编译、可运行

---

## 🚀 开发命令

```bash
# 构建解决方案
dotnet build src/ClipboardManager.sln

# 运行 WPF 应用
dotnet run --project src/ClipboardManager.Wpf

# 运行单元测试
dotnet test src/ClipboardManager.Tests

# 发布单文件
dotnet publish src/ClipboardManager.Wpf -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## 📐 编码规范

1. **命名**: C# PascalCase（方法/属性/类） + camelCase（局部变量/参数）
2. **异步**: IO 操作全部使用 async/await，避免 .Result / .Wait()
3. **日志**: 使用 ILogger 记录关键操作和异常
4. **异常**: 不吞异常（除非明确预期并记录日志）
5. **注释**: Public API 写 XML 注释，复杂逻辑写行内注释
6. **WPF 规范**: ViewModel 实现 INotifyPropertyChanged，使用命令模式绑定

---

## ⚠️ 注意事项

1. **不要一次性做太多** — 每完成一个计划中的步骤就停下验证
2. **不要跳过验证** — 每个步骤都有验证方法，必须执行通过
3. **不要忽略异常处理** — 与功能代码同步完成，不后补
4. **保持文档同步** — 代码变化后及时更新对应的 .md 文件
5. **用户是新手** — 所有决策和进度需要用通俗语言说明
