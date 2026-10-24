# PenBoard 0.1.6

Windows ARM64 触控笔全屏手写 / 屏幕批注工具。

本版修复 0.1.5 的两个自动化问题：

- **自动化误弹**：不再把资源管理器文件列表等 `ListItem` 的 `ValuePattern` 当成输入框；只有 `Edit`、可编辑 `ComboBox`、`Document`（Word/记事本）才算输入光标。
- **UWP/InputSite 输入框仍进批注模式**：当前台窗口是 `Windows.UI.Input.InputSite.WindowClass` 时，会向上搜索应用窗口中的可编辑控件，再进入输入模式。
- **关闭后又被同一个点击重新打开**：自动化忽略 PenBoard 覆盖层自身上的触控笔/触摸点击。

同时保留 0.1.5 的：

- 单个 MSI，首屏选择中文 / English；
- 中英识别按钮文字 + 蓝色底 + 白色描边；
- 输入模式退格；
- 输入闪烁防护、托盘调试日志；
- 批注关闭保存询问；
- 实验性的系统手写板替换。

## 安装

运行 `PenBoard-0.1.6-arm64.msi`，首屏选择语言。per-user 安装，无需管理员。

安装目录：`%LOCALAPPDATA%\Programs\PenBoard`

## 已知限制

- 未做 Authenticode 签名，SmartScreen 可能提示。
- 只覆盖主屏。
- 管理员权限目标程序可能无法直接注入，会回退剪贴板。
- “自动化替换系统手写板”依赖 Windows 版本和输入法窗口类名，属于实验功能。
- 批注 PDF 是单页图像 PDF。

## SHA256

```text
MSI:    1A6893D348615524093F4A703716B7A8B3D4E96CEC1F0BB6DFDB31C891BF0647
便携版: 30BD6C5F0AC3A4BB8F34969888F6941EF2266A099AF3B745BF5E57082B07C118
```

源码：https://github.com/qxsraiy/PenBoard
