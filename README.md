# 轻历 QingLi

一个**本地优先**的 Windows 日程与提醒应用：月 / 周 / 日三种视图，事件增删改查，每天 / 每周 / 每月 / 工作日重复，到点提醒（托盘常驻 + 右下角气泡），ICS 导入导出，事件搜索，左侧迷你月历定位。全部数据保存在本机，**不联网、无任何第三方依赖**。

- 安装包：`QingLi-Setup-1.0.0.exe`（Inno Setup 制作，中文向导，装到 `%LOCALAPPDATA%\Programs\QingLi`，免管理员权限）
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

- 左侧迷你月历：点击日期跳转，带事件小圆点，可翻月
- 左侧「当日日程」列表：点击条目直接编辑
- 系统托盘常驻：关闭窗口最小化到托盘（气泡提示一次），托盘菜单可新建 / 打开 / 今天 / 退出
- 顶部搜索：按标题 / 地点 / 备注过滤，回车打开第一条
- `⋯` 菜单：导入 ICS、导出全部日程为 ICS（RFC 5545：`RRULE`、`VALARM`、转义与折行、UTC 时间）、打开数据文件夹、关于
- Win11 风格深色主题，自绘控件、深色圆角标题栏、DPI 感知

### 快捷键

| 按键 | 功能 |
| --- | --- |
| `Ctrl+N` | 新建日程（当前选中日期） |
| `Ctrl+F` | 聚焦搜索框 |
| `Ctrl+1/2/3` | 月 / 周 / 日视图 |
| `T` | 回到今天 |
| 方向键 | 月视图中移动选中日期 |
| `Enter` | 在选中日期新建日程 |
| `Esc` | 关闭搜索 / 对话框 |

## 仓库结构

```
.
├── src/                 # 应用源码（C# 5，目标 .NET Framework 4.x）
│   ├── Program.cs       # 入口、单实例、 DPI 设置
│   ├── MainForm.cs      # 主窗口：布局、命令分发、托盘
│   ├── MonthView.cs     # 月视图
│   ├── TimeGridView.cs  # 周 / 日视图（共用）
│   ├── EventDialog.cs   # 日程编辑对话框
│   ├── Controls.cs      # 自绘控件（按钮 / 分段 / 迷你月历 / 日期框 / 下拉 / 复选框）
│   ├── Models.cs        # 数据模型 + 持久化 + 重复展开
│   ├── Ics.cs           # ICS (RFC 5545) 导入导出
│   ├── Reminder.cs      # 提醒调度 + 气泡
│   ├── Theme.cs         # 配色 / 圆角 / 深色标题栏
│   └── app.manifest     # DPI / 兼容性声明
├── tests/
│   └── TestCore.cs      # 数据层自动化测试（22 项断言）
├── tools/               # UI 实测辅助脚本（屏幕坐标驱动 + 截图）
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
# [3/3] ISCC 打包 installer.iss -> dist\QingLi-Setup-1.0.0.exe
```

## 测试

数据层有 22 项自动化断言（持久化往返、每天 / 每周 / 每月（月末收敛）/ 工作日重复展开、区间裁剪、单次删除排除项、ICS 导入导出往返）：

```powershell
csc /target:exe /out:build\TestCore.exe tests\TestCore.cs src\Models.cs src\Ics.cs
build\TestCore.exe      # 退出码 0 表示全部通过
```

UI 层验证脚本在 `tools/`（屏幕坐标驱动 + 逐步截图，因自绘控件无无障碍句柄，未走 UIAutomation）。

## 数据

- 日程数据保存在 `%APPDATA%\QingLi\data.xml`，每次修改立即落盘，重启不丢
- 全程零联网；卸载时可选择是否一并删除日程数据，卸载后无残留

## 已知限制

1. 重复日程的「仅此一次」编辑会生成一条独立日程并把原系列的那次标记为排除；排除项无法在界面上恢复
2. ICS 每周重复按事件开始日期所在的星期展开，不解析 `BYDAY` 的具体星期；多天 `BYDAY`（如 `MO,WE,FR`）不支持，只识别为工作日
3. ICS 时区：带 `Z` 的按 UTC 转本地；带 `TZID` 的忽略时区名、按本地时间处理
4. 提醒依赖应用在运行（最小化到托盘时照常提醒），未做开机自启 / 计划任务
5. 按系统 DPI 缩放（system-aware），未做 Per-Monitor V2 的多显示器混合 DPI 自适应；100% / 150% 下验证正常
6. 安装包未签名，首次运行可能弹 SmartScreen「未知发布者」，点「仍要运行」即可
7. 中文安装界面覆盖向导主流程，少数边缘提示串（磁盘空间不足、文件占用等）回退英文

## 卸载

设置 → 应用 →「轻历」→ 卸载，或开始菜单 → 卸载「轻历」。卸载结束时会询问是否同时删除本机的日程数据。

## License

[MIT](LICENSE) © 2026 liunian0403
