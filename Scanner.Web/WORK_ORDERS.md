# 用户工单下载与更新

## WPF 自动匹配批量上传

部署前依次执行 `Database/004_checklist_work_orders.sql` 和 `Database/006_work_order_sources.sql`（已创建工单表只需执行 006）。006 新建来源去重表，不修改 app_users 或现有工单字段。确认 my9281 已存在、状态 active、所属域启用。

`POST /api/checklist-work-orders/batch` 使用现有 `X-Upload-Key`，不是平板登录令牌。请求例：

```json
{"items":[{"sourceKey":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","deviceId":"SN001","description":"托盘号：2；SKU：ABC；RMA：R001"}]}
```

每批 1–500 条。sourceKey 是调用方提供的稳定 64 位小写十六进制来源标识，重试必须保留相同值。设备 ID 最长 100 字，描述最长 1000 字。服务端固定分配 my9281，不接受请求指定用户或审核状态；新任务审核位 0、未完成、任务时间为上传时 UTC。返回 `insertedCount`、`skippedCount`、`userId`、`username`。单批在事务中整体提交，任何失败回滚。

WPF 点击“获取并匹配”后自动上传所有匹配出的待检测明细；SN 写入设备 ID，描述保留托盘、SKU、RMA、类型、检测状态、扫描日期/时间和处理时间。超过 500 条分批上传，失败保留结果，点击“重试上传工单”重新发送，相同来源不会再次生成工单，也不会覆盖完成或审核状态。来源标识由匹配记录内容及同内容记录出现次数生成；相同结果再次匹配会跳过，记录内容变化会视为新来源。扫描时间保留基础数据格式，不转为任务时间。

沿用 WPF 的 UploadBaseUrl / UploadApiKey，部署服务端新版本及 SQL 后重启 WPF。此功能不自动创建默认用户。

验证：运行 `dotnet run --project tests/AutomaticMatching.Tests` 和 `dotnet run --project tests/WebAccount.Tests`。数据库联调时先匹配上传，检查工单 user_id 对应 my9281、audit_status=0、is_completed=0；再次匹配确认新增为 0；用 my9281 登录平板刷新查看，再修改某任务审核位为 1，确认刷新后该任务不下发。自动化测试使用模拟 HTTP，不连接生产数据库。

## 数据库

先在 app_users 所在数据库执行 `Database/004_checklist_work_orders.sql`。不修改用户表。新表 checklist_work_orders 字段：

| 字段 | 含义 |
| --- | --- |
| id | 工单 ID，BIGINT UNSIGNED |
| user_id | 关联 app_users.id，工单所属用户 |
| device_id / description | 设备 ID、描述 |
| task_at | 任务时间，UTC |
| is_completed | 0 未完成、1 已完成 |
| completed_at | 完成时间；切到 1 时设置，切回 0 时清除 |
| audit_status | 审核位，只有 0 可以下发，任何非 0 均不下发 |
| version | 并发版本，从 0 开始，每次 API 更新加 1 |
| created_at / updated_at | 创建及更新时间，UTC |

可选测试数据脚本 `Database/005_work_order_samples.sql` 会为 my9281 插入十条测试任务；没有该用户时不插入，重复执行会再次插入十条。字段保持序号、设备ID、描述、完成开关的界面布局，序号由平板按下载结果生成，不作为数据库任务标识。

## 身份

沿用原有 Bearer 会话及 AccountPermission，不使用设备 API Key。登录及 me 响应新增 userId，取自 app_users.id；平板保存为当前会话 UserId。服务端从已验证会话取得用户 ID，更新时不信任客户端提供的归属。即便 admin，也不能通过这些平板接口查询或修改其他用户的任务。

## 下载

```http
GET /api/checklist-work-orders?userId=42&afterId=0
Authorization: Bearer <token>
```

需要 checklist.read。userId 可省略，省略时取会话用户 ID；提供其他用户 ID 返回 403。SQL 强制 `user_id=会话用户ID AND audit_status=0`。每页最多 200 条，按 ID 升序；取得 200 条后用该页最大 id 作为下一页 afterId，直到不足 200 条。平板已实现此分页下载流程。

响应为 JSON 数组，字段 id、userId、deviceId、description、isCompleted、taskAt、completedAt、updatedAt、version；时间均为 UTC ISO 格式。

## 更新

```http
PUT /api/checklist-work-orders/123
Authorization: Bearer <token>
Content-Type: application/json

{"isCompleted":true,"version":0}
```

需要 checklist.write。isCompleted 和 version 必填，false 和 0 均为合法值。接口只更新完成状态、完成时间、更新时间及版本，不能修改任务归属或审核位。WHERE 条件同时检查 id、会话 user_id、audit_status=0、version；事务提交后返回更新后的整条任务，供平板更新版本号。

无记录、非本人、审核位变成非 0、版本过旧均返回相同 409 提示，不泄露其他用户工单。无有效会话返回 401；viewer 更新返回 403；输入缺失返回 400。响应禁止缓存。

平板保存只提交发生变化的行，逐条更新；部分成功时保留成功行的新版本，其余修改仍保留并提示，不会声称全部成功。这不是整表原子批量保存。重复下载会清空已退出下发范围的工单；下载失败不会把服务器数据替换成本地示例。

## 审核及分配

审核位默认 0，遵循用户指定的下发规则。未增加管理审核接口，审核由现有管理操作或 SQL 设置。修改审核位或重新分配时同时增加版本：

```sql
UPDATE checklist_work_orders
SET audit_status=1, version=version+1
WHERE id=123;

UPDATE checklist_work_orders
SET user_id=42, audit_status=0, version=version+1
WHERE id=123;
```

审核转为非 0 后，已下载的旧任务也不能保存；再次下载会移除此任务。只设置 audit_status 为非 0 而未增加版本仍会阻止保存，但所有管理内容变更建议增加 version 以拒绝陈旧数据。

## 验证

Web/Windows 编译、WebAccount.Tests 的会话/用户 ID 及接口契约检查、CheckListBoard.Auth.Tests 的下载/更新 HTTP 契约检查均已执行。自动接口检查采用替身服务；实际 MySQL DDL、过滤和事务需在测试库验证，未在真实数据库执行脚本。

测试库验收：创建用户 A/B，为 A 放入审核位 0 和非 0 的任务，为 B 放入审核位 0 的任务。A 下载只应得到自己的审核位 0 任务；A 传 B 的用户 ID 必须 403。A 保存自己的任务成功并更新 completed_at/version；切回未完成清空 completed_at。改用过期版本、别人的工单 ID 或停止下发的工单 ID，应返回 409 且数据库无变化。viewer 下载成功但更新 403。两台平板用同一版本同时更新，应只有一次成功。超过 200 条时验证分页不重复。缺失完成状态或版本号应 400。

部署顺序：建表 → 可选测试数据 → 部署新版 Scanner.Web → 安装新版平板程序 → 重新登录取得 userId → 下载工单。平板尚未取得 userId 时会提示部署新版服务端并重新登录。
