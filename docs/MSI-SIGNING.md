# PenBoard：MSI 安装包与代码签名方案

日期：2026-10-24  
适用：PenBoard 0.1.4 / Windows ARM64

---

## 1. 先给结论

1. **MSI 适合做正式安装/卸载/升级，但它本身不解决 SmartScreen。**
   - 未签名的 MSI 同样会被 SmartScreen 拦。
   - 已签名但证书没有信誉的 MSI，也可能先提示一段时间。
   - 真正彻底的修复是 **Authenticode 代码签名 + 时间戳 + 证书信誉**。

2. **v0.1.4 已经提供 per-user WiX MSI**（中文/英文两个 MSI），同时保留 ZIP 便携版。
3. **MSI 仍需要 Authenticode 签名**，否则 SmartScreen 仍可能拦截。
4. 如果你有代码签名证书，签 exe 和 MSI 后，SmartScreen 提示会显著减少，EV 证书通常最快；OV 证书需要积累信誉。

---

## 2. SmartScreen 到底在检查什么

Windows 对从网络下载的文件写入一个 NTFS 备用数据流：

```text
文件名:Zone.Identifier
[ZoneTransfer]
ZoneId=3
```

浏览器下载的 ZIP 带这个标记；用资源管理器解压后，里面的 exe 通常也带标记。SmartScreen 看到“未签名 + 下载标记 + 没有信誉”就弹：

> Windows 已保护你的电脑  
> Microsoft Defender SmartScreen 阻止了无法识别的应用启动

v0.1.2 的处理：

- `install.ps1` 对 `app` 目录和安装目录执行 `Unblock-File`；
- `PenBoard.exe` 启动时删除自身和 `PenBoard.dll` 的 `Zone.Identifier`；
- 提供 `unblock.cmd` / `Unblock-PenBoard.cmd`。

这些能解决“安装后每次启动还弹”的常见情况，但解决不了企业策略或“未签名程序”本身。

---

## 3. 代码签名怎么做

### 3.1 证书类型

| 类型 | 特点 | SmartScreen |
|---|---|---|
| 自签名证书 | 自己生成，不进入微软信任链 | 没有帮助，仍会提示未知发布者 |
| OV 代码签名证书 | 组织验证，文件证书或令牌 | 需要积累下载信誉，初期可能提示 |
| EV 代码签名证书 | 扩展验证，硬件令牌/HSM | 通常能更快建立信誉，最适合桌面软件 |
| Azure Trusted Signing | 微软云签名服务，按签名次数计费 | 需要 Azure 订阅、身份验证；适合 CI |

> 不建议用自签名证书“骗过”SmartScreen；它不会建立微软信任，反而增加部署复杂度。

### 3.2 需要准备什么

- 证书（OV/EV）或 Azure Trusted Signing 账户；
- 证书主题里的**发布者名称**（例如 `PenBoard Software`）；
- 时间戳服务地址（常用 DigiCert / Sectigo）；
- 签名机的证书存储或硬件令牌；
- 如果走 CI：把签名权限放到 CI secret/托管身份里，不要把 PFX 和密码贴到聊天或提交到仓库。

### 3.3 签名命令

`signtool.exe` 来自 Windows SDK，或 NuGet 包 `Microsoft.Windows.SDK.BuildTools`。

```powershell
# 1. 签主程序和主 DLL
signtool sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com /a `
  "artifacts\arm64\app\PenBoard.exe"

signtool sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com /a `
  "artifacts\arm64\app\PenBoard.dll"

# 2. 打 MSI 后签 MSI（最后签安装包）
signtool sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com /a `
  "artifacts\PenBoard-0.1.3-arm64.msi"

# 3. 验证
signtool verify /pa /v "artifacts\PenBoard-0.1.3-arm64.msi"
```

要点：

- 一定加 `/tr` 时间戳；否则证书过期后签名失效。
- 先签 exe/dll，再签 MSI；如果以后加 Burn bootstrapper，最后签 bootstrapper。
- 证书在 `CurrentUser\My` 时可用 `/sha1 <thumbprint>` 指定，避免签错证书。
- EV 令牌通常通过证书存储访问，签名时可能要求输入 PIN。

### 3.4 SmartScreen 信誉

签名不等于立刻“全网无提示”：

- **EV**：通常最快建立信誉；
- **OV**：需要真实下载量/时间积累，前期可能仍提示；
- 可以到 Microsoft Security Intelligence 提交文件误报/信誉复核：
  `https://www.microsoft.com/en-us/wdsi/filesubmission`
- 用一台干净的 Windows 虚拟机，给文件加 `Zone.Identifier` 后测试，才接近用户真实下载场景。

---

## 4. MSI 方案设计

### 4.1 推荐形态

- **per-user MSI**：安装到 `%LOCALAPPDATA%\Programs\PenBoard`，不需要管理员；
- 保留当前 `%LOCALAPPDATA%\PenBoard` 作为旧 ZIP 版本的迁移来源；
- 桌面快捷方式 + 开始菜单 + 可选开机启动；
- 控制面板/设置 → 应用 → 已安装的应用里有 PenBoard，可“修改/卸载”；
- 安装包和 exe 都签名。

如果面向企业统一部署，再做 per-machine MSI（需要管理员和组策略）。

### 4.2 升级与清理旧版本

MSI 的关键是三个东西：

1. **固定 `UpgradeCode`**（所有版本相同）；
2. **每次发布递增 `Version`**（0.1.2 → 0.1.3）；
3. **`MajorUpgrade`**：

```xml
<MajorUpgrade
    DowngradeErrorMessage="A newer version of PenBoard is already installed."
    Schedule="afterInstallInitialize" />
```

`Schedule="afterInstallInitialize"` 会在安装新文件前移除旧 MSI 产品，适合“覆盖安装”。

旧版本残留分三类：

| 残留 | 处理 |
|---|---|
| 旧 MSI 文件/快捷方式 | `MajorUpgrade` + `RemoveExistingProducts` 自动处理 |
| 旧 ZIP 版 `%LOCALAPPDATA%\PenBoard` | 自定义动作/`util:RemoveFolderEx` 在安装前清理 |
| 日志、诊断、缓存 | 安装/卸载时清理；设置是否保留用属性控制 |
| 用户图片 `图片\PenBoard` | **永远保留**，不删 |

WiX v5 可以用 `util:RemoveFolderEx` 删除非 MSI 管理的旧目录：

```xml
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"
     xmlns:util="http://wixtoolset.org/schemas/v4/wxs/util">
  <Package ...>
    <util:RemoveFolderEx Id="RemoveOldPenBoardFolder"
                         Property="OLDPENBOARDFOLDER"
                         On="both" />
  </Package>
</Wix>
```

`OLDPENBOARDFOLDER` 通过 `RegistrySearch` 或 `DirectorySearch` 指向 `%LOCALAPPDATA%\PenBoard`。注意：执行顺序要放在 `InstallFiles` 之前，避免删掉刚装的新文件。

### 4.3 安装/卸载时的进程与文件锁

安装前先停掉托盘程序，否则 exe 被占用：

```xml
<CustomAction Id="StopPenBoard"
              ExeCommand="taskkill.exe /f /im PenBoard.exe"
              Execute="deferred"
              Impersonate="yes"
              Return="ignore" />
```

卸载也要停一次。再用 `Unblock-File` 自定义动作清理下载标记：

```xml
<CustomAction Id="UnblockFiles"
              ExeCommand="powershell.exe -NoProfile -ExecutionPolicy Bypass -Command &quot;Get-ChildItem '[INSTALLFOLDER]' -Recurse -File | Unblock-File&quot;"
              Execute="deferred"
              Impersonate="yes"
              Return="ignore" />
```

生产版更稳的做法是写一个小的 `PenBoard.SetupHelper.exe`，做 stop/unblock/cleanup，避免 PowerShell 执行策略和引号问题。

### 4.4 卸载

- `MajorUpgrade` 负责旧 MSI；
- `RemoveFolderEx` 清 `%LOCALAPPDATA%\PenBoard`、日志、缓存；
- 删除桌面/开始菜单/启动快捷方式；
- 默认**保留** `%APPDATA%\PenBoard\settings.json`（用户设置）；
- 如果用户选择“彻底卸载”，再删设置和日志；
- 不删 `图片\PenBoard`。

### 4.5 打包命令

```powershell
dotnet tool install --global wix --version 5.*
wix build installer\PenBoard.wxs `
  -ext WixToolset.Util.wixext `
  -o artifacts\PenBoard-0.1.3-arm64.msi
```

然后签 MSI：

```powershell
signtool sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com /a `
  artifacts\PenBoard-0.1.3-arm64.msi
```

### 4.6 MSI 与 SmartScreen

再说一次：**MSI 不会自动绕过 SmartScreen**。

- 未签名 MSI：仍可能被拦；
- 已签名 MSI：看证书和信誉；
- 签名 + 时间戳 + EV/信誉：最接近“用户双击直接装”；
- 没有证书时，MSI 仍需要用户点“更多信息 → 仍要运行”，或者由 IT 用组策略/Intune 分发。

---

## 5. 建议的实施顺序

1. **v0.1.2**：当前 ZIP，修复模式切换和按钮高亮（已完成）。
2. **v0.1.3**：WiX per-user MSI：
   - 安装/卸载/修复；
   - `MajorUpgrade` + 旧 ZIP 目录清理；
   - 停进程、Unblock、快捷方式；
   - 保留用户图片。
3. **v0.1.4 / 正式版**：拿到证书后签 exe + MSI，做干净虚拟机 SmartScreen 测试。
4. 如果走企业分发，再补 per-machine MSI、Intune/Win32 app 包装和静默安装参数。

---

## 6. OV 证书需要你做什么

OV（Organization Validation）代码签名证书不是“下载一个文件”就能用的，通常需要：

1. **准备主体资料**
   - 公司/组织注册证明、营业执照或同等文件；
   - 对公电话/邮箱，CA 会做电话或邮件验证；
   - 申请人的姓名、职位、联系方式。
2. **在 CA 下单**
   - DigiCert、Sectigo、GlobalSign、SSL.com 等都可以；
   - 选择 OV Code Signing Certificate，期限通常 1–3 年；
   - 个人开发者通常需要个体户/公司主体；纯个人可问 CA 是否有 Individual Code Signing 产品。
3. **完成验证**
   - CA 验证组织真实存在、申请人有权代表组织；
   - 可能要求回拨电话、DNS/邮箱验证、视频或公证件。
4. **拿到证书**
   - 文件证书（.pfx）或硬件令牌/HSM；
   - 把证书安装到签名机的 `CurrentUser\My` 或 `LocalMachine\My`；
   - 确认证书有 Code Signing EKU。
5. **提供给我什么**
   - 证书**指纹（thumbprint）**和证书主题里的发布者名称；
   - 证书已经装在这台签名机上，或者你本人在机器上输入令牌 PIN；
   - 不要把 PFX 密码/私钥直接发到聊天里。
6. **签名后**
   - 用 `signtool verify /pa /v` 验证；
   - 先在小范围测试 SmartScreen；
   - OV 证书通常需要一段时间的下载信誉积累，初期仍可能提示；EV 通常更快。

如果证书在硬件令牌里，签名时机器会弹 PIN；我可以把签名脚本和命令准备好，由你插令牌执行，或者你远程操作签名机。

## 7. 需要你确认的参数

1. 安装范围：**per-user（推荐，免管理员）** 还是 per-machine？
2. 安装目录：`%LOCALAPPDATA%\Programs\PenBoard`（推荐）还是继续 `%LOCALAPPDATA%\PenBoard`？
3. 卸载时是否删除 `%APPDATA%\PenBoard\settings.json` 和日志？默认保留设置、删日志。
4. 是否保留开机启动？默认保留，可在安装界面取消。
5. 证书：OV / EV / Azure Trusted Signing，发布者名称是什么？
6. MSI 界面语言：中文 / 英文 / 双语。

只要拿到证书或 Azure Trusted Signing 的权限，签名和 MSI 可以一起做完；没有证书也可以先做 MSI，但 SmartScreen 提示仍然存在。
