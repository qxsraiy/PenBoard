# PenBoard 0.1.9

Windows ARM64 触控笔全屏手写 / 屏幕批注工具。

## 本版更新

- **托盘检查更新**：右键最下面“检查更新”，读取 GitHub 最新 Release，对比版本号，下载 ARM64 MSI 并覆盖安装。
- **只允许一个安装**：MSI 保持稳定 UpgradeCode，使用 MajorUpgrade 覆盖旧版本；每次发布使用新版本号，不覆盖旧 Release。
- **顶部只显示模式**：只显示“输入模式”或“批注模式”，不再显示说明小字。
- **底栏切换按钮**：
  - “切换输入”蓝色高亮；
  - “切换批注”红色高亮；
  - 无输入光标时“切换输入”灰色禁用，鼠标悬停提示“未检测到输入框”。
- **保存与关闭分离**：
  - 保存：PNG / PDF / 取消；保存后不关闭，可以继续批注；
  - 关闭：保存并关闭 / 舍弃并关闭 / 取消；
  - 保存并关闭后再选择 PNG / PDF。
- **自动化修复**：记录系统键盘出现前的前台窗口，避免 OSK 抢焦点后无法唤出 PenBoard。
- **安装终端隐藏**：原生 SetupHelper 替代所有 PowerShell 动作。
- **识别稳定性**：识别器和语言切换串行化，修复卡死/无候选字。

## 安装

运行 `PenBoard-0.1.9-arm64.msi`，首屏选择语言。per-user 安装，无需管理员。

安装目录：`%LOCALAPPDATA%\Programs\PenBoard`

## 已知限制

- 未做 Authenticode 签名，SmartScreen 可能提示。
- 只覆盖主屏。
- 管理员权限目标程序可能无法直接注入，会回退剪贴板。
- “自动化替换系统手写板”依赖 Windows 版本和输入法窗口类名，属于实验功能。
- 批注 PDF 是单页图像 PDF。

## SHA256

```text
MSI:    FA9045004F6EB043FE030680A757F463A77CB72CEBBD6D4202D74B0B14EDB9D0
便携版: A36F733047A1845F4BCC59276622D65EFED9D9C3BDAA2A49D13F686855BCF052
```

源码：https://github.com/qxsraiy/PenBoard
