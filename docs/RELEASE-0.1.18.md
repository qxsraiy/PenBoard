# PenBoard 0.1.18

Windows ARM64 触控笔全屏手写 / 屏幕批注工具。

## 本版修复

- **首次更新安装失败**：
  - 原因之一：更新包原来放在 `%LOCALAPPDATA%\PenBoard\updates`，而 MSI 的
    `CleanupOldZipInstall` 在 `InstallFiles` 之前会删除整个
    `%LOCALAPPDATA%\PenBoard`，把正在安装的 MSI 自身删掉；
  - 现在更新包放到 `%LOCALAPPDATA%\PenBoardUpdates`。
- **等待进程退出**：更新下载完成后生成一个批处理启动器，循环等待当前 PenBoard
  进程退出，再执行 `msiexec /i ... /passive`；不再用固定 2 秒延迟。
- **安装日志**：MSI 日志写到 `%TEMP%\PenBoard-update.log`，下一次失败可直接查看。
- 0.1.17 的“删除自动化替换系统手写板、恢复系统键盘默认值、只保留双击检测”保持不变。

## SHA256

```text
MSI:    6E154897EC0FF4333CA38D28139191DD8CE147E03BA27FDC7229A6926BB1488C
便携版: F38AB1517B0B60D847CDA972277E45A2C0D3D910196A88ED05C41888F4DFFF2E
```

源码：https://github.com/qxsraiy/PenBoard
