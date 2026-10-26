# PenBoard 0.3.0

Windows x64 / ARM64 触控笔手写工具。

## 本版重点

- 原生覆盖层启动：C# 托盘按需启动 `PenBoard.DcompOverlay.exe`，不再默认创建 WPF MainWindow。
- 原生覆盖层使用 DirectComposition + D3D11 + Direct2D。
- 原生文字模式：
  - 鼠标/笔迹；
  - Windows Ink 识别；
  - 自动注入候选 0；
  - 退格、撤销、清空、中英切换；
  - `Esc` 强制退出。
- 原生批注模式：
  - 启动时截屏背景；
  - 笔迹叠加；
  - `S` 保存 PNG；
  - `P` 保存 PDF；
  - `Esc` 退出。
- 双架构：
  - x64 安装包/便携版；
  - ARM64 安装包/便携版。

## 已知问题

- 原生批注工具栏按钮点击不稳定，当前用 `S` / `P` 快捷键保存。
- 原生工具栏候选点击还未完成，当前自动注入候选 0。
- C# 托盘 + 原生进程总内存约 100MB 左右，尚未完全达到空闲 45MB/输入 100MB 的目标。
- 原生覆盖层仍是新代码，建议先备份 0.2.1。

## SHA256

```text
ARM64 MSI: 105F36FAAF217666AB7329289F9AC4A17082DF18570E57755D27C7FB463AF522
ARM64 ZIP: 601F9135943819C1EE5C0B02FB7F55ACFD1E0E593A723A32756676ACE965051F
x64 MSI:   CF676B94B454E39D3CF037BDA154A9D30FB35135DECB3969D1B10BFC55B82CE7
x64 ZIP:   FA2003806B2942E3192DACA6931110793D8CFF132FE56E67379C7CE838EDEA67
```

源码：https://github.com/qxsraiy/PenBoard
