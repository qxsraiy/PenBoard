# PenBoard 0.1.19

Windows ARM64 触控笔全屏手写 / 屏幕批注工具。

## 本版新增

### 方案 A：双击时收起系统键盘

- 双击笔按钮后探测输入光标；
- 有光标：尝试隐藏当前可见的系统触摸键盘，打开 PenBoard 输入模式；
- 无光标：打开批注模式。

只隐藏当前 OSK 窗口，不写注册表、不结束进程、不安装 WinEvent 隐藏 Hook。

### 方案 B：输入框旁悬浮按钮

- 点击输入框后，在光标/点击点旁显示一个无焦点、置顶的
  “PenBoard 手写” 小按钮；
- 按钮不抢焦点，不自动打开全屏层；
- 用户点击按钮后才打开 PenBoard；
- 点击空白处会隐藏按钮；
- 系统键盘保持正常，不与其争生命周期。

托盘右键新增开关：

```text
输入框旁显示 PenBoard 按钮
```

默认开启。

## 保留

- 0.1.18 的更新安装修复：更新包移到 `%LOCALAPPDATA%\PenBoardUpdates`，
  等待旧进程退出后再启动 MSI；
- 0.1.17 的移除自动化替换决定：不写注册表、不结束 OSK 进程、不做全局 Hook。

## SHA256

```text
MSI:    5C1664E11420705FC98BED5D846B88BF03B65FA91CFEDCCC42B7E07F4BAE05D6
便携版: 8AE2C1BC3FE16848AA4FEA617BA4653A5A43CC6C3B9FEC0A0C0B40253913CBAF
```

源码：https://github.com/qxsraiy/PenBoard
