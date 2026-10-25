# PenBoard 0.2.1

Windows ARM64 触控笔全屏手写 / 屏幕批注工具。

## 本版优化

- 常驻托盘与 WPF 覆盖层分离：
  - 启动时不再创建 MainWindow；
  - 不再创建 InkCanvas、识别器、工具栏视觉树；
  - 只有双击/悬浮按钮才按需创建覆盖层。
- 输入框旁按钮改为轻量 WinForms 实现，WPF 不再常驻。
- 识别器延迟到输入模式创建。
- 系统键盘关闭移到后台线程，避免 UI 线程被窗口枚举/Process.Kill 阻塞。
- 保留托盘、检查更新、双击检测。

## 实测

x64 Debug 构建：

| 状态 | Working Set | Private |
|---|---:|---:|
| 空闲 | 约 65 MB | 约 14 MB |
| 输入覆盖层打开 | 约 208 MB | 约 148 MB |

目标值：

- 空闲 ≤45 MB：**未达到**（WPF 运行时/共享页基线；Private 已降到约 14 MB）；
- 输入 ≤100 MB：**未达到**（WPF 全屏透明覆盖层 + InkCanvas 本身约 150MB 私有内存）。

要进一步达到目标，需要：
1. 把托盘拆成独立的 WinForms 进程，空闲可到 20–30MB；
2. 把覆盖层改为原生 Win32/Direct2D，输入才可能压到 100MB 以下。

本版按约定暂不重构原生覆盖层。

## SHA256

```text
MSI:    F48A12C92D8522F9D8E460D5F0853B984B06BCE991AB85AD982E5DC724D41621
便携版: 9CF8EEB2B1510046DF937BE00CCE7F3D8428BC836A726BB0E061DFF4A7C81313
```

源码：https://github.com/qxsraiy/PenBoard
