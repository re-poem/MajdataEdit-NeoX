# AGENTS.md

Majdata 谱面（simai）编辑器 MajdataEdit-NeoX：Avalonia 12 / .NET 10 的 MVVM 应用，桌面、Android、Browser、iOS 共用 `MajdataEdit-NeoX/MajdataEdit_NeoX`，桌面入口在 `MajdataEdit_NeoX.Desktop/Program.cs`。

**别在这里找架构**：结构与设计看 `MajdataEdit-NeoX/MajdataEdit_NeoX/Docs/DevDoc.md`，命名与分层看 `MajdataEdit-NeoX/MajdataEdit_NeoX/Docs/NamingConventions.md`。上述 `Doc/` 下的任何文档修改前需询问征得用户同意。下面只记这两份文档没写、但要照做的操作规矩。

## 命令

- 构建（改完至少跑一次）：`dotnet build MajdataEdit-NeoX/MajdataEdit_NeoX.Desktop/MajdataEdit_NeoX.Desktop.csproj`
- 只编主项目（改 ViewModel/Model 时够用，更快）：`dotnet build MajdataEdit-NeoX/MajdataEdit_NeoX/MajdataEdit_NeoX.csproj`
- 运行：`dotnet run --project MajdataEdit-NeoX/MajdataEdit_NeoX.Desktop/MajdataEdit_NeoX.Desktop.csproj`
- 没有测试项目，构建通过只说明编得过；播放、键盘、拖拽这类行为要人肉验。
- 包版本集中在 `MajdataEdit-NeoX/Directory.Packages.props`（CPM），Avalonia 系版本必须同步改。

## 规范

- 注释要短：只写「为什么这么做、哪里有坑」，一到四行说完；XML `<summary>` 只给不自明的成员。不写复述代码的注释，不写大段散文。
- 面向用户的文字一律走本地化：用 `LangKeys`（由 `Assets/Localizations/en-US.json` 生成）+ `LocalizationService`，不写字面串；新键 en-US 和 zh-CN 都要加。
- 弹窗统一走 `Types/MessageBox`（有宿主兜底、进度窗口、取消令牌），别直接调 MsBox。
- 文件级 namespace、可空开启、集合表达式 `[]`；属性用 `[ObservableProperty] partial`，命令用 `[RelayCommand]`。
- 平台差异（Android `content://`、Browser 沙箱没有本地路径等）用 `OperatingSystem.Is*` / 条件编译处理，并在注释里写清原因，别让桌面假设到处漏。
- 后台线程回 UI 走 `Dispatcher.UIThread`；`async void` 只给事件处理器。

## Notes

- 真机/打包相关：`Publish/` 是输出，发布走 `vpk pack`（`UpdaterManager.PackId` 必须与 `--packId` 一致）。
