# SKU / SN 批次

Android test：SKU / SN 扫描 → 先扫 SKU，再扫 SN → 上传批次。
每对扫码形成一条明细，同批 SN 去重，最多 5000 条。未完成配对不能上传；重扫 SKU 可取消当前未配对的 SKU。
每批以 GUID 唯一标识，批次号按首次扫码的本地时间 `yyyyMMddmmss` 生成（不含小时，可重复）。上传后开启新批次。
批次及未完成配对保存在应用私有目录，上传失败可用相同 GUID 重试；封存后禁止修改，避免重试内容不一致。

部署前执行 `009_sku_sn_batches.sql`，兼容 MySQL 5.7。

- `POST /api/sku-sn-batches` 上传整批 `{batchId,batchNumber,createdAt,items:[{sku,sn}]}`；成功返回 `{batchId,itemCount}`。
- `GET /api/sku-sn-batches` 返回全部批次摘要 `{batchId,batchNumber,createdAt,itemCount}`。
- `GET /api/sku-sn-batches/{guid}` 返回批次及完整明细，不存在返回 404。

所有接口携带 `X-Upload-Key`。服务端在同一事务中保存批次和明细，相同 GUID 相同内容重传不重复入库；同 GUID 不同内容返回 400。时间在数据库保存为 UTC。

WPF：上架 → 良品上架（原出库检测）→ 下载批次 → 选择批次 → 下载。下载数据替代当前检测列表，按全局基础表匹配 SN，继续使用原有导出、打印和上架上传功能。
