# PenBoard 0.1.13

Windows ARM64 触控笔全屏手写 / 屏幕批注工具。

## 本版修复

- **全量注册表拦截**：自动化开启时同时写入以下开关，并在关闭自动化/退出时恢复原值：
  - `EnableDesktopModeAutoInvoke=0`
  - `EnableAutoInvoke=0`
  - `EnableTouchKeyboardAutoShow=0`
  - `DisableTouchKeyboardAutoShow=1`
  - `ShowTouchKeyboardOnFocus=0`
  - `EnableHandwritingPanel=0`
  路径：`HKCU\Software\Microsoft\TabletTip\1.7`
- **进程级兜底**：覆盖层打开时每 300ms 隐藏系统手写板窗口，每 1.5s 结束 `TabTip` / `TextInputHost`，避免系统重新拉起。
- **关闭后不立即重开**：关闭覆盖层后暂停自动化，直到下一次点击输入框；同一输入框再点也能重新打开。
- **保存不退出**：保存 PNG/PDF 后保持批注界面；只有“关闭 → 保存并关闭 → 格式”才关闭。
- **单安装**：MSI 稳定 UpgradeCode + MajorUpgrade；0.1.13 独立发布。

## 注意

- 自动化关闭或退出 PenBoard 时，注册表会恢复原值。
- 如果通过远程桌面（RDP）使用，系统手写板可能在 RDP 客户端侧，PenBoard 只能处理远程会话内的窗口。
- 未做代码签名，SmartScreen 可能提示。

## SHA256

```text
MSI:    385ED48EAC4EC1A7B3463693C95361201BD82F44EBCF75B14DABB70395BD5EF6
便携版: 5D5898F3A59ADF126CCB3CBD1CB093E7901A620F8286BDE0A9A991D905C76AEA
```

源码：https://github.com/qxsraiy/PenBoard
