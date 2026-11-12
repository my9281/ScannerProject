# PDA 扫描记录

先执行 `021_repair_scan_records.sql`，再部署 Web 与 PDA。新表 `repair_scan_records` 独立于现有 SKU/SN 批次、良品和工单表。

首页仅保留良品区扫描、托盘查询及新增扫描记录。扫描记录页面明确区分 SN 和 SKU，支持分别清除；两项全部清空后重置首扫时间。每项去首尾空白后为 1–50 字符，保留前导零。首个字段开始录入时记录设备时间，上传时以 UTC 毫秒保存为 `started_at`，状态由服务器固定写为“开始维修”。设备需保持时间准确。

`POST /api/repair-scans`，使用现有 `X-Upload-Key`，JSON 字段为 `scanId`、`sn`、`sku`、`startedAt`。同一个 scanId 重试确认已有记录，不重复插入；内容冲突返回 409。不同扫描可以记录同一 SN 的多次开始维修。

点击上传前先持久化本次记录。未确认成功时保留内容并禁止编辑，使用相同编号重试；重启后恢复待上传记录。成功确认后清空，继续扫描下一台。尚未点击上传的输入在离开页面后不保留。

验证：`dotnet run --project tests/RepairScan.Tests`；数据库往返、断网重试和 PDA 扫码焦点需在部署环境验收。
