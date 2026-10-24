# PenBoard 0.1.5

Windows ARM64 触控笔全屏手写 / 屏幕批注工具。

## 本版重点

- **单个 MSI + 首屏语言选择**：安装开始时选择中文或 English，欢迎页/完成页跟随选择。
- **中文 / English 识别按钮**：文字直接显示当前语言，同时用蓝色底 + 白色描边高亮；只在输入模式显示。
- **退格**：删除输入框里已经识别并插入的文字；只在输入模式显示。
- **输入模式闪烁修复**：打开期间忽略重复 TOGGLE，700ms 去重，500ms 最小显示时间。
- **托盘调试日志**：右键托盘图标开启，复现后打开日志目录即可取 `penboard.log`。
- **批注关闭询问**：保存 PNG + PDF / 不保存关闭 / 取消。
- **自动化替换系统手写板**（实验）：触控笔点击输入框或系统手写板出现时，尝试隐藏 TabTip/TextInputHost 并打开全屏输入。
- 保留开机启动；卸载删除 settings.json 和 logs，保留 `图片\PenBoard`。

## 安装

运行 `PenBoard-0.1.5-arm64.msi`，首屏选择语言。per-user 安装，无需管理员。

安装目录：`%LOCALAPPDATA%\Programs\PenBoard`

## 已知限制

- 未做 Authenticode 签名，SmartScreen 可能提示。
- 只覆盖主屏。
- 管理员权限目标程序可能无法直接注入，会回退剪贴板。
- “自动化替换系统手写板”依赖 Windows 版本和输入法窗口类名，属于实验功能。
- 批注 PDF 是单页图像 PDF。

## SHA256

```text
MSI:    464EAB5D11C3415F8E73B63DE001E3B5D13A7FA2091C37C0B4E3FE35C19C7F4D
便携版: 236C9D6754647EF92F6544B25D07C135EDAC2A0BE3DE5FFA51BBC364AC92B70B
```

源码：https://github.com/qxsraiy/PenBoard
