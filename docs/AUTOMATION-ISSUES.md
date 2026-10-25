# PenBoard 自动化替换系统手写板：问题记录

## 目标

Windows ARM64 设备上，用全屏 PenBoard 手写层替代系统触摸键盘/手写板：

- 有输入光标：双击笔按钮，关闭系统触摸键盘，打开 PenBoard 输入模式；
- 无输入光标：双击笔按钮，打开批注模式。

## 实际尝试过的方案

1. **UIA 焦点检测**：`AutomationElement.FocusedElement`、`GetGUIThreadInfo`、`ControlType.Edit/ComboBox/Document`。
2. **低层鼠标 Hook**：`WH_MOUSE_LL` 捕获笔/鼠标点击。
3. **UIA 焦点变化轮询**：300ms 检查焦点变化。
4. **WinEvent Hook**：`SetWinEventHook(EVENT_OBJECT_SHOW)` 监听 `TabTip`/`TextInputHost` 窗口显示并隐藏。
5. **注册表压制**：
   - `HKCU\Software\Microsoft\TabletTip\1.7`
   - `EnableDesktopModeAutoInvoke=0`
   - `EnableAutoInvoke=0`
   - `EnableTouchKeyboardAutoShow=0`
   - `DisableTouchKeyboardAutoShow=1`
   - `ShowTouchKeyboardOnFocus=0`
   - `EnableHandwritingPanel=0`
   - 以及 `EnableTouchKeyboard`、`TouchKeyboardAutoInvoke`、`AutoShowTouchKeyboard`
   - `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\EnableTouchKeyboard=0`
6. **进程级处理**：每 300/400ms 隐藏 OSK 窗口，每 1.5s 结束 `TabTip`/`TextInputHost`。
7. **点击点 UIA 检测**：`AutomationElement.FromPoint` + 祖先遍历，识别 contenteditable。

## 日志中确认的问题

### 1. contenteditable 被误判为“没有光标”

日志示例：

```text
focus type=ControlType.Group name=栖月
class=ProseMirror is-empty ExEditor-qq-msg-editor ProseMirror-focused
value=False readOnly=False text=True focus=True
```

QQ、Teams、Discord、网页编辑器使用 `ControlType.Group/Pane + TextPattern`，不是 `Edit`。
旧逻辑只接受 `Edit/ComboBox/Document`，所以 `OnTouchPenInput: target has no caret`，PenBoard 不弹。

后续已加入 ProseMirror/Editor/CodeMirror 等类名判断，能识别一部分 contenteditable。

### 2. 点击目标和焦点目标不一致

日志示例：

```text
Mouse/pen click detected: x=737, y=0
OnTouchPenInput: probing 0x270DA2
OnTouchPenInput: target has no caret
AutoFocus changed: key=...ControlType.Edit name=...chat-input-textarea...
```

用户点的是页面，但 `FocusedElement` 仍是上一个输入框；或者反过来，焦点在输入框但点击落在父窗口。
`FromPoint` 可以改善，但 Chromium/Electron 的 UIA 树在不同应用里差异很大，仍会漏判。

### 3. 焦点轮询导致切换页面自动弹 PenBoard

日志示例：

```text
AutoFocus changed: key=...ControlType.ComboBox name=搜索...
OpenOverlay begin...
```

只要页面切换后焦点进入地址栏/搜索框，PenBoard 就自动打开，用户没有点击输入框。
这是 0.1.15/0.1.16 体验问题的主要原因。

### 4. 系统 OSK 被结束后反复重启

日志示例：

```text
OSK process killed: TabTip
OSK process killed: TextInputHost
...
OSK process killed: TabTip
```

`TextInputHost`/`TabTip` 是受系统保护的独立进程；结束进程后 Windows 会重新拉起。  
Hook 隐藏窗口也只能隐藏当前窗口，不能阻止系统再次创建。

### 5. 正常触摸键盘被误伤

注册表值 `EnableDesktopModeAutoInvoke=0` 等在没有自动化时也可能残留，导致用户正常点击输入框时系统触摸键盘不出现。
0.1.15 的 WinEvent Hook 在自动化关闭时仍安装，虽然后来加了 `Enabled` 判断，但用户已经遇到系统键盘消失。

### 6. RDP 场景

日志中出现：

```text
192.168.2.110:3389 - 远程桌面连接
Input Capture Window class=IHWindowClass
```

如果系统手写板是 RDP 客户端本机弹出的，它不在远程会话内，远程 PenBoard 的 Hook/注册表/进程结束都无法关闭客户端键盘。

## 结论

- Windows 没有公开 API 可以“替换”系统虚拟键盘；只能隐藏/禁用/绕过。
- OSK 是受保护的系统进程，进程结束后会被重新拉起。
- Chromium/Electron contenteditable 的 UIA 信息不稳定，点击目标与焦点目标经常不一致。
- 焦点轮询会过度触发，影响正常页面切换。
- 注册表压制会影响用户正常使用系统键盘。

## 0.1.17 的决定

移除所有自动化替换逻辑：

- 删除托盘“自动化：替换系统手写板”；
- 不再安装鼠标/WinEvent Hook；
- 不再轮询焦点；
- 不再写注册表；
- 启动时恢复系统触摸键盘默认值；
- 只保留双击笔按钮：
  - 有输入光标：关闭当前系统触摸键盘，打开 PenBoard 输入模式；
  - 无输入光标：打开批注模式。

这样不会再出现“切换页面自动弹”“系统键盘被永久隐藏”“后台卡死”等问题。
