# PenBoard 0.1.15

Windows ARM64 触控笔全屏手写 / 屏幕批注工具。

## 本版修复

- **HOOK 系统手写板**：使用 `SetWinEventHook(EVENT_OBJECT_SHOW)` 监听窗口显示事件；`TabTip` / `TextInputHost` 窗口一出现就立即 `ShowWindow(SW_HIDE)`，不再只靠 300/400ms 轮询。日志会出现：
  ```text
  TouchKeyboardInterceptor hook=True winEvent=True
  WinEvent OSK show hooked: process=TextInputHost
  ```
- **contenteditable 输入框**：QQ、Teams、Discord、CodeMirror、ProseMirror、Monaco 等 Chromium/Electron 编辑器通常暴露 `ControlType.Group/Pane/Custom` + TextPattern，而不是 Edit。现在这些也会被识别为输入框，点击后打开 PenBoard。
- 保留 10 项注册表压制、持续隐藏和进程结束作为兜底。
- 保存不退出、单按钮模式切换、更新进度条继续保留。

## 重要说明

Windows 没有公开的“替换系统虚拟键盘”API。0.1.15 做的是在窗口显示事件层面 **hook 并隐藏** 系统键盘，同时用 PenBoard 接管输入；不是修改 Windows 系统组件。

如果系统键盘来自 RDP 客户端本机，远程会话内的 hook 无法关闭客户端键盘。

## SHA256

```text
MSI:    7881E33258AF893A26414B3F5A75A84C5F5018CFC05C8116EE8C9A952B66CE57
便携版: 91D195D76AF9FEBFB58CC8CA4409506DB5472BFC16D683ED4CF3E7CE14C2A2DE
```

源码：https://github.com/qxsraiy/PenBoard
