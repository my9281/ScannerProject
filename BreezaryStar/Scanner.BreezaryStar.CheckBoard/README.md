# Scanner.BreezaryStar.CheckBoard

从 Scanner.CheckListBoard 独立复制的 .NET 10 MAUI 工程，位于 BreezaryStar 下，已加入 Scanner.slnx。使用独立应用标识 com.scanner.breezarystar.checkboard、独立登录凭据存储键和服务地址设置，不覆盖原 CheckListBoard 应用。

## 查看预报

1. 将本工程设为启动项目，用现有 Scanner 管理员账号登录；默认服务器为 https://wms.ymforever.com/，可通过登录页“服务设置”修改。
2. 首页第一个按钮为“查看预报”，打开时自动分页读取 GET /api/tencent-docs/records，即 Scanner.Web 已入库的 MySQL 数据。
3. 展示 SKU、工单号、原运单号、实际签收快递单号、渠道、是否签收、状态流转、发货日期、签收日期、备注和问题描述，以及同步状态。日期显示为 yyyy-MM-dd。
4. 支持搜索和手动刷新；返回页面会取消正在进行的请求。失败时保留已展示记录并提示原因。

当前服务器预报接口沿用 system.configure 权限，因此此入口要求管理员。其他模块保留原工程行为。MAUI 不直接访问 MySQL，也不直接调用腾讯文档；首次数据入库和自动同步请参阅 [Web 同步说明](../../Scanner.Web/BREEZARY_SYNC.md)。

## 构建与验证

```powershell
dotnet build BreezaryStar/Scanner.BreezaryStar.CheckBoard/Scanner.BreezaryStar.CheckBoard.csproj -p:ScannerTargetFramework=net10.0-android -m:1 -p:UseSharedCompilation=false
dotnet run --project tests/BreezaryStar.CheckBoard.Tests/BreezaryStar.CheckBoard.Tests.csproj
```

保留原工程 Android、iOS、Mac Catalyst 和 Windows MAUI 目标声明；各平台发布需要相应 SDK 和签名环境。自动测试使用模拟 HTTP 响应，不连接真实服务器或数据库。
