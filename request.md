# 壹仓扫描系统：总需求与实现说明

整理日期：2026-10-09。本文统一记录 WPF、PDA、平板、Web 和原 Mac MAUI 工程，以当前代码为依据。“待接入”和“待验收”不代表已交付。历史详细扫码规则见 [原需求归档](docs/history/2026-10-09-需求文档-整理前.md)，其中旧工程名、运行时及平台状态由本文取代。

## 1. 工程与文档

| 工程 | 定位与状态 | 文档 |
| --- | --- | --- |
| Scanner.WPF | .NET 10 Windows WPF，主仓库工作台 | 本文第 3 节 |
| Scanner.AndroidTester | androidtest，原生 MAUI PDA 扫码、上传、打印 | [PDA 文档](Scanner.AndroidTester/README.md) |
| Scanner.CheckListBoard | 原生 MAUI 平板登录、权限、真实工单读写 | [平板文档](Scanner.CheckListBoard/README.md) |
| Scanner.Web | ASP.NET Core 10 Web 页面和统一业务 API | 本文第 6–8 节 |
| Scanner.MaUI | 原 Mac 迁移客户端，已接入业务，不是空模板 | 本文第 5 节 |
| Scanner.Models / Helpers / Controllers / ViewModels / DependencyInjection | 共享模型、业务规则、控制器和服务 | [共享工程](docs/shared-projects.md) |
| Scanner.Server.Model / BLL / DAL | 服务端模型、校验、数据库访问 | 本文第 7 节 |
| Scanner.Rongta.Android | 容大 Android 打印 SDK 绑定 | [SDK 文档](Scanner.Rongta.Android/README.md) |

## 2. 业务流程与共同要求

1. WPF 导入基础表和工单，支持扫码打印、SN 匹配、检测与上架。
2. PDA 扫描 SN 并关联托盘，上传良品区表；WPF 一次下载全部数据，在本地按托盘查询。
3. WPF 自动匹配良品区与基础资料，生成平板工单；平板下载当前用户、审核位为 0 的工单，保存完成状态。
4. PDA 扫描库位，或按 SKU → SN 配对生成批次；WPF 良品上架可下载批次继续匹配、导出、打印和上传。
5. 上架保存成功时，服务端在同一事务中按 SN 删除良品区表中的对应记录。

托盘号统一为整数 **1–100**。扫描内容去首尾空白，保留前导零。库位和 PDA 批次去重区分大小写；各模块日志、上传去重规则不能混为一套。业务本地日期与数据库 UTC 时间需区分。

WPF 标题、目录、按钮和提示提供中文、英文、西班牙语资源；业务比较用的状态值保持稳定。PDA 和平板共用壹仓蓝金标识，保留图像透明区域；PDA 黑色界面只保留简洁标题，平板保留背景图和半透明卡片。

## 3. WPF 功能目录

运行目标 `net10.0-windows10.0.17763.0`，不再将主程序写作 .NET Framework 4.7.2。扫码枪以键盘方式输入，Enter 提交；打印需要驱动和 Windows 默认打印机。

| 分组 | 入口 | 功能 |
| --- | --- | --- |
| 基础数据 | 导入基础表 | 导入共享基础资料，供 SN 匹配 |
| 基础数据 | 导入工单 | 导入紧急工单与备注 |
| 基础数据 | 手动刷新 | 更新工作台数据 |
| 入库 | 扫码打印 | 编号识别、日志、标签和语音 |
| 良品区 | 自动匹配 | 良品区匹配基础资料，生成并上传检查单工单 |
| 良品区 | 手动匹配 | 原入库检测，导入 SN、匹配和导出 |
| 良品区 | 替换标签 | 现有标签替换流程 |
| 良品区 | 查看良品区 | 全量下载，本地按托盘筛选 |
| 上架 | 维修标签 | 原 SKU/SN 标签入口 |
| 上架 | 良品上架 | 原出库检测，导入/下载 SKU/SN、匹配、导出、打印和上传 |
| 报告 | 日报查看 | 每日业务报告 |
| 报告 | 月报导出 | 月报导出；“余额宝”为已纠正的输入错误 |
| 报告 | 批量打印标签 | 批量标签打印 |

### 扫码打印

保留普通编号、OID、紧急工单、机型及日志规则。OID 包括 34 位数字、12 位 FedEx、1Z 编码和规范化后的长 420 编码；单独短 420 地区码拦截。OID 首次记录，后续按份数打印，并播报 O I D。标签支持 4×6 和 4×4。详细示例、状态和机型映射保留在历史需求与共享规则代码中。

客户端登录和本地扫描模式沿用原认证流程，不与平板 Web Bearer 登录混同。日志去重、重复扫码打印次数与 PDA 服务端唯一键分别处理。

### 良品区与上架

查看良品区在打开或刷新时调用 `GET /api/tester-pallet-scans`，一次获取全部数据；按托盘 1–100 本地查询。自动匹配工单每块最多 500 条，重试沿用稳定来源键。

良品上架先下载全部批次摘要，再按 GUID 下载选中批次。数据替换当前列表，并按基础表匹配 SN，继续导出、打印、上传。上传至 `POST /api/shelved-pallets` 后，服务器事务保存上架数据并删除 `tester_pallet_scans` 中相同 SN；仅下载或本地匹配不会删除。

## 4. PDA 与平板

### PDA AndroidTester

主入口：良品区扫描、打印标签、SKU / SN 扫描；基础资料组含库位扫描。

| 模块 | 记录规则 | 去重与恢复 |
| --- | --- | --- |
| 良品区扫描 | 托盘 1–100，SN、扫描 GUID、时间 | 本次运行内 SN 去重；服务端 SN 唯一更新归属；待上传数据仅在内存 |
| 库位扫描 | 所有扫码作为库位 ID | PDA 本次运行去重；服务端主键去重，保留首次时间和禁用位；待上传数据仅在内存 |
| SKU/SN | 第一枪 SKU、第二枪 SN，一对一条 | 同批 SN 去重，最多 5000 条；批次、半对和封存状态持久化 |
| 打印标签 | 本页当前输入生成普通标签 | 容大 RP425 蓝牙 TSPL/ZPL；发送确认不等于已出纸 |

每批 GUID 唯一；批次号按首次扫码本地时间 `yyyyMMddmmss`，**不含小时，可重复**，接口幂等和查询以 GUID 为准。有未配对 SKU 时禁止上传；上传封存后不可改动，失败原 GUID、原内容重试，回执核对后开启新批次。

三个上传模块共用嵌入 XML 中的地址和 Upload Key，修改需重新构建安装。详见 [PDA 完整文档](Scanner.AndroidTester/README.md)。

### 平板 CheckListBoard

已实现登录、角色权限和真实工单查看/保存。入库检测、安全检测、裸机检测、整机检测、查看评级、历史查看、系统配置仍为待接入页面。

工单下载当前用户审核位 0 的记录，分页拉取；以版本号保存完成状态。user/admin 按权限修改，viewer 只读。逐条保存，失败保留未成功项；重新下载和退出有未保存提示。会话仅内存，记住凭据使用 SecureStorage。详见 [平板完整文档](Scanner.CheckListBoard/README.md)。

## 5. 原 Mac MAUI 工程

`Scanner.MaUI` 已实现业务接入，与 PDA、平板是三个独立应用；不能假定菜单和 WPF 完全相同。

已实现：LoginPage、MainPage、OperationsPage 原生页面；认证与会话、扫码、日志、工单备注、语言切换、扫描文件上传；导入基础表和入库 SN 文本、导出匹配结果和当月待检测资料；导入 SKU/SN 文本、导出上架数据并上传；导入库位费用模板并导出费用结果。

Windows 使用标签绘制与默认打印机。Mac Catalyst 绘制 PDF，打开系统打印对话框，多份打印逐次交互；Android/iOS 分支不支持该工程的自动标签打印。

项目声明 Android、iOS、Mac Catalyst、Windows 目标。Mac 当前 runtime 为 `maccatalyst-x64`，支持平台版本 15.0（不是 macOS 产品版本的直接描述）；Apple Silicon 架构需另行核验。Mac 构建、签名、字体、条码、纸张、文件选择和打印需在具备 Apple 工具链的 Mac 上验收，Windows 编译不能代替此项。

## 6. Web 页面、认证与接口

ASP.NET Core 10，Controller → BLL → DAL，MySqlConnector 数据访问。默认地址 `https://wms.ymforever.com/`。提供主页和登录；注册相关页面/API、域列表、`/pallet-data.html`、`/pallet-directory.html` 当前被入口中间件返回 404，保留代码不代表公开页面可用。

两套认证分别使用：业务上传/下载使用 `X-Upload-Key` 对应 `Upload:ApiKey`，服务端未配置返回 503，错误或缺少密钥返回 401；平板账号与工单读写使用 Bearer 会话及权限。

账号：`POST /api/account/login`、`GET /api/account/me`、`POST /api/account/logout`。角色 admin/user/viewer，权限 checklist.read、checklist.write、system.configure。账号与域等原有表依赖需现场核验。

| 方法与路径 | 用途与规则 |
| --- | --- |
| GET /api/tester-pallet-scans | 全部良品区记录，Upload Key |
| POST /api/tester-pallet-scans | SN/托盘上传，每块最多 1000 条，Upload Key |
| POST /api/warehouse-locations | 库位上传，每块最多 1000 条，Upload Key |
| GET /api/warehouse-locations 或 /download | 全部库位 JSON，包括禁用位，Upload Key |
| POST /api/sku-sn-batches | 整批上传，最多 5000 条，Upload Key |
| GET /api/sku-sn-batches | 全部批次摘要，Upload Key |
| GET /api/sku-sn-batches/{guid} | 完整明细，不存在 404，Upload Key |
| POST /api/shelved-pallets | 保存上架并删除对应良品区 SN，Upload Key |
| GET /api/shelved-pallets/all | 全部上架数据，Upload Key |
| GET /api/shelved-pallets | 日期/托盘查询，数量限制 1–1000，Upload Key |
| POST /api/checklist-work-orders/batch | 生成工单，1–500 条，来源键去重，Upload Key |
| GET /api/checklist-work-orders | 当前用户审核位 0，afterId 分页，每页最多 200，Bearer + checklist.read |
| PUT /api/checklist-work-orders/{id} | 完成状态与 version，Bearer + checklist.write |
| POST /api/uploads/scans | multipart 扫描文本/JSON，支持 deviceName，Upload Key |
| GET /api/uploads、/{id}/content、/{id}/download | 文件列表、预览、下载，Upload Key |

另有健康检查 `/health`、`/health/database`、主页指标、订单及飞书控制器；具体契约和授权以对应代码为准，不作为新增客户端功能。

### 一致性与权限

工单上传默认关联启用的 my9281 账号与有效域，审核位 0、未完成。来源键重复跳过，不重置已完成状态。读取强制当前用户，即使管理员也不能以 userId 读取别人。保存校验归属、审核位、版本；成功版本加一，完成时间 UTC。冲突 409，失效 401，无权限 403。

SKU/SN 头与明细同事务保存；同 GUID 同内容幂等，不同内容 400。数据库时间 UTC，批次号保留 PDA 原值。上架保存与良品区删除同事务。可选飞书通知失败只记录日志，不回滚入库；旧公开托盘页被禁用，通知链接需部署验收。

专项技术补充：[账号](Scanner.Web/WEB_LOGIN.md)、[工单](Scanner.Web/WORK_ORDERS.md)、[库位](Scanner.Web/Database/warehouse_locations.md)、[批次](Scanner.Web/Database/sku_sn_batches.md)、[托盘](docs/托盘扫描上传.md)。旧“出库检测”入口现名“良品上架”。Web 总体需求以本文为入口。

## 7. 数据库与部署脚本

现场版本 **MySQL 5.7.17-log**。脚本在 `Scanner.Web/Database`，由维护人员手动部署，客户端不自动建表。按已有结构选择迁移，不盲目执行全部脚本。

| 脚本 | 用途 / 注意 |
| --- | --- |
| 001_tester_pallet_scans.sql | 良品区初建，旧 CHECK 定义需核验 5.7 兼容 |
| 002_tester_pallet_scans_unique_sn.sql | SN 唯一升级，先检查重复数据 |
| 003_user_sessions.sql | 会话表 |
| 004_checklist_work_orders.sql | 工单表 |
| 005_work_order_samples.sql | 可选样例，不重复写生产 |
| 006_work_order_sources.sql | 工单来源去重 |
| 007_warehouse_locations.sql | 库位表，当前命名 CHECK 写法仍需适配 5.7 |
| 008_tester_pallet_number_100.sql | 1–100 范围更新，无旧 CHECK 时跳过 |
| 009_sku_sn_batches.sql | 批次头和明细，适配 5.7 |

核心表：

- tester_pallet_scans：id、scan_id、sn、scan_date、pallet_number、scan_time；SN 唯一，良品区来源。
- warehouse_locations：location_id 主键、is_disabled 默认 0、scanned_at 首次 UTC 时间；0 启用/1 禁用。重复上传不修改首次时间或禁用位。当前无禁用管理界面/API，可维护 SQL 修改，下载包含禁用记录。
- sku_sn_batches：batch_id GUID 主键、batch_number、created_at、item_count。
- sku_sn_batch_items：batch_id、line_number、sku、sn；同批 SN 唯一，关联批次头。
- checklist_work_orders 及来源去重数据：用户归属、审核、完成状态、版本和来源键。
- shelved_pallet_data：上架结果，依赖现场已有结构；新增批次脚本不会创建此表。

MySQL 5.7 不执行 CHECK 范围约束，托盘和输入校验依赖客户端及服务端；唯一键和事务负责并发一致性。

## 8. 配置、构建与验收

Web：Database 的 Enabled、Server、Port、Database、UserId、Password；Upload 的 ApiKey、Directory、MaxBytes、MaxPreviewBytes；可选 FeishuRobot。密码和密钥不复制到文档，可通过 `Database__Password`、`Upload__ApiKey` 环境变量覆盖。默认上传 10 MiB、预览 1 MiB，目录需写权限。

PDA 使用嵌入 [appsettings.xml 配置](Scanner.AndroidTester/APPSETTINGS.md)；平板修改 HTTPS 地址并使用账号会话，不能用 Upload Key 代替登录。

```powershell
dotnet build Scanner.WPF/Scanner.WPF.csproj
dotnet build Scanner.Web/Scanner.Web.csproj
dotnet build Scanner.AndroidTester/Scanner.AndroidTester.csproj -f net10.0-android
dotnet build Scanner.CheckListBoard/Scanner.CheckListBoard.csproj -f net10.0-android
```

需 .NET 10 SDK；Android 另需 MAUI 工作负载、Android SDK；Mac 在 Mac 上针对 net10.0-maccatalyst 构建。编译通过不代表 APK 安装、真实打印或 SQL 部署。

验收：WPF 五组目录/三语提示、托盘边界、全量下载本地筛选、批次匹配上架；PDA 连续回车/去重/断网/回执、半对恢复与封存重试、真实出纸；平板角色/分页/到期/409/部分保存/未保存退出；Web 密钥/事务/并发去重/上架删除。

待解决或待验收：库位 SQL 5.7 兼容性；PDA 良品区/库位待上传数据重启丢失；库位禁用管理无界面；平板七个入口待接入；Mac 构建和真机打印。测试位于 tests，本文整理核对源码和文档，不新增业务功能，不将历史测试视为现场验收。
