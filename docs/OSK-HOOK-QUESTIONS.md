# 自动替换 Windows 系统手写板/触摸键盘：问题清单

## 1. 目标

在 Windows 11 ARM64 上运行 PenBoard（.NET 8 WPF，per-user）：

- 用户点击任意输入框时，自动打开 PenBoard 全屏手写输入；
- 同时关闭/阻止 Windows 系统触摸键盘、手写板；
- 只有开启“自动化替换”时才生效；关闭时系统键盘必须完全正常；
- 关闭 PenBoard 后，再点同一个输入框仍能再次打开；
- 不影响切换网页、切换应用、点击非输入区域。

## 2. 环境

```text
Windows 11 ARM64
PenBoard: C#/.NET 8 WPF, per-user, 自包含
目标应用:
  - Chrome / Edge (Chromium contenteditable, Omnibox)
  - QQ (Electron, ProseMirror contenteditable)
  - 记事本/Word (UIA Edit/Document)
  - UWP 应用 (Windows.UI.Input.InputSite.WindowClass)
  - 部分场景通过 RDP (mstsc, 192.168.2.110:3389)
```

## 3. 实际现象

1. 点击输入框，系统触摸键盘/手写板先弹出来，PenBoard 有时不弹。
2. 切换网页/页面时，PenBoard 会自动弹出，即使没有点击输入框。
3. 关闭 PenBoard 后，点同一个输入框，系统键盘出来，PenBoard 不再弹。
4. 开启自动化后，系统键盘被隐藏/结束后又反复重启。
5. 关闭自动化后，系统触摸键盘仍然不出现（注册表副作用）。
6. 自动化运行时偶发卡死。
7. 通过 RDP 使用时，系统手写板可能是客户端本机窗口，远程 Hook 无效。

## 4. 已尝试的方案

### 4.1 UIA 焦点检测

- `AutomationElement.FocusedElement`
- `GetGUIThreadInfo` / `hwndCaret` / `hwndFocus`
- `ControlType.Edit`、`ComboBox`、`Document`
- 后扩展 `Group/Pane/Custom` + `TextPattern` + ProseMirror/Editor/CodeMirror 类名

问题：

- Chromium/Electron 的 contenteditable 经常是 `ControlType.Group`，不是 `Edit`；
- 点击目标与焦点目标不一致：
  ```text
  Mouse/pen click detected: x=737, y=0
  OnTouchPenInput: target has no caret
  AutoFocus changed: ... ControlType.Edit name=...chat-input-textarea...
  ```
- 切换页面时焦点进入地址栏/搜索框，`AutoFocus` 自动打开 PenBoard；
- `AutomationElement.FromPoint` + 祖先遍历有改善，但不同 Electron 应用的 UIA 树差异很大，仍会漏判。

### 4.2 低层鼠标 Hook

`SetWindowsHookEx(WH_MOUSE_LL)`，读取 `MSLLHOOKSTRUCT.dwExtraInfo`：

```text
0xFF515700 / 0xFF515712
```

问题：

- 不是所有笔/触摸输入都带这个签名；
- 最后改成所有左键都触发，仍会出现“点击目标有焦点但 Probe 判无光标”；
- 低层 Hook 在 UI 线程/回调里做 UIA 探测容易造成卡顿。

### 4.3 WinEvent Hook

```text
SetWinEventHook(EVENT_OBJECT_SHOW, ...)
```

监听 `TabTip` / `TextInputHost` 窗口显示，立即：

```text
ShowWindow(hwnd, SW_HIDE)
```

问题：

- 能隐藏当前窗口，但 Windows 会重新创建；
- 日志反复出现：
  ```text
  OSK process killed: TabTip
  OSK process killed: TextInputHost
  ```
- `TabTip` 有 Session 0/服务会话进程，普通用户权限无法结束；
- 这不是“阻止创建”，只是“创建后隐藏”。

### 4.4 注册表压制

尝试写入：

```text
HKCU\Software\Microsoft\TabletTip\1.7

EnableDesktopModeAutoInvoke=0
EnableAutoInvoke=0
EnableTouchKeyboardAutoShow=0
DisableTouchKeyboardAutoShow=1
ShowTouchKeyboardOnFocus=0
EnableHandwritingPanel=0
EnableTouchKeyboard=0
TouchKeyboardAutoInvoke=0
AutoShowTouchKeyboard=0

HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced
EnableTouchKeyboard=0
```

问题：

- 某些 Windows 11 版本/输入法不读取这些值；
- RDP 客户端本机键盘不受远程注册表影响；
- 副作用：正常点击输入框时系统触摸键盘不再出现；
- 应用崩溃/退出时如果没有恢复，用户系统键盘会一直不弹。

### 4.5 进程级隐藏/结束

每 300–400ms：

```text
EnumWindows -> ShowWindow(SW_HIDE)
Process.GetProcessesByName("TabTip"/"TextInputHost") -> Kill()
```

问题：

- 会反复重启，形成竞争；
- 部分进程 Access Denied；
- 频繁 EnumWindows/Process 查询造成 UI 卡顿；
- 不能从根本上阻止 OSK 创建。

## 5. 关键日志证据

### contenteditable 被误判

```text
focus type=ControlType.Group name=栖月
class=ProseMirror is-empty ExEditor-qq-msg-editor ProseMirror-focused
value=False readOnly=False text=True focus=True
```

旧逻辑 `OnTouchPenInput: target has no caret`。

### 点击目标与焦点不一致

```text
Mouse/pen click detected: x=737, y=0
OnTouchPenInput: probing 0x270DA2
OnTouchPenInput: target has no caret
AutoFocus changed: ... name=...chat-input-textarea ...
```

### OSK 反复重启

```text
OSK process killed: TabTip (24764)
OSK process killed: TabTip (6792)
OSK process killed: TabTip (19340)
```

### RDP

```text
192.168.2.110:3389 - 远程桌面连接
Input Capture Window class=IHWindowClass
```

## 6. 希望其他 AI 回答的问题

### A. 是否有受支持的“替换 OSK”机制？

1. Windows 11 是否允许第三方注册为系统触摸键盘/手写板提供者？
2. 是否存在 TSF（Text Services Framework）接口可以替换 `TextInputHost` 的 UI，而不是做 IME？
3. `ITfTextInputProcessorEx`、`ITfUIElementSink`、`ITfInputProcessorProfileMgr` 能否控制 OSK 的显示/隐藏？
4. 是否有 `Windows.UI.Text.Core` / `CoreTextServicesManager` 的 API 可以接管触摸键盘？

### B. 如何阻止 OSK 被创建，而不是创建后隐藏？

1. 是否有 `SetWinEventHook(EVENT_OBJECT_CREATE)` 更早的拦截点？
2. 是否可以通过 `AppInit_DLLs` / IFEO / 注入 `TextInputHost` 阻止其窗口创建？是否可靠、是否影响签名/打包应用？
3. 是否可以通过 ETW/Provider 监听并取消 OSK 启动？
4. `TextInputManagementService` 停止/禁用是否是唯一可靠方式？会对 IME、表情面板、触摸输入造成什么影响？
5. 是否存在 per-user / per-app 的策略，而不是全局禁用？

### C. 如何可靠检测“当前点击的是输入框”？

1. Chromium/Electron contenteditable 应该用 UIA 的哪个 Pattern？
2. `ITextProvider2::GetCaretRange` / `ITfContextView::GetTextExt` 是否比 UIA 更可靠？
3. 如何区分“点击输入框”和“切换页面/点击页签”？
4. `AutomationElement.FromPoint` 在跨进程、DPI 缩放、RDP 下是否可靠？需要怎样校准？
5. UWP `Windows.UI.Input.InputSite.WindowClass` 如何稳定找到真实编辑控件？

### D. 如何避免副作用和卡死？

1. Hook 回调里是否应该完全不做 UIA/窗口枚举，改为投递到后台线程？
2. `EnumWindows` + `ShowWindow` 的频率多少安全？
3. 结束 `TabTip`/`TextInputHost` 是否会导致系统不稳定？
4. 自动化关闭时，如何保证所有注册表/Hook/进程状态完全恢复？

### E. RDP 场景

1. 远程会话内能否检测到“OSK 属于 RDP 客户端本机”？
2. 是否有 RDP 客户端策略/注册表可以关闭客户端触摸键盘？
3. 远程应用有没有办法拦截客户端 OSK？

## 7. 当前临时决定

0.1.17 已移除全部自动化替换，只保留：

- 双击笔按钮；
- 有输入光标：关闭当前系统触摸键盘，打开 PenBoard 输入模式；
- 无输入光标：打开批注模式。

完整问题记录：

```text
docs/AUTOMATION-ISSUES.md
```
