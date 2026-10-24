# PenBoard 0.1.14

Windows ARM64 触控笔全屏手写 / 屏幕批注工具。

## 本版修复

- **持续隐藏系统手写板**：自动化开启后，即使 PenBoard 覆盖层关闭，也每 400ms 隐藏系统手写板，每 1.5s 结束 `TabTip` / `TextInputHost`，防止关闭后系统键盘重新出现。
- **扩展注册表**：同时抑制 10 个已知开关，覆盖 `HKCU\Software\Microsoft\TabletTip\1.7` 和
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced`，关闭自动化/退出时恢复。
- **关闭后暂停自动打开**：下一次点击输入框才重新进入 PenBoard，避免关闭后被系统键盘重新拉起。
- **单按钮切换、保存不退出、更新进度条**继续保留。

## 重要限制

Windows 没有公开 API 可以“替换”系统虚拟键盘；系统键盘是独立受保护进程。PenBoard 能做的是：

1. 关闭系统自动唤出；
2. 隐藏/结束远程会话内的系统键盘进程；
3. 用 PenBoard 自己的全屏手写层替代输入。

如果系统手写板来自 **RDP 客户端本机**（不是远程会话内），远程运行的 PenBoard 无法关闭客户端本机的键盘，需要在本机运行 PenBoard 或关闭 RDP 客户端触摸键盘。

## SHA256

```text
MSI:    926D9687DBCEC42EB701BB0151AE56AC5E022F2A39CB46AE497DCC057D8BAD83
便携版: 7BE9DE8FEDE468046726E6DF16997F8B714EDF63DB33510DE4489FE2EFAEDB40
```

源码：https://github.com/qxsraiy/PenBoard
