# 壹仓 PDA / Scanner.AndroidTester

更新：2026-10-09。对应 androidtest，主流程采用原生 MAUI 页面，不依赖旧 Blazor/WebView。Web、SQL 和全平台需求见 [总文档](../request.md)。

## 运行与首页

.NET 10 MAUI，主要部署 Android，最低 API 24。其他声明平台不代表设备验收完成。PDA 扫描器以键盘方式输入，Enter 提交，处理后恢复焦点。首页为良品区扫描、打印标签、SKU / SN 扫描；基础资料组含库位扫描。黑色风格、简洁标题，与平板共用壹仓蓝金图标，透明像素保留透明度，原生开屏黑底。

## 良品区扫描

选择整数托盘 1–100，扫描 SN（去首尾空白，1–100 字符），每条记录扫描 GUID 和时间。本次运行相同 SN 大小写敏感去重，上传后仍保留去重集合。

POST `/api/tester-pallet-scans`，每块最多 1000 条，核对确认后移除已上传记录，失败保留待重试。服务器 SN 唯一，重复上传更新托盘归属及时间。WPF 全量下载查看；成功上架从良品区删除对应 SN。

当前列表和去重集合仅在内存，关闭/重启会丢失待上传数据，不是持久化离线队列。

## 库位扫描

所有扫码视为库位 ID，不识别 SKU/SN。去首尾空白，1–100 字符，拒绝控制字符，本次运行大小写敏感去重。POST `/api/warehouse-locations` 分块最多 1000 条；失败或回执不匹配保留待上传记录。

服务端 warehouse_locations 主键去重，不覆盖首次 UTC 扫描时间，不改变禁用位。is_disabled 默认为 0（启用），1 为禁用。GET `/api/warehouse-locations/download` 或根路径 GET 返回全部 JSON，包括禁用位；PDA 暂无下载/禁用管理页面。

待上传数据目前仅内存。部署 `007_warehouse_locations.sql` 前需适配现场 MySQL 5.7.17 的命名 CHECK 语法问题，不能视为已兼容。

## SKU / SN 批次

第一枪 SKU（1–150 字符），第二枪 SN（1–100 字符），一对一条。去首尾空白、拒绝控制字符。同批 SN 大小写敏感去重，最多 5000 条。“重扫 SKU”取消半对，不删除完成记录。有半对或空批次时禁止上传。

批次 GUID 唯一，批次号按首次扫码本地时间 `yyyyMMddmmss`，不含小时，可重复；选择和幂等使用 GUID。应用私有目录 `sku-sn-batch.json` 保存完成明细、半对和封存状态，临时文件替换写入，启动恢复。卸载/清除应用数据会删除文件。

上传前封存快照，封存后不可修改。失败原 GUID 原内容重试；回执 batchId/itemCount 核对成功后清空并生成新 GUID。

| 接口 | 行为 |
| --- | --- |
| POST /api/sku-sn-batches | 整批 batchId、batchNumber、createdAt、items（sku/sn） |
| GET /api/sku-sn-batches | 全部批次摘要，供 WPF 选择 |
| GET /api/sku-sn-batches/{guid} | 完整明细，供 WPF 良品上架下载 |

服务端头与明细同事务，相同 GUID 相同内容幂等，不同内容 400。SQL 为 [009_sku_sn_batches.sql](../Scanner.Web/Database/009_sku_sn_batches.sql)。

## 打印标签

本页当前输入回车自动打印，也可点击打印。空输入不打印；取消或失败保留，发送期间限制重复提交，成功后清空和聚焦。

先在系统配对容大 RP425，允许蓝牙/附近设备权限，选择设备和 TSPL（国内）或 ZPL（国际）。记住选择，长按打印清除选择。标签为 SN、末五位、二维码、Code 128 和日期，保留前导零，不生成紧急/机型字段。

SDK 纸张 102×152 mm、203 dpi，TSPL 间隙 3 mm；连接超时 25 秒、发送确认 60 秒，不自动重试。发送回调不等于出纸，失败先检查纸张再重试。详见 [打印说明](../docs/AndroidTester打印.md)。

## 配置与代码入口

appsettings.xml 为嵌入资源，UploadBaseUrl 设置服务器，UploadApiKey 对应 X-Upload-Key，三个上传模块共用，无密码输入框。改 XML 后重新构建安装，特殊字符需 XML 转义。见 [配置说明](APPSETTINGS.md)，文档不复制真实密钥。

MainPage/NativeScanPage：首页与良品区；LocationScanPage/LocationScanSession：库位；SkuSnScanPage/SkuSnSession：配对与恢复；PrintLabelPage：蓝牙打印；Services/AppSettings：配置；对应 ApiService：HTTP 和确认校验。

## 构建与验收

```powershell
dotnet build Scanner.AndroidTester/Scanner.AndroidTester.csproj -f net10.0-android
```

需 .NET 10 SDK、MAUI Android 工作负载与 Android SDK。测试入口 tests/AndroidTester.Tests、PalletScan.Tests、LocationScan.Tests、SkuSnBatch.Tests。设备验收覆盖边界、重复、连续回车、断网、错误密钥、分块失败、半对恢复、封存重试和真实蓝牙出纸；编译不能代替设备验收。
