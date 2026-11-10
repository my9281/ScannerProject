# 库位扫描

部署服务端前执行 `Scanner.Web/Database/007_warehouse_locations.sql`，创建 `warehouse_locations` 表。
AndroidTester 首页 → 基础资料 → 1. 库位扫描。扫描内容全部作为库位 ID；去除首尾空白，保留前导零，大小写区分。PDA 本次运行去重（包含已成功上传记录），每批最多 1000 条；上传失败保留未确认数据。未上传数据仅保存在内存中，退出应用会丢失。

## 上传

`POST /api/warehouse-locations`

```json
{"items":[{"locationId":"A-001","scannedAt":"2026-10-08T09:00:00+08:00"}]}
```

返回 `{"acceptedLocationIds":["A-001"]}`。批内和数据库中已存在的库位自动去重；并发重复通过主键保护。已存在记录的首次扫描时间和禁用状态均保留，新库位默认启用。扫描时间存为 UTC。

## 下载

`GET /api/warehouse-locations/download`（也支持 `GET /api/warehouse-locations`），返回全部库位，包括已禁用记录：

```json
[{"locationId":"A-001","isDisabled":false,"scannedAt":"2026-10-08T01:00:00Z"}]
```

服务端配置上传密钥时，两种接口都需携带 `X-Upload-Key` 请求头，沿用现有 API 鉴权。

## 禁用标志

`is_disabled`: `0` 启用，`1` 禁用。可在数据库中维护：

```sql
UPDATE warehouse_locations SET is_disabled = 1 WHERE location_id = 'A-001';
```

重复扫描和上传不会覆盖禁用状态。
