# PenBoard

Windows ARM64 触控笔全屏手写 / 屏幕批注工具。

- 有输入光标：双击笔按钮打开全屏手写，调用 Windows Ink 识别，把文字送入当前输入框。
- 无输入光标：冻结当前屏幕，手写批注；关闭时选择保存 PDF、保存 PNG、直接关闭或取消。
- 中文 / English 手写识别切换，中文全角标点、英文半角标点。
- 退格删除已经插入的文字。
- 支持涂抹擦、整笔擦、撤销、颜色、粗细。
- 托盘调试日志、实验性的系统手写板替换。

当前版本：**v0.1.8**  
发布页：https://github.com/qxsraiy/PenBoard/releases

> 这是未签名构建。SmartScreen 仍可能提示；代码签名证书需要企业/个人主体注册审核，本轮未做。

## 0.1.8 修复

- 安装器 PowerShell 动作全部隐藏窗口，不再弹出多个终端。
- 批注保存改为“保存 PDF”或“保存 PNG”，一次只保存一种。
- 安装完成页增加“启动 PenBoard / Launch PenBoard”复选框，默认勾选，后台启动。
- 中文语言按钮红色高亮，English 蓝色高亮。
- 修复 0.1.6 后期自动化不触发：系统手写板可见性检测与隐藏冷却分离。
- 识别器和语言切换串行化，修复卡死/无候选字。

## 0.1.7 修复

- 自动化只把 `Edit`、可编辑 `ComboBox`、`Document`（Word/记事本）当作输入框；资源管理器文件列表等 `ListItem` 不再误触发。
- UWP `Windows.UI.Input.InputSite.WindowClass` 前台窗口会向上查找应用窗口中的可编辑控件，修复“已经点进输入框却仍然进入批注模式”。
- PenBoard 覆盖层上的触控笔/触摸点击会被自动化忽略，避免关闭后又被同一个点击重新打开。

## 安装

### MSI（推荐）

下载 `PenBoard-0.1.8-arm64.msi`，运行后在首屏选择“中文”或“English”。

- per-user 安装，不需要管理员权限；
- 安装目录：`%LOCALAPPDATA%\Programs\PenBoard`；
- 创建桌面/开始菜单/开机启动快捷方式；
- 卸载时删除安装目录、快捷方式、`settings.json` 和日志；
- 保留 `图片\PenBoard`。

### 便携版

下载 `PenBoard-arm64-v0.1.8-portable.zip`，解压后运行 `install.cmd`。  
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

wix build -b installer installer/PenBoard.wxs -o artifacts/PenBoard-0.1.8-arm64.msi
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
