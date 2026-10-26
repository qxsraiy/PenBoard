# PenBoard 0.3.2

Windows x64 / ARM64 触控笔手写工具。

## 本版修复

- 原生文字模式支持候选字点击：
  - 识别后显示候选行；
  - 点击候选可注入对应文字；
  - 仍保留候选 0 自动注入。
- 原生批注工具栏按钮已修通：
  - `Save PNG`
  - `Save PDF`
  - `Close`
- 快捷键：
  - `Esc` 关闭；
  - `S` 保存 PNG；
  - `P` 保存 PDF。
- 保留 C# 托盘按需启动原生覆盖层。
- x64 / ARM64 MSI 和便携版。

## 已知限制

- 原生覆盖层 + C# 托盘合计内存约 100MB；
- ARM64 原生 exe 已打包，但本机只验证了 x64；
- 候选点击还未做自动化回归测试。

## SHA256

```text
ARM64 MSI: E8190CD4A76D24431D7FA4589C337844F183C7CF292A2E34510DD387B844B4AE
ARM64 ZIP: F3BD9D913B500B9478356EE75BD8F1921C17F43573D72136392DACA5838DA5FD
x64 MSI:   DE8AFDD2E175D7986C534F6DF3949B1F2E921ECAA985CCC2F6FA02D7195FCD79
x64 ZIP:   0F1331C916DB7344AB5F8E5DCE37F9EB5F21916E115249A442898ED2BF60BA96
```

源码：https://github.com/qxsraiy/PenBoard
