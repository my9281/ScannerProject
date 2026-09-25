# WMS 文件管理站点

该 ASP.NET Core 站点支持通过网页或 API 上传、浏览、预览和下载 TXT / JSON 文件。文件默认按 UTC 日期保存到 `App_Data/uploads/yyyyMMdd`，该目录不会被 Git 跟踪，也不能通过静态文件地址直接访问。

## API

- `GET /api/account/domains`：获取可选择的域列表。
- `POST /api/account/register`：注册内存账户，请求字段为 `username`、`password`、`confirmPassword` 和 `domainId`。
- `POST /api/account/login`：登录内存账户，请求字段为 `username` 和 `password`。
- `POST /api/uploads/scans`：上传文件，表单字段为 `file` 和可选的 `deviceName`。
- `GET /api/uploads`：列出已上传文件。
- `GET /api/uploads/{id}/content`：读取文件内容，供网页预览。
- `GET /api/uploads/{id}/download`：下载文件。
- `GET /health`：健康检查。

除健康检查外，所有 API 在配置了密钥时都需要请求头 `X-Upload-Key`。上传仅接受有效的 `.txt` 或 `.json` 文件，默认最大 10 MB；在线预览默认最大 1 MB。

账户接口暂不连接数据库，注册数据仅保存在当前进程内，应用重启后会清空。登录成功后返回的随机令牌目前仅作为接口返回值，尚未接入其他接口的授权验证。

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

未配置 `Upload__ApiKey` 时接口允许匿名访问，只适合本地测试。生产环境还需确保运行站点的账户对上传目录有读写权限。

## 运行与发布

```powershell
dotnet run --project Scanner.Web.csproj
dotnet publish Scanner.Web.csproj -p:PublishProfile=IIS-SelfContained
```

`IIS-SelfContained` 和 Visual Studio 使用的 `FolderProfile` 均按 Windows x64 自包含方式发布。发布到 IIS 时，必须将发布目录的全部文件（不只是项目 DLL）部署到站点物理目录。服务器仍需安装 ASP.NET Core Hosting Bundle 以提供 IIS 的 AspNetCoreModuleV2，但不要求安装与项目匹配的 .NET 10 运行时。应用程序池使用“无托管代码”。
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

服务器已安装 ASP.NET Core 10 Hosting Bundle 时，使用 `LeanUpload` 配置生成精简、单文件、无 PDB 的 Windows x64 发布包：

```powershell
dotnet publish Scanner.Web -c Release -p:PublishProfile=LeanUpload
```

输出目录为 `Scanner.Web/bin/Release/net10.0/publish/secure-lean-win-x64`。此配置不启用 Trim，避免 ASP.NET Core 控制器和 JSON 反射被错误裁剪；它通过复用服务器运行时和单文件打包减少体积及上传文件数量。
