# PenBoard

Windows ARM64 触控笔全屏手写 / 屏幕批注工具。

- 有输入光标：双击笔按钮打开全屏手写，调用 Windows Ink 识别，把文字送入当前输入框。
- 无输入光标：冻结当前屏幕，手写批注；关闭时选择保存 PDF、保存 PNG、直接关闭或取消。
- 中文 / English 手写识别切换，中文全角标点、英文半角标点。
- 退格删除已经插入的文字。
- 支持涂抹擦、整笔擦、撤销、颜色、粗细。
- 托盘调试日志、实验性的系统手写板替换。

当前版本：**v0.1.14**  
发布页：https://github.com/qxsraiy/PenBoard/releases

> 这是未签名构建。SmartScreen 仍可能提示；代码签名证书需要企业/个人主体注册审核，本轮未做。

## 0.1.14 修复

- 自动化开启后即使覆盖层关闭，也持续每 400ms 隐藏系统手写板并每 1.5s 结束 TabTip/TextInputHost。
- 注册表抑制扩展到 10 个开关（TabletTip\1.7 + Explorer\Advanced）。
- 关闭后暂停自动打开，直到下一次点击输入框。

## 0.1.13 修复

- 全量注册表抑制系统手写板：同时写入 6 个自动唤出开关，退出/关闭自动化时恢复。
- 覆盖层打开时每 300ms 隐藏系统手写板，并每 1.5s 结束 TabTip/TextInputHost。
- 关闭覆盖层后暂停自动化，直到下一次点击输入框，避免立即重开。
- 同一输入框关闭后再点也能重新打开。

## 0.1.12 修复

- 自动化开启时关闭 Windows 触摸键盘自动唤出（`EnableDesktopModeAutoInvoke=0`），关闭自动化或退出时恢复。
- 覆盖层打开时每 300ms 强制隐藏系统手写板并重新置顶。
- 关闭输入模式后重置触发，再点同一个输入框也能重新打开。

## 0.1.11 修复

- “切换输入/批注”合并为一个按钮，放在“细/中/粗”右侧。
- 保存/关闭弹框去掉右侧空白。
- 检查更新显示下载进度条。
- 所有左键点击都触发输入框探测，自动化不再依赖触控笔事件。
- 覆盖层每 800ms 重新置顶并隐藏系统手写板，避免遮挡底栏。
- 保存后保持批注界面。

## 0.1.10 修复

- 修复“保存后退出”：保存 PNG/PDF 后保持覆盖层，可以继续批注；只有“关闭 → 保存并关闭 → 选择格式”才关闭。
- 自动化新增 UI 焦点变化检测，系统手写板抢焦点或触控笔事件不回调时也能自动打开 PenBoard。
- 版本号递增到 0.1.14，不覆盖 0.1.9 Release。

## 0.1.9 修复

- 托盘右键最下面新增“检查更新”：读取 GitHub 最新 Release，比较版本号，下载 ARM64 MSI 并覆盖安装。
- 顶部只显示“输入模式”/“批注模式”；底栏改为“切换输入”（蓝色）和“切换批注”（红色）。
- 没有输入光标时，“切换输入”灰色禁用，悬停提示“未检测到输入框”。
- “保存”只保存当前状态，不关闭，保存后继续写；“关闭”才走“保存并关闭 / 舍弃并关闭 / 取消”，保存并关闭后再选 PNG/PDF。
- 自动化保存系统键盘出现前的前台窗口作为目标，避免 OSK 抢焦点后无法唤出 PenBoard。
- 安装器改用原生 SetupHelper，彻底隐藏 PowerShell/终端窗口。
- 每次发布递增版本号；MSI 使用稳定 UpgradeCode + MajorUpgrade，设备上只保留一个产品。

## 0.1.8 修复

- 原生 SetupHelper 替代 PowerShell 安装动作。
- 批注保存选择 PDF 或 PNG。
- 安装完成页默认勾选启动。
- 中文红色、English 蓝色高亮。
- 系统手写板检测与隐藏冷却分离；识别器串行化。

## 0.1.7 修复

- 自动化只把 `Edit`、可编辑 `ComboBox`、`Document`（Word/记事本）当作输入框；资源管理器文件列表等 `ListItem` 不再误触发。
- UWP `Windows.UI.Input.InputSite.WindowClass` 前台窗口会向上查找应用窗口中的可编辑控件，修复“已经点进输入框却仍然进入批注模式”。
- PenBoard 覆盖层上的触控笔/触摸点击会被自动化忽略，避免关闭后又被同一个点击重新打开。

## 安装

### MSI（推荐）

下载 `PenBoard-0.1.14-arm64.msi`，运行后在首屏选择“中文”或“English”。

- per-user 安装，不需要管理员权限；
- 安装目录：`%LOCALAPPDATA%\Programs\PenBoard`；
- 创建桌面/开始菜单/开机启动快捷方式；
- 卸载时删除安装目录、快捷方式、`settings.json` 和日志；
- 保留 `图片\PenBoard`。

### 便携版

下载 `PenBoard-arm64-v0.1.14-portable.zip`，解压后运行 `install.cmd`。  
便携版安装到 `%LOCALAPPDATA%\PenBoard`，卸载时运行 `uninstall.cmd`。

## 设置笔双击

Windows 11：设置 → 蓝牙和其他设备 → 笔和 Windows Ink → 快捷按钮 / 双击 → 打开应用 → 选择：

```text
%LOCALAPPDATA%\Programs\PenBoard\PenBoard.exe
```

也可以直接选择桌面 `PenBoard.lnk`。不依赖笔设置时，可用 `Ctrl+Alt+P` 测试。

## 构建

需要 .NET 8 SDK、WiX 5。

```powershell
dotnet publish src/PenBoard.App/PenBoard.App.csproj `
  -c Release -r win-arm64 --self-contained true `
  -p:PublishSingleFile=false -o artifacts/arm64/app

powershell -ExecutionPolicy Bypass -File installer/build-helper.ps1

wix build -b installer installer/PenBoard.wxs -o artifacts/PenBoard-0.1.14-arm64.msi
```

MSI 是 per-user、x86 平台 Windows Installer 包，内含 ARM64 payload；在 ARM64 Windows 上安装不受平台限制。

## 目录

```text
src/PenBoard.App/       WPF 主程序
installer/PenBoard.wxs  单 MSI + 中英文语言选择 UI
installer/app.ico       MSI/快捷方式图标
docs/                   计划、MSI/签名说明
```

## 已知限制

- 只覆盖主屏。
- 目标程序以管理员权限运行时，普通权限 PenBoard 无法直接注入文字，会回退剪贴板。
- “自动化：替换系统手写板”是实验功能，依赖 TabTip/TextInputHost 窗口类名。
- 批注 PDF 是单页图像 PDF。
- 未签名；SmartScreen 提示需要代码签名证书才能彻底解决。

## 签名

签名步骤和 OV 证书材料见 [docs/MSI-SIGNING.md](docs/MSI-SIGNING.md)。  
拿到证书后对 `PenBoard.exe`、`PenBoard.dll` 和 MSI 做 Authenticode 签名并加时间戳。

## 许可

暂未指定开源许可证；仓库当前为公开源码，发布前请补 LICENSE。
