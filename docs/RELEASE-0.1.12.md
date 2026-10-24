# PenBoard 0.1.12

Windows ARM64 触控笔全屏手写 / 屏幕批注工具。

## 本版修复

- **系统手写板自动拦截**：
  - 自动化开启时设置
    `HKCU\Software\Microsoft\TabletTip\1.7\EnableDesktopModeAutoInvoke=0`，
    阻止 Windows 自动唤出触摸键盘/手写板；
  - 关闭自动化或退出 PenBoard 时恢复原值；
  - 覆盖层打开期间每 300ms 强制隐藏 TabTip/TextInputHost 并重新置顶。
- **同一输入框可重复打开**：关闭输入模式后重置自动化触发状态，再点同一个输入框也能重新打开 PenBoard，不需要先切换输入框。
- **单按钮切换**：底栏“切换输入/切换批注”一个按钮，位置在“细/中/粗”右侧。
- **保存不退出**：保存 PNG/PDF 后覆盖层保持，只有“关闭 → 保存并关闭 → 格式”才关闭。
- **弹框空白**：保存/关闭弹框宽度按内容收缩。
- **更新进度**：检查更新显示下载进度条。
- **单安装**：MSI 稳定 UpgradeCode + MajorUpgrade；0.1.12 独立发布，不覆盖旧版。

## 安装

运行 `PenBoard-0.1.12-arm64.msi`，首屏选择语言。per-user 安装，无需管理员。

## SHA256

```text
MSI:    3E83A0872D92698E3ED2CBFCB1554D70C32F7F9830419721E59B0479193F7AAA
便携版: B14F9C831DF847F6F050EE38E981E60C67E673062F05A2E37A0CA3E3CEF9D5D1
```

源码：https://github.com/qxsraiy/PenBoard
