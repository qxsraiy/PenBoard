# PenBoard 0.1.8

Windows ARM64 触控笔全屏手写 / 屏幕批注工具。

## 本版修复

- **安装终端弹窗**：MSI 中的 PowerShell 自定义动作全部使用隐藏窗口，不再弹出一堆终端。
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
MSI:    2AC5A9952304B5F7FE2B26223D51CCA1EE08917A0F32FB833BF4BC6F7A1799FC
便携版: 293FBA1106D1898A3C31ADCEF36D49E8CF9A3ABD53854307C8336B5A8228E20C
```

源码：https://github.com/qxsraiy/PenBoard
