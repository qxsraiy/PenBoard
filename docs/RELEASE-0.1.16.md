# PenBoard 0.1.16

Windows ARM64 触控笔全屏手写 / 屏幕批注工具。

## 本版修复

- **内置更新首次安装失败**：下载完成后先退出 PenBoard，延迟 2 秒再启动
  `msiexec /i ... /passive`，不再出现“第一次退出但没装成功、第二次才成功”。
- **切换页面误弹**：焦点变化只记录，不再自动打开 PenBoard；只有点击输入框才触发。
- **点击位置更准确**：先用 UIA `AutomationElement.FromPoint` 判断鼠标实际点到的控件，
  再回退到焦点元素。点页签、空白处不会被地址栏/搜索框误判。
- **关闭后同一输入框可重开**：关闭后重置触发状态，再点同一个输入框也重新打开。
- **自动化关闭时不隐藏系统手写板**：WinEvent hook、注册表和定时器都受自动化开关控制。
- 保留 0.1.15 的 OSK 显示 Hook、10 项注册表压制和 contenteditable 输入框支持。

## 注意

系统虚拟键盘是独立系统进程，PenBoard 做的是 hook/隐藏并用自身手写层接管；如果系统键盘来自 RDP 客户端本机，远程会话内的 hook 无法关闭客户端键盘。

## SHA256

```text
MSI:    D5CB1D440B5F27FBEF3E0B5606D94F3AC04F628675BB882C4E4C730B13B5E3E9
便携版: 8790F86AF55E2542A6795BCC387079719BE7F0B1BEC07C1000B4CDF93108E144
```

源码：https://github.com/qxsraiy/PenBoard
