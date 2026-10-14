# WMS 文件管理站点

该 ASP.NET Core 站点支持通过网页或 API 上传、浏览、预览和下载 TXT / JSON 文件。文件默认按 UTC 日期保存到 `App_Data/uploads/yyyyMMdd`，该目录不会被 Git 跟踪，也不能通过静态文件地址直接访问。

## API

- `GET /api/account/domains`：获取可选择的域列表。
- `POST /api/account/register`：注册账户（当前公开站点关闭注册入口），请求字段为 `username`、`password`、`confirmPassword` 和 `domainId`。
- `POST /api/account/login`：登录 MySQL app_users 账户，请求字段为 `username` 和 `password`。
- `POST /api/uploads/scans`：上传文件，表单字段为 `file` 和可选的 `deviceName`。
- `GET /api/uploads`：列出已上传文件。
- `GET /api/uploads/{id}/content`：读取文件内容，供网页预览。
- `GET /api/uploads/{id}/download`：下载文件。
- `GET /health`：健康检查。

设备上传及业务数据 API 需要配置密钥并提供请求头 `X-Upload-Key`；账户登录接口可由浏览器直接访问。上传仅接受有效的 `.txt` 或 `.json` 文件，默认最大 10 MB；在线预览默认最大 1 MB。

账户登录使用 MySQL app_users，令牌会话存储在 user_sessions。登录状态通过 me 接口验证；检测模块业务接口可通过 AccountPermission 验证权限。部署依赖、角色和测试步骤见 [WEB_LOGIN.md](WEB_LOGIN.md)。

## 账户页面

- `/account/login`：登录页面。
- `/account/register`：注册页面。

## 配置

生产环境建议使用环境变量：

```text
Upload__ApiKey=一段足够长的随机密钥
Upload__Directory=/var/lib/ymstar/uploads
Upload__MaxBytes=10485760
Upload__MaxPreviewBytes=1048576
```

未配置 `Upload__ApiKey` 时，受密钥保护的接口返回 503。生产环境还需确保运行站点的账户对上传目录有读写权限。

## 运行与发布

```powershell
dotnet run --project Scanner.Web.csproj
dotnet publish Scanner.Web.csproj -p:PublishProfile=IIS-SelfContained
```

Visual Studio 使用的 `FolderProfile` 按 Windows x64 框架依赖方式发布，需要服务器安装 ASP.NET Core 10 Hosting Bundle。它只携带应用和 MySQL 驱动，不复制 .NET 运行时。`IIS-SelfContained` 保留自包含方式，适用于需要随应用携带运行时的部署；IIS 仍需安装 Hosting Bundle 提供 AspNetCoreModuleV2。应用程序池使用“无托管代码”。请使用新发布目录部署，并保留服务器现有配置和上传数据，避免旧运行时文件留在站点目录。
## 上架托盘接口

接口使用与上传接口相同的 `X-Upload-Key` 请求头。

批量录入：`POST /api/shelved-pallets`

```json
{
  "palletNumber": "TP20260924001",
  "items": [
    {
      "number": 1,
      "sn": "SN000001",
      "sku": "SKU000001",
      "type": "调拨入库",
      "processingMethod": "检测通过",
      "processingTime": "2026-09-24 10:30:00"
    }
  ]
}
```

上架日期、上架时间和 UUID 由 Web 服务生成；整个批次使用数据库事务写入。

查询：`GET /api/shelved-pallets?from=2026-09-24T00:00:00&to=2026-09-24T23:59:59&palletNumber=TP20260924001&limit=500`

`from`、`to` 和 `palletNumber` 都可以省略，`limit` 范围为 1–1000。

按整天和托盘号查询时可使用简写：`GET /api/shelved-pallets?date=2026-09-24&p=1`。其中 `date` 表示该日期全天，`p` 是 `palletNumber` 的简写。

简洁展示页面：`/pallet-data.html?t=2026-09-24&p=1`。页面顶部显示上架日期和托盘号，明细仅显示 SKU、SN；API 也接受 `t` 作为 `date` 的简写。

## 飞书机器人推送

在 Web 配置中填写 `FeishuRobot`。上架托盘批量录入成功后，后台会推送托盘号、上架日期、数量和 `/pallet-data.html?t=日期&p=托盘号` 查看地址。推送失败只写服务器日志，不会回滚或误报已经成功的数据库录入。

```json
"FeishuRobot": {
  "Enabled": true,
  "WebhookUrl": "https://open.feishu.cn/open-apis/bot/v2/hook/你的机器人标识",
  "Secret": "开启签名校验时填写；未开启则留空",
  "PublicBaseUrl": "https://wms.ymforever.com"
}
```

测试接口：`POST /api/feishu/test`，请求体示例：`{"message":"测试推送","url":"https://wms.ymforever.com"}`。接口继续使用 `X-Upload-Key` 请求头。

托盘页面提供“推送飞书”按钮，对应接口为 `POST /api/feishu/pallet?t=2026-09-24&p=1`。后台会核对该日期和托盘的数据、计算数量，再推送当前展示页地址。

## 精简发布包

首页为单屏黑底白字展示页，仅保留登录入口。`GET /api/home/metrics` 是公开的宣传数量接口，仅返回五个计数，不返回文件名、用户信息、SKU 或 SN；计数缓存 60 秒，不可用时返回 null。线路数对应上传目录中的 TXT／JSON 文件数；用户数取 `app_users` 全部记录；良品区托盘数按 `shelved_pallet_data` 的上架日期＋托盘号分组，记录数取该表全部行；维修方案数按 `tester_pallet_scans` 的扫描日期＋托盘号分组。良品区没有出库状态字段，数量表示表内累计数据，不代表实时在库量。

服务器已安装 ASP.NET Core 10 Hosting Bundle 时，使用 `LeanUpload` 配置生成精简、单文件、无 PDB 的 Windows x64 发布包：

```powershell
dotnet publish Scanner.Web -c Release -p:PublishProfile=LeanUpload
```

输出目录为 `Scanner.Web/bin/Release/net10.0/publish/secure-lean-win-x64`。此配置不启用 Trim，避免 ASP.NET Core 控制器和 JSON 反射被错误裁剪；它通过复用服务器运行时和单文件打包减少体积及上传文件数量。

所有发布模式排除 PDB、开发工具清单、数据库脚本、README、本地私有配置、App_Data 数据和已关闭的注册／托盘网页。静态文件通过 `UseStaticFiles` 提供，不发布压缩副本；保留 SDK 生成的静态资源元数据。普通开发构建仍保留调试能力。业务程序集和 MySqlConnector 是实际依赖，必须保留；单文件模式将它们打包到可执行文件中。
