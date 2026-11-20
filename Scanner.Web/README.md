# Scanner.Web 文档入口

首次安装与数据库/账号初始化、升级备份和排障统一见 [系统使用与维护](../docs/系统使用与维护.md)。旧结构快照不是完整新版初始化脚本，公开注册仍关闭。

BreezaryStar Android 扫码入库：新表脚本 `Database/019_breezarystar_inbound_scans.sql`，管理员查看页 `/breezary-inbound.html`；设备缓存、上传设置及接口见 [AndroidScanner 说明](../BreezaryStar/Scanner.BreezaryStar.AndroidScanner/README.md)。

BreezaryStar 腾讯文档：管理员授权及读取测试页 `/tencent-docs.html`；开放平台回调地址、服务器配置和权限要求见 [腾讯文档 OAuth 接入](TENCENT_DOCS.md)。

BreezaryStar 本地数据库、建表 SQL、定时拉取与修改推送见 [本地数据库与双向同步](BREEZARY_SYNC.md)。日常记录查询已改为读取本地 MySQL。

Web 端需求、页面状态、认证、完整业务接口目录、数据库脚本和部署要求已统一整理到 [总需求文档第 6–8 节](../request.md)。

技术补充：[账号与会话](WEB_LOGIN.md)、[检查单工单](WORK_ORDERS.md)、[库位](Database/warehouse_locations.md)、[SKU/SN 批次](Database/sku_sn_batches.md)。

WPF 更新包发布：管理员登录后打开 `/wpf-updates.html`，上传 WPF 发布 ZIP，服务器自动生成版本清单。配置、权限、IIS 上传限制及持久目录见 [WPF 发布与自动更新](../docs/WPF发布与自动更新.md)。

公开注册保持禁用；托盘目录 `/pallet-directory.html?ts=2026-10-01&te=2026-10-31` 和每托明细 `/pallet-data.html?t=2026-10-13&p=2` 可直接访问，无需上传密钥，主页不提供入口。根地址带 `?t=日期&p=托盘号` 会跳转至明细。页面按日期和托盘号动态显示 SKU/SN，上架后无需生成独立静态文件；现场 MySQL 5.7.17，库位建表命名 CHECK 兼容性仍需处理。当前实现状态以总文档为准，[旧说明](../docs/history/2026-10-09-Web说明-整理前.md) 保留供查阅。

良品区 PDA 托盘查询接口：`GET /api/tester-pallet-scans/lookup?sn=设备SN`，沿用 `X-Upload-Key` 认证。直接按 SN 查询 `tester_pallet_scans` 并返回对应托盘号（1–100）及扫描时间，不依赖库存或归档状态；重复 SN 取扫描时间最新、ID 最大的记录；未找到返回 404，非法 SN 返回 400。无需新增数据库脚本。部署及 PDA 验收步骤见 [AndroidTester 说明](../Scanner.ShippingCowAndroid/README.md#良品区托盘查询)。

## 域管理与实收记录

部署前在 Web 项目使用的 MySQL 数据库执行 `Database/025_managed_domains.sql`。管理员可在「我的工作台 → 管理员工具 → 域管理」创建域，在「创建用户」选择所属域，或在「用户与密码管理」为已有账户调整所属域。新建域由管理员分配成员，既有域保留原自助注册行为。

换域会撤销该用户的现有会话。PDA01 的扫描记录集中显示在「PDA 公共池」，所有已登录用户均可查看。所属域收货看板按承运商和运单号关联 Bluetti 域的有效预报，已匹配的公共扫描记录仅在 Bluetti 域看板显示，其余公共扫描记录在其他域看板显示。其他账号的扫描记录仅参与本域看板。

管理员可在工作台设置 Bluetti 拉取接口和腾讯文档接口。Bluetti 拉取配置保存到名为 Bluetti 的域。如原来在其他域配置过拉取，请在新入口重新保存并同步。公共池和看板复用现有的 `carrier_receipt_scans`、`bluetti_receiving_forecasts` 和 `user_domains` 表。可执行 `Database/026_receipt_board_match_index.sql` 为看板的预报匹配补充索引。
