# PDA 库位 SKU 与配件数量

部署前先备份数据库，执行 `020_location_skus.sql`（依赖已有 `007_warehouse_locations.sql`），再更新服务端和 PDA。

库位扫描页面新增“库位核对绑定”。先上传创建的库位，再查询库位、扫描 SKU，建立一库位多 SKU 的映射。新绑定数量为 0，重复绑定不重置数量。SKU 按扫描代码精确匹配，目前不额外校验外部 SKU 主数据。

首页新增“上架配件”。填写操作人，扫描库位查看 SKU 和当前数量；扫描已绑定 SKU 核对后，填写正整数数量，点 ↑ 增加或 ↓ 减少。每次保存后需重新扫描 SKU。禁用库位不可绑定或修改，未绑定 SKU 不可修改，数量不能为负。

`warehouse_location_skus` 保存库位、SKU、数量；`warehouse_location_sku_history` 保存绑定/调整、操作编号、操作人、前后数量、增减数和服务器 UTC 时间。操作人来自 PDA 手填，不代表已验证的账号身份。数量和日志在同一事务中提交，库位行锁串行化并发操作，操作编号防止重复提交。

接口沿用 `X-Upload-Key`：GET `api/warehouse-locations/skus?locationId=...`；POST `api/warehouse-locations/skus/bind` 或 `adjust`，请求字段 `locationId, sku, delta, operationId, operator`。绑定 delta 为 0，调整为非零整数。未确认请求本地保留，同一编号重试；不要清除应用数据。日志可按库位、SKU、时间查询数据库历史表。

验收：创建并上传库位；绑定两种 SKU；重复绑定不清零；扫描错误 SKU 阻止调整；增加 3 后减少 2 为 1；再减少 2 拒绝；禁用库位拒绝操作；断网恢复重试只入账一次；核对历史表数量和操作人。正式使用前需真实 MySQL 与 PDA 联调。
