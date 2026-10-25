# PenBoard 0.2.0

Windows ARM64 触控笔全屏手写 / 屏幕批注工具。

## 版本规则

版本号每段只允许 0–9：

```text
0.2.0 ... 0.2.9 -> 0.3.0
```

更新比较按 major → minor → build 逐位比较。

## 本版修复

- **首次更新只退出不安装**：不再生成并调用批处理，直接启动
  `msiexec /i "...msi" /passive`，避免 cmd 引号/等待逻辑导致安装器未启动。
  MSI 日志仍写到 `%TEMP%\PenBoard-update.log`。
- **输入框旁按钮不出现**：除了低层鼠标 Hook，新增 700ms 焦点轮询兜底；
  输入框获得焦点时也会显示“PenBoard 手写”悬浮按钮。
- **双击收起系统键盘无效**：双击或点悬浮按钮时，先隐藏系统键盘窗口，
  再结束当前 `TabTip`/`TextInputHost` 进程。只在这两个动作执行，不做全局压制。
- 不写注册表、不做全局 OSK Hook、不全局杀进程；系统键盘正常保留。

## SHA256

```text
MSI:    3349B176061337D1AF9B977AE61243529B69D7784F1C982B40743619A740DD27
便携版: 46855156E7672C8D522A32F5010AC8495F064A292B4DBA04F3DBBF85A3F3869E
```

源码：https://github.com/qxsraiy/PenBoard
