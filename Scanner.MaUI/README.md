# MAUI 桌面工程

MAUI 是后续 macOS 客户端的入口，业务规则以 `Scanner.WPF` 为参照。
本次整理覆盖扫描、工单匹配、预报导入与应用组装；尚未完成 WPF 全功能移植。

## 代码边界

- `MauiProgram.cs`：启动、字体、日志。
- `Controllers/MauiComposition.cs`：集中注册共享服务、MAUI 服务、控制器与页面，对应 WPF 的 `DesktopComposition`。
- `Controllers` / `Views`：页面交互与视图接口。
- `ViewModels/MainViewModel.cs`：扫描流程、忙碌状态、用户反馈。
- `Services`：扫描、工单、会话与设备适配。
- `Platforms/MacCatalyst`：Mac 入口、权限、应用元数据。
- `Scanner.Helpers`：与 WPF 共用的导入、报表等业务能力。

## 已对齐的规则

- 扫描先清除空白、BOM、零宽空格，再去重、计数与匹配。
- GS1 地区码警告；OID 首次不打印，重复扫描触发打印。
- 同一编号只写一次日志；日志写入失败可重试，计数不提前增加。
- 导入运单使用超过八位的尾号匹配，新的不匹配 OID 清除上一次上下文。
- CSV 与 XLSX 均调用 WPF 使用的 `UrgentWorkOrderImportHelper`：DTC 表头、FedEx 预报、备注合并、维修标记规则保持一致。旧的 44 列 CSV 解析已移除。
- 型号按最长名称优先匹配。

## 本机验证

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
dotnet run --project tests/BarcodeClassification.Tests --disable-build-servers
dotnet build Scanner.MaUI/Scanner.MaUI.csproj -p:ScannerTargetFramework=net10.0-windows10.0.19041.0 --disable-build-servers -m:1
```

## Mac 开发入口

在配置好对应 .NET MAUI / Xcode 环境的 Mac 上，显式选择 MacCatalyst 和目标架构：

```sh
dotnet build Scanner.MaUI/Scanner.MaUI.csproj -p:ScannerTargetFramework=net10.0-maccatalyst -p:RuntimeIdentifier=maccatalyst-arm64
# Intel Mac 将架构改为 maccatalyst-x64
```

项目不再强制 Mac 使用 x64；MacCatalyst 显式引用 `Entitlements.plist`。
签名、证书、发布方式应在实际 Mac 环境确定。本次没有生成或验证 Mac 安装包。

## 后续移植与验收

- 现有 Mac 打印使用系统打印面板；需实机验证默认打印机、取消操作、多份打印、4×4/4×6 尺寸、中文字体和条码可读性。
- 登录、SecureStorage 会话恢复、扫码枪回车、语音、文件选择和日志打开需在 Mac 实机验证。
- WPF 的扫描 Excel 台账、新建记录文件、检验 OK 打印、自定义型号等能力尚未在 MAUI 主流程中对齐。
- WPF 的补打/特殊标签、批量标签、BLUETTI 会话及其他独立窗口应逐项确认需求后移植；现有 MAUI OperationsPage 不是全部 WPF 窗口的替代。
- WPF 与 MAUI 的登录/接口契约仍分开；后续统一前需核对服务端契约。
