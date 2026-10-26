# PenBoard 0.3.1

Windows x64 / ARM64 触控笔手写工具。

## 本版修复

- 原生批注工具栏按钮已修通：
  - `Save PNG` 点击保存；
  - `Save PDF` 点击保存；
  - `Close` 关闭。
- 原生快捷键：
  - `Esc` 强制关闭；
  - `S` 保存 PNG；
  - `P` 保存 PDF。
- PNG 保存已验证，文件正常生成。
- PDF 保存已验证，文件正常生成。
- 保留原生文字模式：
  - 笔迹；
  - Windows Ink 识别；
  - 自动注入候选 0；
  - 退格/撤销/清空/中英切换。
- 保留 C# 托盘按需启动原生覆盖层。
- 提供 x64 / ARM64 安装包和便携版。

## 已知限制

- 候选字点击选择尚未完成；
- 原生工具栏与 C# 托盘合计内存约 100MB，未完全达到 45MB 空闲目标；
- ARM64 原生 exe 已打包，但本机只验证了 x64。

## SHA256

```text
ARM64 MSI: D295902603F69FF846213DB1E3C83DFC48BB08CA39953F71127BAA03833A5FB5
ARM64 ZIP: 574F880268C6AEDD92302CBE6752F2582C58E8C3CA32187E22140CF5ECCD4A2D
x64 MSI:   EBE691BA3F8A8A4034A7299ECCF05CC1D9921C6E8169857928672F6452898A06
x64 ZIP:   C4F1718DFEDE82C579ACB1AF69EB66373377BBBE4F02E468C03F4F9654BFEDB8
```

源码：https://github.com/qxsraiy/PenBoard
