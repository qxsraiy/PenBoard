# PenBoard 0.1.8

Windows ARM64 触控笔全屏手写 / 屏幕批注工具。

## 本版修复

- **安装终端弹窗**：MSI 改用原生 SetupHelper，不再启动 PowerShell，也不会弹出一堆终端。
- **批注保存格式**：关闭批注或点“保存”时弹出选择，只保存 `PDF` 或 `PNG` 其中一种，不再同时保存。
- **安装后启动**：完成页增加“启动 PenBoard / Launch PenBoard”复选框，默认勾选，安装完成后后台启动托盘程序。
- **语言按钮颜色**：中文红色高亮，English 蓝色高亮；两种状态都明确亮起。
- **自动化恢复**：系统手写板“可见性检测”和“隐藏冷却”分离，修复 0.1.6 后期不触发的问题。
- **卡死/无候选字**：识别和语言切换串行化，识别器异常后自动重建。
- **覆盖安装**：安装 UI 在 `InstallValidate` 前结束正在运行的 PenBoard，避免文件占用报错。

## 安装

运行 `PenBoard-0.1.8-arm64.msi`，首屏选择语言。per-user 安装，无需管理员。

安装目录：`%LOCALAPPDATA%\Programs\PenBoard`

## 已知限制

- 未做 Authenticode 签名，SmartScreen 可能提示。
- 只覆盖主屏。
- 管理员权限目标程序可能无法直接注入，会回退剪贴板。
- “自动化替换系统手写板”依赖 Windows 版本和输入法窗口类名，属于实验功能。
- 批注 PDF 是单页图像 PDF。

## SHA256

```text
MSI:    2404D05231C9B95802CD838769966BC3CA76FC4F22AE7235036EA66E80DFA34C
便携版: 3F48BB4700D6E4D10FC231AE7FFD058E792C61E8DDC38C6005FBDC141F73BC61
```

源码：https://github.com/qxsraiy/PenBoard
