# PenBoard（暂名）— Windows 触控笔全屏手写插件实施计划

- 版本：v0.1（方案评审稿）
- 日期：2026-10-24
- 目标平台：Windows 10 1809+ / Windows 11，**ARM64 优先**（同时保留 x64 构建路径）
- 形态：独立托盘应用 + 系统“笔快捷按钮/双击”快捷方式（可理解为 Windows 触控笔插件）

> 2026-10-24 实现更新：已产出 `PenBoard-arm64-v0.1.0.zip` 自包含 ARM64 预览包。
> 已在本机 x64 上用鼠标/合成输入验证：单实例切换、Text 模式识别注入、Annotation 截图保存 PNG/PDF。
> 真实触控笔双击、压感和延迟仍需在 ARM64 笔设备上联调。
>
> 2026-10-24 v0.1.1：安装器与程序启动时移除 Mark-of-the-Web，减少已安装副本的 SmartScreen 重复提示；
> 工具条/候选条/状态文字按任务栏工作区自动上移，底部至少保留 72 DIP。
> MSI 只改善安装/分发，不绕过 SmartScreen；最终方案仍是代码签名。
>
> 2026-10-24 v0.1.2：双击启动时通过 IPC 传递启动瞬间的前台窗口句柄，自动选择输入/批注更可靠；
> 批注切换到输入时重新探测光标；颜色/粗细/笔/橡皮选中状态高亮，修复色块 hover 变白。
> MSI 与签名方案见 `docs/MSI-SIGNING.md`。

---

## 1. 结论

方案可行，建议按“**独立 WPF 托盘应用 + 单实例切换 + 全屏无激活覆盖层 + Windows 系统手写识别 + SendInput 注入**”实现。

核心判断：

1. **双击触发不自己拦截笔硬件**。Windows 的“笔快捷按钮/双击”设置本来就能启动一个 exe 或 lnk；每次双击启动一次 `PenBoard.exe --toggle`，程序用单实例 IPC 把它变成“开/关”切换。这是最稳、最不依赖笔厂商驱动的路线。
2. **有光标时做“全屏手写输入层”**。覆盖层用 `WS_EX_NOACTIVATE` 显示，不抢焦点；笔迹用 Windows Ink 识别，再用 `SendInput` 把文字送进当前光标。
3. **无光标时做“冻结屏幕批注层”**。先把当前屏幕截成背景，再在上面写字；关闭时把“截图 + 笔迹”合成 PNG/PDF。这样批注内容、保存结果和屏幕所见完全一致。
4. **“涂抹消除”用橡皮工具实现**：默认按点擦除（可局部擦），可切换整笔擦除；后续可加“涂鸦手势自动擦除”。

需要提前说明的两个边界：

- 本文按“**系统设置里的笔快捷按钮/双击动作**”实现。如果指的是笔侧键在应用内的双击（系统不映射为快捷动作），那需要笔厂商驱动或原始 HID 监听，属于另一条技术路线。
- 当前开发机没有检测到触控笔/触摸屏（只有 1280×800 显示器），所以“真实笔双击、压感、笔迹延迟”这三项最终需要你的笔设备做一次联调；其余部分可以用合成笔输入 + 热键在开发机验证。

---

## 2. 需求拆解与验收口径

| 场景 | 触发 | 打开后行为 | 再次双击关闭时 |
|---|---|---|---|
| A. 当前有输入光标 | 笔双击 | 全屏透明手写层；写完后系统识别，文字进入光标处；可涂抹擦除 | 取消未完成的识别，丢弃未提交笔迹，仅关闭；不再输入任何文字 |
| B. 当前无输入光标 | 笔双击 | 截取当前屏幕作为背景，进入批注模式；笔迹留在画面上 | 若有笔迹，合成并保存 PNG/PDF，然后关闭；无笔迹则直接关闭 |
| C. 有光标但想批注 | 笔双击后点“批注” | 切换到 B 的冻结批注模式 | 同 B |
| D. 无光标但想输入 | 笔双击后点“输入” | 切换为透明输入层（若目标仍保有焦点） | 同 A |

“关闭不执行操作”的准确含义：

- 文本模式关闭：**不注入文字**，只关闭手写层。已经自动插入的文字不回滚（避免向目标应用发送 Ctrl+Z 造成误撤销）。
- 批注模式关闭：保存是用户明确允许的关闭动作；除此之外不执行其他操作。

---

## 3. 总体技术路线

### 3.1 为什么是独立应用，而不是输入法/驱动

- Windows 没有公开 API 可以“把系统屏幕键盘的手写板嵌进任意应用”，也没有公开 API 可以读取 TabTip 手写板的笔迹。
- 但 Windows 提供了完整的手写识别 API（`Windows.UI.Input.Inking`），桌面应用可以直接调用。
- 因此正确做法是：自己做全屏手写层，调用系统识别器，再把结果注入当前焦点控件。体验上等价于“取代屏幕键盘手写板”，但不依赖 TabTip 内部实现。

### 3.2 双击 → 单实例切换

系统设置每次双击都会启动一次快捷方式，所以：

```text
第一次双击: 启动 PenBoard.exe --toggle
  ├─ 没有实例：启动主程序，打开覆盖层
  └─ 已有实例：通过命名管道发送 TOGGLE，自己退出

第二次双击: 再启动 PenBoard.exe --toggle
  └─ 已有实例：收到 TOGGLE，关闭覆盖层并退出
```

实现细节：

- 互斥体：`Local\PenBoard.SingleInstance`（按登录会话隔离）。
- IPC：命名管道 `PenBoard.Toggle.<SessionId>`，消息只有 `TOGGLE` / `PING` / `QUIT`。
- 命名管道忙时重试 5 次 × 50ms；主实例未就绪时，第二个进程等待后再发。
- 主程序默认驻留托盘，安装时可选“开机启动”。这样第二次双击的响应通常在 100ms 内；即使未驻留，冷启动也能工作，只是首次打开会慢 300–800ms。
- 打包为 WinExe（无控制台窗口），快捷方式参数 `--toggle`。

### 3.3 输入光标检测（决定 A/B 模式）

按“强证据优先、弱证据回退”的顺序：

1. **UI Automation（首选）**
   - `AutomationElement.FocusedElement`
   - `TextPattern2.GetCaretRange(out isActive)`：`isActive == true` 表示控件里有活动插入点。
   - `ValuePattern.IsReadOnly == false`：排除只读文档/只读输入框。
   - 结合 `ControlType`（Edit/Document）和 `IsKeyboardFocusable`。
   - 使用 FlaUI.UIA3（MIT）或原生 UIA COM；UIA 调用放在独立 MTA 线程，带 250–300ms 超时，避免目标应用卡死拖住覆盖层。
2. **Win32 回退**
   - `GetGUIThreadInfo(0)` 取前台线程的 `hwndCaret`、`rcCaret`。
   - 对 `Edit` / `RichEdit` / `Scintilla` 等常见类名，检查 `ES_READONLY`、`EM_GETOPTIONS`。
   - `GetCaretPos` 仅在本进程有效，不作为跨进程主依据。
3. **保守判定**
   - 只有“焦点元素可编辑 + 有活动 caret”才算 A 模式。
   - 读屏器、只读 PDF、桌面、图片查看器 → B 模式。
   - 不确定时默认 B（批注），并在底部提供“切换输入”按钮；用户可手动纠正。

> 取舍：Electron、WinUI、部分 Java/Qt 控件的 UIA 支持不完整。对这类目标，UIA 失败时用 Win32 caret 回退；仍失败则默认批注，避免把文字误发到错误窗口。

### 3.4 全屏覆盖层（不抢焦点）

窗口关键设置：

- `WindowStyle=None`、`ResizeMode=NoResize`、`Topmost=true`、`ShowActivated=false`。
- 扩展样式：`WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`；Topmost 由 `SetWindowPos(HWND_TOPMOST, ...)` 设置。
- 显示：`ShowWindow(SW_SHOWNOACTIVATE)`；`SetWindowPos(HWND_TOPMOST, ..., SWP_NOACTIVATE | SWP_SHOWWINDOW)`。
- 处理 `WM_MOUSEACTIVATE` 返回 `MA_NOACTIVATE`，防止点击工具栏时激活覆盖层。
- 不调用 `SetForegroundWindow`，不清除目标焦点。
- 覆盖层不进入 Alt+Tab（`WS_EX_TOOLWINDOW`）。
- DPI：Per-Monitor V2；按笔所在显示器或前台窗口所在显示器定位。

两种视觉模式共用同一个窗口：

- **输入模式**：`AllowsTransparency=true`，背景全透明，只显示笔迹、底部工具条和候选条；能看见下方文档和光标。
- **批注模式**：背景绘制启动时截取的屏幕位图（不透明），笔迹叠加在上面；视觉上就是“屏幕被冻结并可批注”。

> 风险点：WPF 分层透明窗口 + 实时笔迹在个别机器上可能有输入/性能问题。M0 先做 spike；如果 `InkCanvas` 在 `WS_EX_NOACTIVATE` 下不稳定，回退到原生 `WM_POINTER` 收点 + WPF `DrawingVisual` 渲染。

### 3.5 手写与系统识别

- 笔迹采集：WPF `InkCanvas`（`Stylus` 事件，保留压力）或原生 `WM_POINTER`。
- 只接受笔（`PointerDeviceType.Pen` / `TabletDeviceType.Stylus`）；触摸和鼠标默认不画，避免手掌误触。调试模式允许鼠标。
- 笔迹数据转 Windows Ink：
  - `InkStrokeBuilder.CreateStrokeFromInkPoints(...)`
  - 点集来自 `Stroke.StylusPoints`，带 `PressureFactor`
  - `InkStrokeContainer.AddStroke(...)`
- 识别：
  - `new InkRecognizerContainer()`
  - `GetRecognizers()` 列出已安装识别器
  - `RecognizeAsync(strokeContainer, InkRecognitionTarget.All)`
  - `InkRecognitionResult.GetTextCandidates()` 取候选
- 识别器语言：
  - 默认跟随当前输入语言/系统 UI 语言；可在设置里手动选“中文（简体）/英文/其他”。
  - 若系统没装对应手写识别语言包，提示去“设置 → 时间和语言 → 语言和区域 → 语言选项 → 手写”安装；不静默失败。
- 识别线程：`InkRecognizerContainer` 不是 agile 对象，放在专用 STA 线程创建和使用；UI 线程只传笔迹快照和收结果。
- 分行：MVP 先按“当前未提交的全部笔迹”识别，候选里保留换行；后续按笔迹包围盒的 Y 聚类做多行分段，减少整页识别误差。

### 3.6 文字注入

- 主路径：`SendInput` + `KEYEVENTF_UNICODE`，逐 UTF-16 单元发送；`\n` 用 `VK_RETURN`，`\t` 用 `VK_TAB`。
- 发送前再次确认：
  - 前台窗口仍是打开覆盖层时记录的目标窗口/进程；
  - 目标焦点没有变化；
  - 目标不是只读。
- 失败回退：
  - 目标进程完整性级别更高（管理员程序）时，`SendInput` 会被 UIPI 静默拦截；检测到后把识别结果放入剪贴板，并在托盘提示“已复制，请手动粘贴”。
  - 可选：用户以管理员身份运行 PenBoard 时，可注入管理员目标；默认不主动提权。
- 不采用 `ValuePattern.SetValue` 整框替换，避免覆盖用户已有内容。
- 不采用“模拟 Ctrl+V”作为默认路径，避免破坏用户剪贴板；仅在用户开启“粘贴模式”时使用，并保存/恢复剪贴板。

### 3.7 批注与导出

打开批注模式前：

1. 记录笔所在显示器/前台窗口显示器。
2. 用 `Graphics.CopyFromScreen` 截取该显示器（物理像素）。
3. 再显示覆盖层，避免截到自己。

关闭时：

1. 若没有笔迹：直接关闭，不保存。
2. 若有笔迹：用 `RenderTargetBitmap` 按 2× 或显示器原生分辨率渲染“背景图 + 笔迹”。
3. 保存 PNG：WPF `PngBitmapEncoder`。
4. 保存 PDF：内置一个最小单页 PDF 写入器（页面只放一张图片 XObject，`/FlateDecode`），避免引入 GPL/AGPL 依赖，也避免中文字体嵌入问题。若后续需要多页/矢量，再评估 PDFsharp（MIT）。
5. 默认保存到 `%USERPROFILE%\Pictures\PenBoard\yyyy-MM-dd\`，文件名 `PenBoard_yyyyMMdd_HHmmss.png/.pdf`。
6. 托盘气泡提示保存路径；点击打开文件夹。
7. 保存失败时保留覆盖层和笔迹，提示重试，不丢内容。

默认关闭行为：**自动保存 PNG + PDF**。设置里可改为只存一种、每次询问、或只复制到剪贴板。

### 3.8 涂抹消除

三层能力，按优先级实现：

1. **橡皮工具（MVP）**
   - `InkCanvas.EditingMode = EraseByPoint`，圆形橡皮，支持局部擦除。
   - 工具条可切换“整笔擦除”（`EraseByStroke`）。
2. **笔尾橡皮（如有）**
   - `EditingModeInverted = EraseByStroke`；笔倒过来自动进入擦除。
3. **涂鸦手势擦除（增强）**
   - 检测小范围内反复折返的笔画（方向反转 ≥ 4 次、包围盒小、路径长度远大于对角线）。
   - 判定为“涂抹”后，删除与涂抹路径相交的整笔；阈值可在设置里调。
   - 该功能默认关闭，避免把正常汉字/连笔误判为擦除。

### 3.9 系统双击设置

Windows 没有稳定公开的 API 可以写“笔双击动作”。安装器只做两件事：

1. 生成 `PenBoard.exe` 和 `PenBoard.lnk`（目标 exe，参数 `--toggle`）。
2. 打开 `ms-settings:pen`，显示图文步骤，让用户手动把“双击/快捷按钮”指向快捷方式或 exe。

Windows 11 24H2+：设置 → 蓝牙和其他设备 → 笔和 Windows Ink → 快捷按钮/双击 → 选择“打开应用” → 浏览到 `PenBoard.exe` 或 `.lnk`。
Windows 10：设置 → 设备 → 笔和 Windows Ink → 笔快捷方式 → 双击 → 打开应用。

如果系统只接受 `.exe` 不接受 `.lnk`，直接指向 exe 即可；lnk 只是方便用户识别和移动。

---

## 4. 架构与模块

```text
┌─────────────────────────────┐
│ Windows 笔双击/快捷按钮设置  │
└──────────────┬──────────────┘
               │ 启动 PenBoard.exe --toggle
┌──────────────▼──────────────┐
│ Trigger / SingleInstance    │  互斥体 + 命名管道
│  - 首次：启动主程序          │
│  - 再次：发送 TOGGLE 后退出  │
└──────────────┬──────────────┘
               │
┌──────────────▼─────────────────────────────────────┐
│ App Host（托盘、设置、日志、热键）                  │
│                                                    │
│  ┌──────────────┐   ┌──────────────┐               │
│  │ ContextProbe │   │ ModeRouter   │               │
│  │ UIA + Win32  │──▶│ 输入 / 批注  │               │
│  └──────────────┘   └──────┬───────┘               │
│                            │                       │
│  ┌─────────────────────────▼────────────────────┐  │
│  │ OverlayWindow（WS_EX_NOACTIVATE，全屏）       │  │
│  │  - InkCanvas / WM_POINTER                     │  │
│  │  - 工具条、候选条、撤销                        │  │
│  │  - 输入模式：透明                              │  │
│  │  - 批注模式：截图背景                          │  │
│  └──────────┬───────────────────┬───────────────┘  │
│             │                   │                  │
│  ┌──────────▼────────┐  ┌───────▼───────────────┐  │
│  │ Recognizer        │  │ Exporter              │  │
│  │ Windows Ink       │  │ PNG + 最小 PDF        │  │
│  └──────────┬────────┘  └───────────────────────┘  │
│             │                                      │
│  ┌──────────▼────────┐                             │
│  │ TextInjector      │  SendInput / 剪贴板回退      │
│  └───────────────────┘                             │
└────────────────────────────────────────────────────┘
```

模块职责：

| 模块 | 职责 | 关键实现 |
|---|---|---|
| `Trigger` | 解析 `--toggle`，单实例切换 | Mutex、NamedPipe、WinExe |
| `AppHost` | 托盘、设置、日志、热键、生命周期 | WPF `Application`、`NotifyIcon` |
| `ContextProbe` | 找前台窗口、焦点元素、caret、可编辑性 | FlaUI/UIA、`GetGUIThreadInfo` |
| `OverlayWindow` | 全屏无激活窗口、笔迹、工具条 | `WS_EX_NOACTIVATE`、WPF `InkCanvas` |
| `Recognizer` | 笔迹转文字、候选、语言选择 | `InkRecognizerContainer` |
| `TextInjector` | 把文字送到光标 | `SendInput`、UIPI 检测 |
| `Exporter` | 截图、合成、PNG/PDF | `CopyFromScreen`、`RenderTargetBitmap` |
| `Settings` | JSON 配置、保存目录、调试热键 | `%APPDATA%\PenBoard\settings.json` |

---

## 5. 关键流程

### 5.1 打开（有光标）

```text
双击
 → Trigger 发现主实例
 → AppHost 记录前台窗口/焦点
 → ContextProbe 判定可编辑 + 有 caret
 → OverlayWindow 以透明模式显示（不激活）
 → 用户书写
 → 停笔 800ms
 → Recognizer 识别
 → 候选条显示 1.2s
 → 无选择：TextInjector 发送首选候选
 → 清空已提交笔迹
```

### 5.2 打开（无光标）

```text
双击
 → ContextProbe 判定无可用 caret
 → 截取当前显示器
 → OverlayWindow 以批注模式显示（截图背景）
 → 用户书写/擦除
 → 再次双击
 → 有笔迹：合成 PNG/PDF，保存，提示
 → 关闭覆盖层
```

### 5.3 关闭（文本模式）

```text
再次双击
 → 取消待执行的识别计时器
 → 若有正在发送的文字，等待发送完成（最多 300ms）
 → 丢弃未提交笔迹
 → 隐藏窗口
 → 不发送任何新文字
```

### 5.4 关闭（批注模式）

```text
再次双击
 → 若笔迹为空：隐藏
 → 否则渲染合成图 → 保存 PNG/PDF → 隐藏
 → 保存失败：保留窗口，提示重试
```

### 5.5 异常回退

- 识别器为空 / 语言包缺失 → 保留笔迹，提示安装手写包；不自动注入。
- 目标窗口失焦 / 被关闭 → 不注入；把候选放进剪贴板并提示。
- 目标进程权限更高 → 不注入；复制到剪贴板。
- 覆盖层崩溃 → 顶层异常处理关闭窗口、记录日志、托盘提示。
- 显示器切换/休眠 → 关闭覆盖层，避免画到错误坐标。

---

## 6. 技术选型

| 领域 | 选择 | 理由 | 备选 |
|---|---|---|---|
| 语言/框架 | C# + .NET 8 + WPF | 开发快；透明无激活窗口成熟；WinRT 可调用 | C++/WinRT（性能更好，成本高） |
| 手写采集 | WPF `InkCanvas` | 笔迹、压感、橡皮开箱即用 | 原生 `WM_POINTER` + Direct2D |
| 系统识别 | `Windows.UI.Input.Inking` | 系统自带，无需联网/模型 | 旧版 `IInkAnalyzer` COM |
| 光标检测 | FlaUI.UIA3 + Win32 回退 | UIA 覆盖现代应用，Win32 覆盖传统控件 | 纯 COM UIA |
| 文字注入 | `SendInput` Unicode | 兼容性最好，不破坏控件内容 | 剪贴板粘贴（可选） |
| 截图 | `Graphics.CopyFromScreen` | 简单、可靠、支持多屏 | Windows.Graphics.Capture（需授权） |
| PNG | WPF `PngBitmapEncoder` | 无额外依赖 | ImageSharp |
| PDF | 自写单页图像 PDF | 无 GPL、无字体问题、体积小 | PDFsharp（MIT） |
| 配置 | JSON | 简单可调试 | 注册表 |
| 托盘 | WinForms `NotifyIcon` / WPF 托盘库 | 系统原生 | 第三方托盘库 |
| 测试 | xUnit + 合成笔输入 | 可无笔自动化 | 手工测试 |

目标框架建议：`net8.0-windows10.0.19041.0`，启用 `UseWPF`、`UseWindowsForms`（托盘）、`PerMonitorV2`。

当前机器只有 .NET 8 运行时，没有 SDK；开工第一步需要在工作区安装 .NET 8 SDK（便携版即可，不改系统环境），或使用已有构建机。

---

## 7. 风险与 M0 先导验证

| 风险 | 概率 | 影响 | 验证/缓解 |
|---|---|---|---|
| 开发机没有笔，无法验证真实双击/压感 | 高 | 高 | 用 `--toggle` 热键 + `CreateSyntheticPointerDevice` 合成笔输入验证 80%；最后在你的设备上联调 |
| `WS_EX_NOACTIVATE` + 分层透明 + 实时笔迹不稳定 | 中 | 高 | M0 做最小窗口 spike；失败则改原生 `WM_POINTER` + WPF `DrawingVisual` |
| 系统设置只接受 exe，不接受 lnk | 中 | 低 | 快捷方式同时指向 exe；lnk 仅作入口 |
| 某些应用 UIA 不暴露 caret | 中 | 中 | Win32 `GetGUIThreadInfo` 回退；仍失败默认批注 + 手动切换 |
| 管理员窗口拒绝 SendInput（UIPI） | 中 | 中 | 检测完整性级别；复制到剪贴板提示；可选管理员运行 |
| 缺少中文手写识别语言包 | 中 | 高 | 启动时枚举识别器；缺包给安装指引，不静默失败 |
| 多屏/高 DPI 坐标错位 | 中 | 中 | Per-Monitor V2；统一用物理像素截图、DIP 显示、导出时换算 |
| 受保护窗口截图黑屏 | 低 | 低 | 记录限制；黑屏时仍可批注，保存图会黑；提示用户 |
| 冷启动慢导致第二次双击排队 | 中 | 中 | 默认开机驻留托盘；命名管道重试；可选 NativeAOT 触发器 |
| 关闭时误注入 | 低 | 高 | 关闭先取消识别计时器；发送前校验前台窗口；未提交笔迹直接丢弃 |
| 笔侧键不是系统“双击”动作 | 中 | 高 | 先确认你的笔型号和设置项；若是侧键，需要厂商驱动/原始 HID，另立方案 |

### M0 必做的 5 个 spike（1–2 天）

1. **无激活透明窗口**：全屏、Topmost、`WS_EX_NOACTIVATE`，用合成笔/鼠标画线，确认不抢焦点、不闪任务栏。
2. **WinRT 识别**：控制台/小窗创建 `InkRecognizerContainer`，喂 2–3 笔，打印候选；确认中英文识别器列表。
3. **caret 探测**：在 Notepad、Chrome 地址栏、Edge 文本框、Word、VS Code、只读 PDF 上各测一次，记录判定结果和耗时。
4. **单实例切换**：两个进程同时 `--toggle`，确认只开一个覆盖层，第二次能关。
5. **批注导出**：截图 + 手写 + PNG/PDF，确认 PDF 能打开、方向/比例正确。

M0 通过后再进入正式开发；任何一个 spike 失败，先调整路线，不把风险留到后期。

---

## 8. 里程碑与工期

| 阶段 | 内容 | 产出 | 预估 |
|---|---|---|---|
| M0 | 可行性 spike（上节 5 项） | spike 记录 + 技术结论 | 1–2 天 |
| M1 | 覆盖层、笔迹、工具条、橡皮、撤销 | 可手写的全屏层 | 2–3 天 |
| M2 | caret 探测、识别、候选、注入、失败回退 | 有光标可输入 | 2–3 天 |
| M3 | 截图批注、PNG/PDF、自动保存、托盘提示 | 无光标可批注导出 | 1–2 天 |
| M4 | 单实例/安装器/开机启动/设置/日志/测试 | 可安装的 v0.1 | 1–2 天 |

合计约 **8–12 个工作日**。不含你在真实笔设备上的联调时间；不含代码签名/企业分发。

建议先交付一个“**M1 可手写预览版**”，你可以在设备上先试笔迹延迟和双击触发，再继续 M2/M3，避免方向性返工。

---

## 9. 目录结构（拟）

```text
D:\aihome\PenBoard\
  docs\
    PLAN.md                 ← 本文
    SETUP.md                ← 系统双击设置图文步骤
  src\
    PenBoard.App\           ← WPF 主程序、托盘、覆盖层
    PenBoard.Core\          ← 识别、注入、截图、PDF、配置
    PenBoard.Trigger\       ← 可选：轻量触发器（后续）
  tests\
    PenBoard.Core.Tests\    ← 笔迹分组、涂鸦检测、PDF 结构
    PenBoard.UiTests\       ← 合成笔输入、窗口行为
  installer\
    install.ps1
    uninstall.ps1
    PenBoard.lnk
  artifacts\
```

---

## 10. 验收清单（v0.1）

1. 系统“双击/快捷按钮”指向 PenBoard 后，第一次双击在 300ms 内打开覆盖层；第二次双击关闭。
2. 在 Notepad、Chrome 地址栏、Edge 文本框、Word 中，有 caret 时进入输入模式；覆盖层不抢焦点，Alt+Tab 不出现。
3. 手写“测试 123”后，系统识别并在光标处插入文字；中文/英文/数字混合不崩。
4. 停笔后候选条出现；点候选可改选；关闭时未提交笔迹不注入。
5. 桌面/图片查看器/只读 PDF 中，双击进入批注模式；笔迹可见；再次双击保存 PNG+PDF 并关闭。
6. 保存的 PNG 与屏幕所见一致；PDF 单页、方向正确、可被 Edge/Acrobat 打开。
7. 橡皮可局部擦除；整笔擦除可切换；笔尾橡皮（如有）可用。
8. 150% / 200% DPI、双屏、横竖屏下坐标正确。
9. 管理员窗口注入失败时，文字进入剪贴板并有提示，不静默丢失。
10. 连续使用 30 分钟无崩溃、无明显墨迹延迟（目标 < 30ms）、内存 < 200MB。

---

## 11. 默认产品决策（可改）

- 名称：**PenBoard**（中文显示“墨板”可后续再定）。
- 有光标时：停笔 **800ms** 自动识别并插入首选候选；候选条保留 1.2s。
- 无光标时：关闭自动保存 **PNG + PDF** 两种格式。
- 保存目录：`图片\PenBoard\日期\`。
- 默认不驻留？→ **建议驻留托盘并开机启动**，否则每次双击都要冷启动，第二次关闭会有明显延迟。
- 默认不修改系统设置、不写注册表、不联网。

---

## 12. v0.1 明确不做

- 不拦截/模拟笔硬件双击，不写笔厂商驱动；触发完全依赖 Windows 的笔快捷按钮设置。
- 不修改系统“双击动作”的注册表项；安装器只打开设置页并给步骤。
- 不嵌入、不劫持、不结束 `TabTip.exe`；覆盖层显示在屏幕键盘之上。
- 不做云 OCR/联网识别；只调用本机 Windows Ink 识别器。
- 不做实时视频上的透明批注；v0.1 的批注是“冻结当前屏幕 + 笔迹”。
- 不做多页 PDF、矢量 PDF、手写转形状；v0.1 的 PDF 是单页图像。
- 不做完整绘图软件能力（图层、套索、缩放画布）；只保留手写输入和批注所需的最小工具集。

---

## 13. 参考

- [InkRecognizerContainer（Windows Ink 手写识别）](https://learn.microsoft.com/en-us/uwp/api/windows.ui.input.inking.inkrecognizercontainer)
- [Pen and stylus interactions](https://learn.microsoft.com/en-us/windows/apps/design/input/pen-and-stylus-interactions)
- [GetGUIThreadInfo（前台 caret 位置）](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getguithreadinfo)
- [IUIAutomationTextPattern2::GetCaretRange](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationclient/nn-uiautomationclient-iuiautomationtextpattern2)
- [SendInput（Unicode 注入与 UIPI 限制）](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)
- [WPF InkCanvas](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.inkcanvas)
- [WS_EX_NOACTIVATE 行为说明](https://devblogs.microsoft.com/oldnewthing/20240919-00/?p=110283)
- [FlaUI（MIT，UIA 封装）](https://github.com/FlaUI/FlaUI)
- [PDFsharp（MIT，备选 PDF 方案）](https://github.com/empira/PDFsharp)
- [CreateSyntheticPointerDevice（合成笔输入，用于测试）](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-createsyntheticpointerdevice)
