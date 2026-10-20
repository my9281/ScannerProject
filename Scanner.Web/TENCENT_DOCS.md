# 腾讯文档 OAuth 与 BreezaryStar 读取测试

当前日常记录读取已改为 MySQL 本地表。请先执行 `Database/017_breezarystar_receipts.sql`；入库、编辑推送和自动同步配置见 [本地数据库与双向同步](BREEZARY_SYNC.md)。本文的 OAuth / DirectToken 凭据配置仍适用。

服务器使用腾讯文档官方 OAuth 2.0 和 REST Open API。管理员在浏览器授权一次，服务器保存并刷新令牌。当前是一套 Scanner.Web 安装共用一个腾讯文档授权账号，所有管理、查询接口都要求 Scanner 管理员的 `system.configure` 权限；回调由一次性 state 和同浏览器 Cookie 校验，不要求腾讯携带 Scanner 登录令牌。

## 开放平台配置

如果开发者页面已经提供 `client_id`、`access_token`、`open_id`，可先使用直接凭据模式测试读取。在服务器 `appsettings.Local.json` 的 `TencentDocs` 节填写 `Enabled=true`、`AuthMode="DirectToken"`、`ClientId`、`AccessToken`、`OpenId`，保留本文的 `EncodedId` 和 `SheetId`。此模式不需要 `ClientSecret`、`RedirectUri`，不发起 OAuth、不自动刷新，令牌到期后需手动重新生成并更新配置。可选 `AccessTokenExpiresAt` 使用带时区的 ISO 日期，例如 `2026-11-15T09:20:13+08:00`（以平台实际显示为准）；不填时由腾讯接口判断令牌有效性。配置后重启服务，管理员在测试页直接点击「拉取数据」。以下 OAuth 步骤用于后续自动续期接入。

1. 登录 [腾讯文档开放合作平台](https://docs.qq.com/open/)，注册开发者并创建第三方应用，完成平台审核，取得 `Client ID` 和 `Client Secret`。
2. 配置授权回调域名白名单。如果站点继续使用当前部署文档中的域名，填写 `wms.ymforever.com`。平台若要求完整回调 URL，填写：

   ```text
   https://wms.ymforever.com/api/tencent-docs/oauth/callback
   ```

   如部署到其他域名，请同时替换平台设置及下方 `RedirectUri`，两者必须完全一致。本实现要求 HTTPS、根站点部署、固定回调路径，不能带 query 或 fragment。
3. 申请智能表格权限 `scope.smartsheet`，以及文档标识转换所需的 `scope.drive.file.metadata.readonly`（或官方允许的 `scope.drive.readonly` / `scope.drive.file.metadata`）。授权入口按官方要求发送 `scope=all`，实际可用范围由应用审批和账号访问权限决定。
4. 授权时选择能访问目标智能表格的 QQ/微信账号。网页分享权限不能替代 Open API 授权。

## 服务器配置

在**服务器部署目录**的 `appsettings.Local.json` 中合并下列节；保留文件中已有 Database 等配置。该文件已从 Git 忽略，且不会随项目发布复制。不要将 Client Secret 放进网页、WPF 或仓库。

```json
{
  "TencentDocs": {
    "Enabled": true,
    "ClientId": "替换为应用 Client ID",
    "ClientSecret": "替换为应用 Client Secret",
    "RedirectUri": "https://wms.ymforever.com/api/tencent-docs/oauth/callback",
    "StoragePath": "C:\\ScannerData\\TencentDocs",
    "EncodedId": "DYk5kdUx1aGJUQXdp",
    "FileId": "",
    "SheetId": "t00i2h"
  }
}
```

也可配置对应环境变量，例如 `TencentDocs__ClientId`、`TencentDocs__ClientSecret`、`TencentDocs__RedirectUri` 等，环境变量优先于 JSON。修改后重启站点或回收 IIS 应用池。默认 `Enabled=false`，不影响现有业务接口。

`StoragePath` 必须在 `wwwroot` 外，给 IIS 应用池身份授予该目录的读写权限。此目录包含加密授权文件和持久 Data Protection 密钥，需一起备份，并保留跨发布、应用池回收使用。限制目录访问权限；能读取密钥及授权文件的人可以解密令牌。不要把密钥目录发布到公网。

默认的 `EncodedId`、`SheetId` 对应「Breezary 风扇 - 维修美东 / 收货记录表」。服务器会通过官方 converter 接口将分享链接 ID 转换为 `fileID`。如果已有 API 返回的真实 `fileID`，可以直接填写 `FileId`，跳过 converter。

## 授权与读取

1. 将新版本 Scanner.Web 部署到站点，完成上述配置。
2. 在 `https://wms.ymforever.com/login.html` 使用现有 Scanner 管理员账号登录。
3. 从账户页进入「BreezaryStar · 腾讯文档」，或直接打开 `https://wms.ymforever.com/tencent-docs.html`。
4. 点击「连接 / 重新授权腾讯文档」，在腾讯页面扫码并同意授权。成功后自动返回测试页。
5. 点击「检查工作表」确认子表 ID，再点击「拉取数据」。页面自动分页读取整张工作表，最多展示 10000 条；不按分享链接中的 viewId 过滤。

随后运行 `ScannerProject.BreezaryStar.WPF`，填写服务器 HTTPS 根地址、Scanner 管理员账号及密码，点击「拉取数据」。WPF 通过本服务器的登录、状态和记录接口获取数据，结束后注销临时 Scanner 会话；不需要在桌面程序中填写腾讯文档 Token 或应用密钥。

授权 state 和浏览器 Cookie 有效期 10 分钟、只能使用一次。部署或进程重启会使未完成授权失效，重新点击连接即可。多实例负载均衡需为授权过程保持同实例路由；当前没有分布式 state 存储。

访问令牌将在到期前 5 分钟内、下一次读取时刷新，以腾讯 `expires_in` 为准。刷新响应不返回新 refresh_token 时保留原刷新令牌；连接文件通过跨进程锁和原子替换避免并发刷新覆盖。换应用 ClientId 后要求重新授权。

## 接口

除 OAuth 回调外均携带 `Authorization: Bearer <Scanner 登录返回的 token>`，不是腾讯文档 Token：

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| GET | `/api/tencent-docs/status` | 配置、连接、到期时间；不返回密钥或腾讯 Token |
| POST | `/api/tencent-docs/oauth/start` | 创建授权会话，返回腾讯授权 URL 并设置安全 Cookie |
| GET | `/api/tencent-docs/oauth/callback` | 腾讯回调，服务器换 Token |
| GET | `/api/tencent-docs/sheets` | 查询配置文档的工作表信息 |
| GET | `/api/tencent-docs/records?offset=0&limit=100` | 读取 MySQL 本地记录，返回 records、hasMore、next、total |
| GET | `/api/tencent-docs/remote-records?offset=0&limit=100` | 诊断用直接读取腾讯记录 |

当前已支持本地入库、修改和待同步字段回写，接口详见同步说明。仅操作服务器配置的目标文档，不允许客户端传任意腾讯 API 地址。授权和业务请求关闭重定向；专用 HttpClient 禁用 HTTP 日志，防止官方 GET Token URL 中的密钥进入请求日志。若服务器启用了额外的 APM、出站代理记录或 IIS 查询字符串日志，也需对 OAuth 查询参数做脱敏；回调页面不向浏览器回传腾讯令牌。

## 适用范围与收费

官方旧版智能表 REST 文档说明接口适用于在线表格内的智能子表；用户给出的 `/smartsheet/` 独立智能表是否已被当前应用开放，需要授权后通过真实接口验证。本实现会报告文档类型/权限错误，不把失败标为读取成功，不以 MCP 个人 Token 隐式替代 OAuth。

官方开放接口概述写明从 2025-07-01 起每应用提供 20000 次免费调用（未注明按月重置），更多额度需要申请资源包。以开放平台当前账号显示的权益及报价为准；此处 Open API 与个人 MCP 的 VIP 权益不是同一项承诺。

官方依据：[OAuth 流程](https://docs.qq.com/open/document/app/oauth2/)、[授权入口](https://docs.qq.com/open/document/app/oauth2/authorize.html)、[换取 Token](https://docs.qq.com/open/document/app/oauth2/access_token.html)、[刷新 Token](https://docs.qq.com/open/document/app/oauth2/refresh_token.html)、[文档 ID 转换](https://docs.qq.com/open/document/app/openapi/v2/file/util/converter.html)、[智能表概念和适用范围](https://docs.qq.com/open/document/app/openapi/v2/smartsheet/common/concept.html)、[查询记录](https://docs.qq.com/open/document/app/openapi/v2/smartsheet/record/get_records.html)、[额度说明](https://docs.qq.com/open/document/app/)。

## 验证

```powershell
dotnet build Scanner.Web/Scanner.Web.csproj --no-restore -m:1 -p:UseSharedCompilation=false
dotnet run --project tests/TencentDocs.Tests/TencentDocs.Tests.csproj -p:BuildInParallel=false -p:UseSharedCompilation=false
```

自动测试不连接腾讯真实账号或生产数据库。实际应用审批、授权及目标智能表兼容性需由部署后的真实账号验收。
