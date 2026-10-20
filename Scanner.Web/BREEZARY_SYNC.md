# BreezaryStar 本地数据库与双向同步

日常浏览器和 WPF 查询读取 Scanner.Web 的 MySQL 表，不调用腾讯文档。同步入库和推送是独立操作，可手动执行，也可由服务器后台定时执行。

## 先执行建表 SQL

在现有 Scanner.Web 数据库中执行完整文件：

`Scanner.Web/Database/017_breezarystar_receipts.sql`

脚本只创建 `breezarystar_receipts`，兼容 MySQL 5.7.17，不删除或修改旧表、不自动执行迁移。使用现有 `Database` 连接配置，不需另建数据库账号或填写腾讯凭据到数据库。

如果已经执行过较早版本的 017，请再执行 `Database/018_breezarystar_receipt_fields.sql`，补上实际字段；该补充脚本可重复执行。新建表使用最新 017 即已包含这些字段。更新部署后执行一次同步入库，已有记录的新增辅助列会按原始 JSON 重新填充。

实际业务字段映射：

| 腾讯字段 | MySQL 列 |
| --- | --- |
| SKU | sku |
| 工单号 | work_order_no |
| 运单号 | tracking_no |
| 实际签收快递单号 | actual_tracking_no |
| 渠道 | channel |
| 是否签收 | receipt_status |
| 状态流转 | workflow_status |
| 发货日期 | ship_date_ms |
| 签收日期 | receipt_date_ms |
| 备注 | remark |
| 问题描述 | issue_description |

其中“是否签收”“渠道”“状态流转”的辅助列保存显示文字，完整选项 ID、style 等保存在原始 JSON 中，推送仍发送正确选项对象。日期原值按毫秒保存，网页将发货/签收日期格式化显示。空富文本 format 和选项显示颜色差异不会被当作业务冲突。

表中保存：

- 原始腾讯 `file_id`、`sheet_id`、`record_id`；目标文档和工作表组成的 target_key 与 record_id 唯一，重复拉取不会重复插入。
- 上述业务列便于 SQL 查询。辅助查询列有长度上限，完整字段原值始终保存在 JSON 中。
- `remote_values_json`：最近从腾讯读取的字段原值。
- `pending_values_json`：本地修改、等待回写的字段；`base_values_json`：修改时对应的腾讯旧值。
- `sync_status`：clean / pending / error / conflict，版本号、最近拉取和推送时间及错误信息。DATETIME 使用 UTC。

## 部署和使用

1. 执行建表 SQL，再部署本次 Scanner.Web。
2. 保留已验证成功的 TencentDocs 三项凭据及现有 Database 连接配置。
3. 管理员打开 `/tencent-docs.html`，首次点击「从腾讯同步入库」。整个远端分页成功后才在一个事务中保存，失败不覆盖已有本地数据。
4. 此后页面打开时自动查看本地表，「查看本地数据」只刷新数据库结果，不调用腾讯。即便腾讯令牌到期，已入库数据仍可查看。
5. 管理员调试页可点击某行「编辑本地」，修改字段值 JSON，保留腾讯原来的字段值类型（单选项使用腾讯已有选项的原始结构，不凭空更改选项 ID）。保存后标记为待推送，不立即调用腾讯。
6. 点击「推送待同步改动」：只更新本地改过的字段，不发送整行其他字段；成功后清除该行待推送标记。
7. WPF 继续使用 Scanner 管理员账号登录并读取相同的本地数据，不再依赖腾讯在线状态。WPF 当前用于查询，编辑和同步管理在网页完成。

## 自动同步（避免每次手动操作）

在现有 `appsettings.Local.json` 的 `TencentDocs` 节**增加**下面三项，保留 ClientId / AccessToken / OpenId 等已有设置：

```json
{
  "TencentDocs": {
    "SyncEnabled": true,
    "SyncAutoPush": true,
    "SyncIntervalSeconds": 300
  }
}
```

修改后重启站点。后台在启动约 15 秒后开始，之后每 300 秒执行：先推送本地待同步改动，再拉取腾讯最新数据入库。没有待推送改动时不会执行推送前远端读取。间隔最低 30 秒、最高 86400 秒；数据多时每轮可能需要多次分页 API 调用，需结合接口额度调整。

默认 `SyncEnabled=false`，先执行建表 SQL、完成手动验证后再开启。若只想自动拉取而手动推送，设 `SyncAutoPush=false`。

后台任务随 Web 进程运行，站点停止时不会同步。IIS 部署需让应用池持续运行（例如 AlwaysRunning、关闭空闲回收并启用站点预加载）；回收重启后后台任务会恢复。多进程通过数据库 GET_LOCK 避免同目标的同步、编辑和推送重叠，页面遇到忙碌提示可稍后重试。

## 冲突、失败与重试

- 拉取只更新远端快照，保留尚未推送的本地改动，不因腾讯删除记录而删除本地历史。
- 推送前读取腾讯当前记录，逐字段比较其值与本地编辑时保存的旧值。远端修改了同一字段、字段消失或记录已删除时标记冲突，保留本地改动，不覆盖远端。
- 冲突行编辑窗口显示腾讯最新值；人工核对后，可以勾选“以其为基准重新提交”保存，再推送。远端被删除的记录不会被隐式重建。
- 腾讯更新失败保留待推送字段及错误信息，下次可重试。若腾讯已经成功、但本地确认保存失败，下次检查发现远端已是目标值会直接确认，避免重复更新。
- 本地编辑携带 version，过时版本返回 409，防止覆盖其他用户或后台同步产生的新数据。
- 腾讯接口未提供本文已确认可用的条件更新参数；读取核对与远端更新间仍存在较短竞争窗口。需要严格远端并发控制时应另行设计字段归属或业务锁，当前不能保证与腾讯客户端同时编辑同一字段的原子性。

## API（Scanner 管理员会话）

| 方法 | 路径 | 数据来源 / 作用 |
| --- | --- | --- |
| GET | `/api/tencent-docs/records?offset=0&limit=100` | MySQL 本地分页，返回 values、recordID、version、syncStatus、lastError |
| POST | `/api/tencent-docs/sync/pull` | 腾讯全量分页 → 本地事务保存 |
| PATCH | `/api/tencent-docs/records/{recordID}` | 仅本地修改；请求 `{ "version": 1, "values": { "备注": [{ "type": "text", "text": "已收货" }] } }`，字段格式以实际原值为准 |
| POST | `/api/tencent-docs/sync/push` | 推送所有待同步字段，返回 pushed / conflicts / failed |
| GET | `/api/tencent-docs/remote-records?offset=0&limit=100` | 诊断用直读腾讯，日常页面和 WPF 不使用 |

所有接口沿用 `system.configure` 管理员权限。当前支持已存在腾讯记录的本地修改及回写，不自动创建或删除腾讯记录。

验证：`tests/TencentDocs.Tests` 使用内存仓储及 HTTP 替身覆盖同步、权限、冲突和重试；真实 MySQL 建表、IIS 定时运行及腾讯实际写入需在服务器部署后验收。未在生产数据库执行 SQL，未向真实腾讯文档推送测试数据。
