# repairwms 数据库结构档案

归档日期：2026-10-09。来源为用户提供的 `repairwms.sql`，导出时间 2026-10-09 23:35:08，服务器 MySQL 5.7.17-log。只归档结构，不复制业务数据、用户凭据、会话内容或原始含数据导出。

## 结构快照与使用

[纯结构 SQL](repairwms-schema-2026-10-09.sql) 包含 11 张表、原有索引及 5 个外键。保留字段类型、默认值、字符集、排序规则和字段注释；移除 INSERT 和当前表级 AUTO_INCREMENT 计数，保留自增字段属性。按依赖顺序建表，最后添加外键。

在已选择的空数据库执行，不自动创建/切换数据库，也不删除已有表。不是升级迁移脚本：CREATE IF NOT EXISTS 不会修正已有表，重复执行外键 ADD 会报重复约束。已有数据库按主文档选择迁移。空库没有账号/域数据，部署应用仍需单独初始化合法账号与域。

## 表目录

| 表 | 用途 |
| --- | --- |
| user_domains | 账号域与启用状态 |
| app_users | Web 用户、密码校验和角色 |
| user_sessions | Bearer 会话 |
| orders | 现有订单 API |
| tester_pallet_scans | PDA 良品区 SN/托盘 |
| warehouse_locations | 库位与禁用标记 |
| checklist_work_orders | 平板工单、位置、完成和审核状态 |
| checklist_work_order_sources | WPF 自动匹配来源去重 |
| sku_sn_batches | SKU/SN 批次头 |
| sku_sn_batch_items | SKU/SN 配对明细 |
| shelved_pallet_data | 上架结果 |

## 字段与索引字典

### user_domains

| 字段 | SQL 定义 |
| --- | --- |
| id | int(10) unsigned NOT NULL AUTO_INCREMENT COMMENT '域ID' |
| domain_name | varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '域名称' |
| is_enabled | tinyint(1) NOT NULL DEFAULT '1' COMMENT '是否启用' |
| created_at | datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间' |
| updated_at | datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间' |

索引：

- `PRIMARY KEY (id)`
- `UNIQUE KEY uk_user_domains_name (domain_name)`

### app_users

| 字段 | SQL 定义 |
| --- | --- |
| id | bigint(20) unsigned NOT NULL AUTO_INCREMENT COMMENT '用户ID' |
| uuid | char(36) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '用户UUID' |
| username | varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '登录用户名' |
| display_name | varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '显示名称' |
| domain_id | int(10) unsigned NOT NULL COMMENT '所属域ID' |
| password_salt | varbinary(32) NOT NULL COMMENT '密码盐值' |
| password_hash | varbinary(64) NOT NULL COMMENT '密码哈希' |
| password_algorithm | varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PBKDF2-SHA256' COMMENT '密码算法' |
| password_iterations | int(10) unsigned NOT NULL DEFAULT '100000' COMMENT 'PBKDF2迭代次数' |
| role | varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'user' COMMENT '角色：admin/user/viewer' |
| status | varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'active' COMMENT '状态：active/disabled/locked' |
| failed_login_count | int(10) unsigned NOT NULL DEFAULT '0' COMMENT '连续登录失败次数' |
| locked_until | datetime DEFAULT NULL COMMENT '锁定截止时间' |
| last_login_at | datetime DEFAULT NULL COMMENT '最后登录时间' |
| password_changed_at | datetime DEFAULT NULL COMMENT '密码修改时间' |
| created_at | datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间' |
| updated_at | datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间' |

索引：

- `PRIMARY KEY (id)`
- `UNIQUE KEY uk_app_users_uuid (uuid)`
- `UNIQUE KEY uk_app_users_username (username)`
- `KEY idx_app_users_domain (domain_id)`
- `KEY idx_app_users_status (status)`

### user_sessions

| 字段 | SQL 定义 |
| --- | --- |
| id | bigint(20) unsigned NOT NULL AUTO_INCREMENT COMMENT '会话ID' |
| user_id | bigint(20) unsigned NOT NULL COMMENT '用户ID' |
| token_hash | binary(32) NOT NULL COMMENT '会话令牌SHA-256哈希' |
| expires_at | datetime NOT NULL COMMENT '过期时间' |
| revoked_at | datetime DEFAULT NULL COMMENT '注销或撤销时间' |
| ip_address | varchar(45) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'IPv4或IPv6' |
| user_agent | varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '客户端信息' |
| created_at | datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间' |
| last_used_at | datetime DEFAULT NULL COMMENT '最后使用时间' |

索引：

- `PRIMARY KEY (id)`
- `UNIQUE KEY uk_user_sessions_token_hash (token_hash)`
- `KEY idx_user_sessions_user (user_id)`
- `KEY idx_user_sessions_expires (expires_at)`

### orders

| 字段 | SQL 定义 |
| --- | --- |
| id | int(11) NOT NULL AUTO_INCREMENT |
| dtype | varchar(50) NOT NULL |
| sn | varchar(50) NOT NULL |
| doid | varchar(50) NOT NULL |
| dtime | datetime NOT NULL |
| dnote | varchar(50) NOT NULL |

索引：

- `PRIMARY KEY (id)`
- `KEY id (id)`

### tester_pallet_scans

| 字段 | SQL 定义 |
| --- | --- |
| id | bigint(20) NOT NULL AUTO_INCREMENT |
| scan_id | char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL |
| sn | varchar(100) NOT NULL |
| scan_date | date NOT NULL |
| pallet_number | int(11) NOT NULL |
| scan_time | datetime(3) NOT NULL |

索引：

- `PRIMARY KEY (id)`
- `UNIQUE KEY uq_tester_pallet_scan_id (scan_id)`
- `KEY ix_tester_pallet_date (scan_date,pallet_number)`

### warehouse_locations

| 字段 | SQL 定义 |
| --- | --- |
| location_id | varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL |
| is_disabled | tinyint(1) NOT NULL DEFAULT '0' COMMENT '0=启用，1=禁用' |
| scanned_at | datetime(3) NOT NULL COMMENT '首次扫描时间 UTC' |

索引：

- `PRIMARY KEY (location_id)`

### checklist_work_orders

| 字段 | SQL 定义 |
| --- | --- |
| id | bigint(20) unsigned NOT NULL AUTO_INCREMENT |
| user_id | bigint(20) unsigned NOT NULL COMMENT 'Assigned app_users.id' |
| device_id | varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL |
| description | varchar(1000) COLLATE utf8mb4_unicode_ci NOT NULL |
| location | varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '' COMMENT '位置，托盘编号字符串' |
| task_at | datetime(3) NOT NULL COMMENT 'Task time in UTC' |
| is_completed | tinyint(3) unsigned NOT NULL DEFAULT '0' COMMENT '0 incomplete, 1 complete' |
| completed_at | datetime(3) DEFAULT NULL |
| audit_status | tinyint(3) unsigned NOT NULL DEFAULT '0' COMMENT 'Only 0 is distributable' |
| version | int(10) unsigned NOT NULL DEFAULT '0' COMMENT 'Optimistic concurrency version' |
| created_at | datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) |
| updated_at | datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3) |

索引：

- `PRIMARY KEY (id)`
- `KEY idx_work_orders_user_audit_id (user_id,audit_status,id)`

### checklist_work_order_sources

| 字段 | SQL 定义 |
| --- | --- |
| source_key | char(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL |
| work_order_id | bigint(20) unsigned DEFAULT NULL |

索引：

- `PRIMARY KEY (source_key)`
- `KEY ix_work_order_source_order (work_order_id)`

### sku_sn_batches

| 字段 | SQL 定义 |
| --- | --- |
| batch_id | char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL |
| batch_number | char(12) NOT NULL |
| created_at | datetime(3) NOT NULL |
| item_count | int(11) NOT NULL |

索引：

- `PRIMARY KEY (batch_id)`
- `KEY ix_sku_sn_batch_time (created_at)`

### sku_sn_batch_items

| 字段 | SQL 定义 |
| --- | --- |
| batch_id | char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL |
| line_number | int(11) NOT NULL |
| sku | varchar(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL |
| sn | varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL |

索引：

- `PRIMARY KEY (batch_id,line_number)`
- `UNIQUE KEY uq_sku_sn_batch_sn (batch_id,sn)`

### shelved_pallet_data

| 字段 | SQL 定义 |
| --- | --- |
| uuid | char(36) NOT NULL COMMENT '唯一标识' |
| number | int(11) NOT NULL COMMENT '出库编号' |
| sn | varchar(100) NOT NULL COMMENT '产品序列号' |
| sku | varchar(150) NOT NULL COMMENT 'SKU' |
| type | varchar(100) DEFAULT NULL COMMENT '类型' |
| processing_method | varchar(100) DEFAULT NULL COMMENT '处理方式' |
| processing_time | datetime DEFAULT NULL COMMENT '处理时间' |
| shelving_date | date NOT NULL COMMENT '上架日期' |
| pallet_number | varchar(100) NOT NULL COMMENT '托盘号' |
| shelving_time | time NOT NULL COMMENT '上架时间' |

索引：

- `PRIMARY KEY (uuid)`
- `KEY idx_shelved_pallet_sn (sn)`
- `KEY idx_shelved_pallet_sku (sku)`
- `KEY idx_shelved_pallet_number (pallet_number)`
- `KEY idx_shelved_pallet_date (shelving_date)`

## 外键

```sql
ALTER TABLE `app_users`
  ADD CONSTRAINT `fk_app_users_domain` FOREIGN KEY (`domain_id`) REFERENCES `user_domains` (`id`) ON UPDATE CASCADE;

ALTER TABLE `checklist_work_orders`
  ADD CONSTRAINT `fk_work_orders_user` FOREIGN KEY (`user_id`) REFERENCES `app_users` (`id`) ON UPDATE CASCADE;

ALTER TABLE `checklist_work_order_sources`
  ADD CONSTRAINT `fk_work_order_source_order` FOREIGN KEY (`work_order_id`) REFERENCES `checklist_work_orders` (`id`) ON DELETE CASCADE;

ALTER TABLE `sku_sn_batch_items`
  ADD CONSTRAINT `fk_sku_sn_batch` FOREIGN KEY (`batch_id`) REFERENCES `sku_sn_batches` (`batch_id`);

ALTER TABLE `user_sessions`
  ADD CONSTRAINT `fk_user_sessions_user` FOREIGN KEY (`user_id`) REFERENCES `app_users` (`id`) ON DELETE CASCADE ON UPDATE CASCADE;
```

## 与需求的对应关系

- 工单 location 已为 VARCHAR(100)，保存托盘编号字符串，描述与位置独立。
- is_completed 是平板完成状态；audit_status 是上架过滤标记，业务代码在写入 shelved_pallet_data 时按 SN 设置为 1，平板只下载 0。该行为由服务端事务实现，快照没有触发器。
- 库位 is_disabled 为 0/1，当前现场建表没有 CHECK，适配 MySQL 5.7；范围由业务校验负责。
- 良品区 pallet_number 为整数，上架表 pallet_number 为 VARCHAR(100)，不要混淆。
- 批次 GUID 唯一，12 字符批次号 yyyyMMddmmss 可以重复。
- 当前约束未包含 CHECK，不能从结构快照推断数据库自动校验托盘 1–100。

[总需求与部署](../../request.md) · [增量 SQL](../../Scanner.Web/Database)

## 表结构审查

[2026-10-09 问题与解决方案](2026-10-09-表结构审查与方案.md) 覆盖 11 张表及相关代码，整理 27 项发现和改进建议；[只读预检 SQL](repairwms-review-checks.sql) 用于现场确认，尚未连接生产库执行。

重要修正：实际快照的 tester_pallet_scans 没有 SN 唯一索引，当前代码依赖的按 SN 去重尚缺数据库约束。主需求中“SN 唯一”属于设计目标，不代表现场已经部署。

## 保留数据修复

代码及新增迁移见 [修复与部署记录](保留数据修复与部署.md)。良品区采用归档标记，不删除业务记录；上架请求幂等、返修周期、SN 原值与不敏感匹配、库位审计已接入。迁移尚未在现场执行，问题逐项状态和保留边界在记录中。
