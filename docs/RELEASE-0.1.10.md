# PenBoard 0.1.10

Windows ARM64 触控笔全屏手写 / 屏幕批注工具。

## 本版修复

- **保存不再退出**：
  - 点“保存”选择 PNG/PDF 后只保存当前状态，覆盖层保持打开，可以继续批注；
  - 取消保存同样保持打开；
  - 只有“关闭 → 保存并关闭 → 选择 PNG/PDF”才会保存后关闭。
- **自动化焦点检测**：
  - 保留触控笔/系统手写板监听；
  - 新增 UI Automation 焦点变化检测，即使系统手写板抢走前台焦点或触控笔事件没有回调，进入可编辑输入框时也会自动打开 PenBoard；
  - 打开前后强制隐藏系统手写板。
- **检查更新**：托盘右键最下面“检查更新”，读取 GitHub 最新 Release，比较版本号，下载 ARM64 MSI 并覆盖安装。
- **单安装/版本递增**：MSI 使用稳定 UpgradeCode + MajorUpgrade；0.1.10 是独立版本，不覆盖 0.1.9 Release。
- **安装无终端**：原生 SetupHelper 替代 PowerShell 自定义动作。
- **模式切换**：顶部只显示输入/批注模式；底栏“切换输入”蓝色、“切换批注”红色；无输入光标时切换输入灰色禁用并提示“未检测到输入框”。

## 安装

运行 `PenBoard-0.1.10-arm64.msi`，首屏选择语言。per-user 安装，无需管理员。

安装目录：`%LOCALAPPDATA%\Programs\PenBoard`

## 已知限制

- 未做 Authenticode 签名，SmartScreen 可能提示。
- 只覆盖主屏。
- 管理员权限目标程序可能无法直接注入，会回退剪贴板。
- 批注 PDF 是单页图像 PDF。

## SHA256

```text
MSI:    87566AB02437B47809DC029838DF921C1291079AE8177EDB17E574101FE2BD6A
便携版: 5B35B8C48942D2703A91EB35BF3EBD2353315FAF1FC3A7CD4CA516A41A97F52D
```

源码：https://github.com/qxsraiy/PenBoard
