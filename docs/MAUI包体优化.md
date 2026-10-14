# MAUI 包体优化

适用工程：Scanner.AndroidTester、Scanner.CheckListBoard、Scanner.MaUI。统一配置见 build/MauiPackageSize.targets。

## 打包调整

- PDA 使用原生 MAUI，移除未使用的 Blazor WebView 依赖和注册；历史 Razor/wwwroot 源文件保留，不参与原生应用打包。
- PDA、平板只打包实际使用的图片与字体，移除模板图片和闲置资源；现有壹仓图标、透明通道、页面图片保持原样。
- Scanner.MaUI 保留中英西 UI 字体 GenJyuuGothic；不打包闲置 ZiHunShouJin（约 4.8 MiB）及 Bickham 字体。IMing（约 23.7 MiB）只供 Windows/Mac 打印：Windows 使用嵌入资源，Mac 使用应用字体，避免 Windows 重复打包及其他平台携带。以上是源资源大小，不是安装包压缩后的减少量。
- 调试日志提供程序仅在 Debug 引用；Scanner.MaUI 的 ZXing 仅供 Windows/Mac 打印引用。
- Release 关闭调试符号，Android/Mac 使用部分裁剪。未启用风险较高的全量裁剪；处理器架构支持不变。
- Android Release 默认关闭 Mono AOT、Windows Release 默认关闭 ReadyToRun，减少预编译代码。启动可能稍慢；Mac AOT 保持平台要求。

## 发布

安装 .NET 10 和对应 MAUI 工作负载后，使用：

```powershell
dotnet publish Scanner.AndroidTester/Scanner.AndroidTester.csproj -c Release -f net10.0-android -p:ScannerTargetFramework=net10.0-android
dotnet publish Scanner.CheckListBoard/Scanner.CheckListBoard.csproj -c Release -f net10.0-android -p:ScannerTargetFramework=net10.0-android
# 在 Mac 上执行
dotnet publish Scanner.MaUI/Scanner.MaUI.csproj -c Release -f net10.0-maccatalyst -p:ScannerTargetFramework=net10.0-maccatalyst
```

ScannerTargetFramework 只限制应用的构建平台，不改变共享类库目标框架。若优先启动速度，追加 `-p:ScannerPreferSmallPackage=false` 恢复 SDK 默认预编译策略；资源精简仍生效。正式分发需使用现有生产签名流程，构建生成的默认签名 APK 不应直接替代生产签名版本。

## 验证与边界

2026-10-10 验证：PDA Android Release 打包成功（22.53 MiB），原生扫码、XML 配置、SKU/SN 配对与恢复测试通过；平板客户端 36 项检查通过。三个工程的 Android Release 均已成功打包：PDA 22.53 MiB，平板 22.48 MiB，Scanner.MaUI Android 46.82 MiB。平板与 Scanner.MaUI 构建均 0 警告、0 错误；PDA 构建有容大 SDK 生成绑定代码的警告，0 个错误。实际安装后仍需检查扫码、上传/重试、平板保存、三语言提示及蓝牙打印。Windows/Mac 平台字体打印和 Mac 安装包需在对应平台验收。

历史可用包为 Debug，不能用它与本次 Release 计算优化比例。对比包大小必须使用相同 SDK、架构、签名和 Release 配置。

配置依据：[MAUI 裁剪](https://learn.microsoft.com/en-us/dotnet/maui/deployment/trimming?view=net-maui-10.0)、[Android 构建属性](https://learn.microsoft.com/en-us/dotnet/android/building-apps/build-properties)、[运行时与编译](https://learn.microsoft.com/en-us/dotnet/maui/deployment/runtimes-compilation)。
