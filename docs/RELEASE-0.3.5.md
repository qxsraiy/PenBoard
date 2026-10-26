# PenBoard 0.3.5

Windows x64 / ARM64 触控笔手写工具。

## 本版修复

- 修复批注模式被当成输入模式：
  - `--annotation` 启动时现在会关闭文字识别；
  - 批注模式不再自动注入候选字。
- 修复书写时凭空出现的一笔：
  - 覆盖层打开后的前 300ms 忽略输入；
  - 避免悬浮按钮点击/抬起被当成笔迹起点。
- 恢复顶部模式栏：
  - `输入模式` / `批注模式`。
- 恢复底部工具组：
  - 笔、擦；
  - 黑/红/蓝；
  - 细/中/粗；
  - 原有识别、语言、退格、撤销、清空、关闭。
- 保留 ARM64 `WM_POINTER*` 按钮和识别修复。

## 已知限制

- 擦除工具目前只切换了按钮状态，擦除逻辑还未完成；
- 候选点击和批注保存仍无自动化回归测试；
- 合计内存仍约 100MB。

## SHA256

```text
ARM64 MSI: 7A15A0A72AA1DB102D1B665BC45791DB8FFEA9173A3D13AEEF8E0C92ADF3950C
ARM64 ZIP: D093BF0CD3D11F2378C8FE3197BB2DBA14A450AD4AEBB42D6BA385E83826FA6F
x64 MSI:   8E382D4FD803DF717F15D3ADA46E87181954889D0EDB0AB3F8306648EB15C4BD
x64 ZIP:   06B10D2A4ED9D6D84988A0859EF8F4DA4876647C05FD70B9A2FA12703F3E055F
```

源码：https://github.com/qxsraiy/PenBoard
