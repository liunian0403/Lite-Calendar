# 轻历 QingLi

一个**本地优先**的 Windows 日程与提醒应用：月 / 周 / 日三种视图，事件增删改查，每天 / 每周 / 每月 / 工作日重复，到点提醒（托盘常驻 + 右下角气泡），ICS 导入导出，事件搜索，中国法定节假日与调休自动展示，左侧迷你月历定位。日程数据保存在本机；节假日数据从公开数据源自动拉取并本地缓存，断网时使用内置数据。

- 安装包：`QingLi-Setup-1.2.0.exe`（Inno Setup 制作，中文向导，装到 `%LOCALAPPDATA%\Programs\QingLi`，免管理员权限）
- 系统要求：Windows 7 SP1 及以上（Windows 10 / 11 体验最佳），.NET Framework 4.6+（Win10/11 已内置）

## 功能

### 视图

- 月 / 周 / 日三种视图，右上角分段控件切换（快捷键 `Ctrl+1` / `Ctrl+2` / `Ctrl+3`），启动与切换后默认定位到今天
- 左上角 `‹` `›` 翻页、「今天」按钮一键回到今天
- 周 / 日视图带当前时刻指示线，可滚轮上下滚动，双击空白处新建
- 月视图双击空白格新建，右键弹出「新建 / 编辑 / 删除」

### 日程

- 字段：标题、开始 / 结束时间、全天、地点、备注、提醒、8 色标签
- 重复：不重复 / 每天 / 每周 / 每月 / 工作日（周一至周五），可设重复截止日
- 重复日程支持「仅此一次」或「整个系列」的编辑与删除（删除单次会记住排除项，重启后仍生效）
- 提醒：不提醒 / 开始时 / 提前 5 / 15 / 30 分钟 / 1 小时 / 1 天；到点弹右下角气泡（不抢焦点），支持「5 分钟后提醒」与「知道了」

### 其他

- 中国法定节假日与调休：自动从公开数据源（[holiday-cn](https://github.com/NateScarlet/holiday-cn)，依据国务院办公厅公告整理）拉取当年与次年数据，本地缓存，断网自动回退到内置快照；月 / 周 / 日视图与迷你月历均标注「休」（法定放假）与「班」（调休上班），状态栏显示数据来源与更新时间
- 顶部搜索：按标题 / 地点 / 备注过滤，回车打开第一条；同时支持节假日中文名与英文别名搜索（如 `national day` → 国庆节、`spring festival` → 春节、`holiday` 列出全部即将到来的节假日）；英文关键词 `calendar` / `calander`（拼写变体均可）回车即回到今天
- 左侧迷你月历：点击日期跳转，带事件小圆点，可翻月，节假日日期标红
- 左侧「当日日程」列表：点击条目直接编辑；标题附带当天节假日 / 调休信息
- 系统托盘常驻：关闭窗口最小化到托盘（气泡提示一次），托盘菜单可新建 / 打开 / 今天 / 退出
- `⋯` 菜单：导入 ICS、导出全部日程为 ICS（RFC 5545：`RRULE`、`VALARM`、转义与折行、UTC 时间）、主题、打开数据文件夹、关于
- 白天 / 黑夜主题：三档「跟随系统 / 始终浅色 / 始终深色」，入口在 `⋯` →「主题」与托盘右键「主题」，选中即生效、立即写盘，不重启、不重载日程；「跟随系统」会在系统切换深浅色后 2 秒内自动跟随（含已打开的编辑对话框与提醒气泡）。深色沿用 1.1.0 的 Win11 Dark 外观，浅色为白底深字，两套配色均按 WCAG 2.1 AA（正文 ≥ 4.5:1）校验
- Win11 风格自绘控件、深浅两套标题栏（DWM 沉浸式属性双向切换）、DPI 感知

### 快捷键

| 按键 | 功能 |
| --- | --- |
| `Ctrl+N` | 新建日程（当前选中日期） |
| `Ctrl+F` | 聚焦搜索框 |
| `Ctrl+1/2/3` | 月 / 周 / 日视图 |
| `Ctrl+Shift+L` | 循环切换主题（跟随系统 → 始终浅色 → 始终深色） |
| `T` | 回到今天 |
| 方向键 | 月视图中移动选中日期 |
| `Enter` | 在选中日期新建日程 |
| `Esc` | 关闭搜索 / 对话框 |

## 仓库结构

```
.
├── src/                 # 应用源码（C# 5，目标 .NET Framework 4.x）
│   ├── Program.cs       # 入口、单实例、 DPI 设置
│   ├── MainForm.cs      # 主窗口：布局、命令分发、搜索、托盘
│   ├── MonthView.cs     # 月视图（含节假日 / 调休标注）
│   ├── TimeGridView.cs  # 周 / 日视图（共用）
│   ├── EventDialog.cs   # 日程编辑对话框
│   ├── Controls.cs      # 自绘控件（按钮 / 分段 / 迷你月历 / 日期框 / 下拉 / 复选框）
│   ├── Models.cs        # 数据模型 + 持久化 + 重复展开
│   ├── HolidayService.cs# 中国法定节假日 / 调休：拉取、缓存、搜索（多数据源回退）
│   ├── HolidayData.cs   # 构建期嵌入的节假日快照（断网兜底）
│   ├── JsonLite.cs      # 极简 JSON 解析器（无第三方依赖）
│   ├── Ics.cs           # ICS (RFC 5545) 导入导出
│   ├── Reminder.cs      # 提醒调度 + 气泡
│   ├── Theme.cs         # 深浅两套调色板、语义令牌、主题档位、系统主题、标题栏
│   ├── AssemblyInfo.cs  # 程序集版本号（与 installer.iss 的 AppVersion 同步）
│   └── app.manifest     # DPI / 兼容性声明
├── tests/
│   ├── TestCore.cs      # 数据层自动化测试（22 项断言）
│   ├── TestHoliday.cs   # 节假日 / JSON / 搜索关键词测试（36 项断言）
│   └── TestTheme.cs     # 主题 / 对比度 / 持久化测试（127 项断言）
├── tools/               # UI 实测辅助脚本（屏幕坐标驱动 + 截图）+ verify.ps1 回归脚本
├── installer.iss        # Inno Setup 安装脚本
├── zh-CN.isl            # Inno Setup 中文消息表
└── build.ps1            # 一键构建：生成图标 → csc 编译 → ISCC 打包
```

## 构建

依赖只有两个，均为现成工具，**不引入任何第三方 NuGet 包**：

1. Windows 自带的 .NET Framework C# 编译器 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`（源码按 C# 5 语法编写）
2. Inno Setup 6（`ISCC.exe`，默认查找 `%LOCALAPPDATA%\Programs\Inno Setup 6\`，也支持 `C:\Program Files (x86)\Inno Setup 6\`；可用 `winget install JRSoftware.InnoSetup` 安装）

一键构建安装包：

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
# [1/3] 生成多尺寸 app.ico（System.Drawing 绘制）
# [2/3] csc 编译 src\*.cs -> build\QingLi.exe（带图标与 DPI manifest）
# [3/3] ISCC 打包 installer.iss -> dist\QingLi-Setup-1.2.0.exe
```

## 测试

数据层有 22 项自动化断言（持久化往返、每天 / 每周 / 每月（月末收敛）/ 工作日重复展开、区间裁剪、单次删除排除项、ICS 导入导出往返）：

```powershell
csc /target:exe /out:build\TestCore.exe tests\TestCore.cs src\Models.cs src\Ics.cs
build\TestCore.exe      # 退出码 0 表示全部通过
```

节假日与搜索关键词有 36 项自动化断言（JSON 解析、数据源与内置快照两种形态合并、本地缓存与损坏回退、`calendar` / `calander` 关键词、中英文节假日搜索、去重与范围限制）：

```powershell
csc /target:exe /out:build\TestHoliday.exe tests\TestHoliday.cs src\HolidayService.cs src\HolidayData.cs src\JsonLite.cs
build\TestHoliday.exe   # 退出码 0 表示全部通过
```

主题有 127 项自动化断言（三档解析与非法值回退、全新安装 / 升级默认值、深浅两套调色板取值、WCAG 2.1 AA 对比度、事件 8 色映射、`<Theme>` 写入读回、损坏文件备份回退、主题变更广播）：

```powershell
csc /target:exe /out:build\TestTheme.exe tests\TestTheme.cs src\Theme.cs src\Models.cs src\Ics.cs /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.dll
build\TestTheme.exe     # 退出码 0 表示全部通过
```

一键回归（编译应用 + 三个测试套件 + 主题静态约束 + 版本号一致性）：

```powershell
powershell -ExecutionPolicy Bypass -File tools\verify.ps1
```

UI 层验证脚本在 `tools/`（屏幕坐标驱动 + 逐步截图，因自绘控件无无障碍句柄，未走 UIAutomation）。

## 数据

- 日程数据保存在 `%APPDATA%\QingLi\data.xml`，每次修改立即落盘，重启不丢
- 日程数据不上传任何服务器；仅节假日信息在运行时从公开数据源（jsDelivr CDN 上的 holiday-cn 数据）自动拉取，并缓存到 `%APPDATA%\QingLi\holidays-{year}.json`，断网时使用内置快照，不影响其他功能
- 卸载时可选择是否一并删除日程数据，卸载后无残留

## 已知限制

1. 重复日程的「仅此一次」编辑会生成一条独立日程并把原系列的那次标记为排除；排除项无法在界面上恢复
2. ICS 每周重复按事件开始日期所在的星期展开，不解析 `BYDAY` 的具体星期；多天 `BYDAY`（如 `MO,WE,FR`）不支持，只识别为工作日
3. ICS 时区：带 `Z` 的按 UTC 转本地；带 `TZID` 的忽略时区名、按本地时间处理
4. 提醒依赖应用在运行（最小化到托盘时照常提醒），未做开机自启 / 计划任务
5. 按系统 DPI 缩放（system-aware），未做 Per-Monitor V2 的多显示器混合 DPI 自适应；125% / 150% 缩放未实测（1.2.0 未改动尺寸 / 字体 / 间距）
6. 安装包未签名，首次运行可能弹 SmartScreen「未知发布者」，点「仍要运行」即可
7. 中文安装界面覆盖向导主流程，少数边缘提示串（磁盘空间不足、文件占用等）回退英文
8. 节假日数据依赖上游 holiday-cn 的年度发布节奏：次年安排通常在当年 11–12 月公布，公布前应用内次年节假日为空（联网成功后自动补全）；内置快照为构建期数据
9. 主题：Win7 或关闭 DWM 时，深色主题下标题栏仍为系统浅色（客户区正常，属已知限制）；`MessageBox`（删除确认、关于）由系统绘制，不随应用主题变化
10. 主题：深色下多行「备注」输入框的滚动条仍是系统原生浅色（1.1.0 既有，需自绘滚动条才能统一，本次未改）
11. 主题默认值：全新安装为「跟随系统」，从 1.1.0 升级（`data.xml` 无 `<Theme>`）为「始终深色」以保持旧外观；若退回 1.1.0 版程序使用，其保存时会丢弃 `<Theme>`，再回到 1.2.0 会按升级情形重新按深色处理

## 卸载

设置 → 应用 →「轻历」→ 卸载，或开始菜单 → 卸载「轻历」。卸载结束时会询问是否同时删除本机的日程数据，选「否」即保留（升级 / 重装后可继续使用）。

> ⚠️ **静默卸载会无提示删除数据**：用 `/VERYSILENT`（或 `/SILENT`）卸载时不会弹询问框，`%APPDATA%\QingLi` 会被直接删除，日程与节假日缓存一并丢失。普通用户请走交互式卸载并在弹窗里选「否」保留数据；脚本化卸载前请先自行备份 `%APPDATA%\QingLi`。（1.1.0 同样行为，1.2.0 仅在文档层面提示。）

## License

[MIT](LICENSE) © 2026 liunian0403
