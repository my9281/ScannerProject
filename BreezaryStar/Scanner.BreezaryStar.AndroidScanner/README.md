# Scanner.BreezaryStar.AndroidScanner

从 Scanner.AndroidTester 的 MAUI 平台与资源模板独立复制，只保留「扫码入库」新业务。应用标识为 `com.scanner.breezarystar.androidscanner`，不覆盖原 PDA 应用，不包含旧预扫描、检测、打印、库位或 SKU/SN 模块。

## Android 使用

### 精简安装包

正式安装使用 Release APK，默认只包含 ARM64 真机运行库，启用托管程序集裁剪并关闭 AOT 以减小体积。Debug 保留调试配置，不用作体积对比目标。已移除其他平台模板、未使用的图片和自定义字体，界面使用系统字体。

```powershell
dotnet build BreezaryStar/Scanner.BreezaryStar.AndroidScanner/Scanner.BreezaryStar.AndroidScanner.csproj -c Release
```

安装包位于工程 `bin/Release/net10.0-android/` 中的 `*-Signed.apk`。若 PDA 是旧款 32 位 ARM，构建时追加 `-p:RuntimeIdentifiers=android-arm`；x64 模拟器追加 `-p:RuntimeIdentifiers=android-x64`。包标识保持不变，覆盖安装可保留应用缓存；安装前不要卸载旧版。

1. 安装 APK，选择缓存位置 1–100，点击「开始扫描」。扫描期间位置固定；停止后可换位置。
2. 扫码枪设置为键盘输出，并以回车结束。也可输入后点击「记录当前条码」。不依赖摄像头或打印机。
3. 每条扫描立即写入设备应用数据目录的独立 JSON 文件，成功落盘后才显示已保存。相同内容再次扫描会生成另一条记录，保留每次扫描事件。
4. 停止扫描，打开「连接设置」，填写服务器 HTTPS 地址和上传密钥。密钥对应现有 Web `Upload:ApiKey`，保存到系统 SecureStorage；没有将原项目密钥复制进新应用。
5. 点击「上传全部待上传记录」，上传所有缓存位置的待上传条目，每批最多 200 条。服务器确认后标记已上传；断网、超时或异常确认不会清除未确认记录。
6. 本地保留已上传历史，页面展示最近 200 条和总数/待上传数。退出、进程重启后仍可读取缓存；卸载应用会删除应用数据。

每条记录包含独立 ScanId、持久 DeviceId、缓存位置、原始条码和扫描时间。仅移除末尾 CR/LF 扫码结束符，不做 SKU/SN 分类、条码内容去重或业务改写。设备时间需正确。

## Web 部署

1. 在 Scanner.Web 现有数据库执行 [019 建表脚本](../../Scanner.Web/Database/019_breezarystar_inbound_scans.sql)，创建 `breezarystar_inbound_scans`，兼容 MySQL 5.7.17。
2. 部署新版 Web，沿用原 Database 连接和 Upload:ApiKey 配置。
3. 管理员登录后，从账户页进入「BreezaryStar · 扫码入库记录」，或打开 `/breezary-inbound.html`，可按位置、扫描内容查询并加载更多。

接口：

- `POST /api/breezary-inbound-scans`，请求头 `X-Upload-Key`，请求 `{ "items": [...] }`；事务提交后才返回已确认记录 ID。ScanId 唯一，重复上传同一条记录不重复建行；相同 ID 携带不同内容返回 409。
- `GET /api/breezary-inbound-scans?limit=100&location=1&search=...&beforeId=...`，需要 Scanner 管理员 Bearer 会话。设备上传密钥不能授予网页查询权限。

扫码入库表与腾讯预报表分开保存，不自动修改腾讯文档。数据库扫描和上传时间均为 UTC，网页按浏览器本地时间显示。

## 验证

```powershell
dotnet build BreezaryStar/Scanner.BreezaryStar.AndroidScanner/Scanner.BreezaryStar.AndroidScanner.csproj -m:1 -p:UseSharedCompilation=false
dotnet run --project tests/BreezaryInbound.Tests/BreezaryInbound.Tests.csproj -p:BuildInParallel=false -p:UseSharedCompilation=false
```

自动测试使用临时设备缓存、模拟 HTTP 和内存服务器仓储，覆盖本地恢复、重复扫描、上传重试、分批确认和 Web 权限。未执行生产建表或上传；实际扫码枪和服务器 MySQL 需要部署后验收。
