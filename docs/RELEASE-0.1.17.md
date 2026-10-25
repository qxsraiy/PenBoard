# PenBoard 0.1.17

Windows ARM64 触控笔全屏手写 / 屏幕批注工具。

## 本版变更

- **删除自动化替换系统手写板功能**：
  - 不再安装鼠标低层 Hook；
  - 不再安装 WinEvent 显示 Hook；
  - 不再轮询 UIA 焦点；
  - 不再写系统触摸键盘注册表；
  - 不再结束 `TabTip` / `TextInputHost` 进程。
- **恢复系统触摸键盘默认值**：启动时设置
  `EnableDesktopModeAutoInvoke=1`，并删除之前写入的其他开关，修复正常点击输入框时触摸键盘不出现的问题。
- **只保留双击笔按钮检测**：
  - 检测到输入光标：关闭当前系统触摸键盘，打开 PenBoard 输入模式；
  - 没有输入光标：打开批注模式。
- **保留更新安装修复**：下载完成后先退出 PenBoard，延迟 2 秒再启动 MSI。

自动化替换的完整问题记录见：

```text
docs/AUTOMATION-ISSUES.md
```

## 安装

运行 `PenBoard-0.1.17-arm64.msi`，per-user 安装，无需管理员。

## SHA256

```text
MSI:    E4F561B65E5AF4FC40E1ED36E6BA5A9F7262939B24873E50F0DC91B023775A8A
便携版: 65DDAFA436FCF3DC09FAF939684B25CD718A29E9EEA68C15273BBEC7B2497F17
```

源码：https://github.com/qxsraiy/PenBoard
