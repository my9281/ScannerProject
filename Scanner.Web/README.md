# Scanner.Web 文档入口

首次安装与数据库/账号初始化、升级备份和排障统一见 [系统使用与维护](../docs/系统使用与维护.md)。旧结构快照不是完整新版初始化脚本，公开注册仍关闭。

BreezaryStar Android 扫码入库：新表脚本 `Database/019_breezarystar_inbound_scans.sql`，管理员查看页 `/breezary-inbound.html`；设备缓存、上传设置及接口见 [AndroidScanner 说明](../BreezaryStar/Scanner.BreezaryStar.AndroidScanner/README.md)。

BreezaryStar 腾讯文档：管理员授权及读取测试页 `/tencent-docs.html`；开放平台回调地址、服务器配置和权限要求见 [腾讯文档 OAuth 接入](TENCENT_DOCS.md)。

BreezaryStar 本地数据库、建表 SQL、定时拉取与修改推送见 [本地数据库与双向同步](BREEZARY_SYNC.md)。日常记录查询已改为读取本地 MySQL。

Web 端需求、页面状态、认证、完整业务接口目录、数据库脚本和部署要求已统一整理到 [总需求文档第 6–8 节](../request.md)。

技术补充：[账号与会话](WEB_LOGIN.md)、[检查单工单](WORK_ORDERS.md)、[库位](Database/warehouse_locations.md)、[SKU/SN 批次](Database/sku_sn_batches.md)。

WPF 更新包发布：管理员登录后打开 `/wpf-updates.html`，上传 WPF 发布 ZIP，服务器自动生成版本清单。配置、权限、IIS 上传限制及持久目录见 [WPF 发布与自动更新](../docs/WPF发布与自动更新.md)。

公开注册保持禁用；托盘目录 `/pallet-directory.html?ts=2026-10-01&te=2026-10-31` 和每托明细 `/pallet-data.html?t=2026-10-13&p=2` 可直接访问，无需上传密钥，主页不提供入口。根地址带 `?t=日期&p=托盘号` 会跳转至明细。页面按日期和托盘号动态显示 SKU/SN，上架后无需生成独立静态文件；现场 MySQL 5.7.17，库位建表命名 CHECK 兼容性仍需处理。当前实现状态以总文档为准，[旧说明](../docs/history/2026-10-09-Web说明-整理前.md) 保留供查阅。
