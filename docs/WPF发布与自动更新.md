# WPF 发布瘦身与自动更新

## 发布

在仓库根目录执行：

```powershell
.\build\Publish-WpfUpdate.ps1
```

每次输出到 `artifacts/wpf/<随机目录>/`，包含可直接运行的 `publish` 目录、版本 ZIP 和本地 `latest.json`。通过 Web 发布时只上传 ZIP，服务器重新生成正式清单。默认 win-x64、依赖框架发布：客户端需安装 .NET 10 Windows Desktop Runtime x64。无需预装运行时的部署用 `-SelfContained`，包会较大。更新源应持续使用同一种发布模式，避免自包含安装更新成依赖框架包。

Release 禁用 WPF 调试符号，发布排除 PDB/XML 文档，限制依赖库语言资源为中英西，移除三份未使用的 Bickham 书法字体。保留界面字体、标签打印字体、字体许可文件。不对 WPF 使用不受支持的裁剪；不删除现有 bin/obj 或历史产物，新发布目录避免混入旧文件。

每次发布新版本，先同时提高 `Scanner.WPF/Properties/AssemblyInfo.cs` 的 `AssemblyVersion` 和 `AssemblyFileVersion`，采用四段数字，例如 `1.0.1.0`。脚本从实际 DLL 读取版本，自动计算 ZIP 的 SHA-256，不手填校验值。

## 更新服务器

默认更新地址：`https://wms.ymforever.com/updates/wpf/latest.json`。新版 Scanner.Web 提供管理员上传页面、公开版本清单和更新包下载接口，无需新数据库表。

1. 部署新版 Scanner.Web，保留现有账号配置和数据库。
2. 在 `https://wms.ymforever.com/login.html` 使用管理员账号登录。
3. 从账户页点击“WPF 更新发布”，或直接打开 `https://wms.ymforever.com/wpf-updates.html`。
4. 选择发布脚本生成的 `Scanner.WPF-版本-win-x64.zip`，点击“上传并发布”。不要上传源代码 ZIP、整个 build/bin 目录或手填 JSON。
5. 页面显示新版本后，客户端下次启动会检查并自动更新。

上传接口 `POST /api/updates/wpf` 接受 multipart 字段 `file`，使用 Bearer 账号会话与 `system.configure` 权限，普通账号/只读账号不能发布，客户端 Upload Key 不能用于发布更新。最大 ZIP 512 MB；校验包根目录、路径、x64 EXE、程序集名称/版本与 .NET 10 运行配置。版本从 DLL 读取，SHA-256 从上传文件计算。服务器先保存包，再原子替换清单；同版本、旧版本返回 409，失败不会切换当前清单。发布日志记录管理员账号、版本和校验值。

更新包保存在站点 `App_Data/WpfUpdates/`，不放在 wwwroot、不执行上传内容。IIS 应用池账号需有此目录写权限。可用 `WpfUpdates:StoragePath` 或环境变量 `WpfUpdates__StoragePath` 指向站点之外的持久目录，推荐生产部署使用，避免覆盖发布时删除更新包。不要清空正在使用的历史包，客户端可能已拿到旧清单。发布使用文件锁，支持同存储目录内多个 IIS 进程互斥。

`web.config` 已为 `/api/updates/wpf` 配置约 513 MB IIS 请求上限（包含表单开销）；如果站点前面还有代理，需对应提高此接口的上传限制和超时。页面遇到 413 会给出说明。

清单响应禁止缓存；版本包按校验值命名并允许长期缓存与 Range 下载。服务器生成的清单示例（SHA-256 为实际文件值）：

```json
{
  "version": "1.0.1.0",
  "packageUrl": "/updates/wpf/packages/Scanner.WPF-1.0.1.0-<SHA256>-win-x64.zip",
  "sha256": "<64位十六进制SHA-256>"
}
```

包 URL 使用相对网站根路径，WPF 按清单地址解析到同一域名。客户端要求清单与包使用同 HTTPS 域名和端口，不允许重定向。Web 页面无需输入下载域名。

## 客户端配置与行为

发布后的 `Scanner.WPF.dll.config` 中：

```xml
<add key="AutoUpdateEnabled" value="true"/>
<add key="AutoUpdateManifestUrl" value="https://wms.ymforever.com/updates/wpf/latest.json"/>
```

默认开启，缺少开关也开启；设置为 `false` 禁用。首次安装此版本需手动分发，旧版没有更新功能。安装目录必须可由当前用户写入，推荐使用用户目录；不自动提权。

每次启动在登录前比较本地程序集与远端版本。相同版本正常启动，远端较旧仅记录差异，不降级；远端较新自动下载、验证 SHA-256、检查 ZIP 路径和 DLL 版本，然后退出，由隐藏的 PowerShell 辅助程序备份并替换程序文件，再启动新版。网络/清单检查最多约 10 秒，包下载最多 5 分钟；无网、404、校验失败等继续打开当前程序。重启带 `--updated`，避免同一次更新立即再检查。

更新保留安装目录中的 `Scanner.WPF.dll.config` / `Scanner.WPF.exe.config`，不删除扫描日志和用户数据。已有第二实例、权限不足、文件锁定等会阻止更新；替换失败恢复本次修改的旧文件。回滚异常保留临时备份便于恢复。成功后清理临时包和备份，不清理安装目录中历史遗留的文件。

日志：`%LOCALAPPDATA%/Scanner/Updates/update.log`。临时包/失败备份：`%TEMP%/Scanner.WPF.Update/<随机目录>/`。本功能在启动时检查，运行中的业务窗口不会被定时强制重启。

## 验证

```powershell
dotnet build Scanner.WPF/Scanner.WPF.csproj -c Release -m:1
dotnet run --project tests/WpfUpdate.Tests/WpfUpdate.Tests.csproj -c Release
.\tests\WpfUpdate.Tests\Test-ApplyUpdate.ps1
dotnet run --project tests/WpfUpdate.Web.Tests/WpfUpdate.Web.Tests.csproj -c Release -- <生成的WPF-ZIP完整路径>
```

客户端测试覆盖 HTTPS 同源限制、合法包、版本不符、路径越界/重复、下载体积限制、配置保留、文件锁定失败与回滚。Web 集成测试通过本地 HTTP 验证管理员/普通账号权限、真实 ZIP 上传、清单/包下载、校验值、Range 下载、重复/旧版本拒绝、并发发布与失败时清单保留，不访问生产数据库。服务器上线后还需在实际客户端验收 HTTPS 下载和更新后重启。
