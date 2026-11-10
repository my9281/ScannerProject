-- repairwms 结构归档：2026-10-09
-- 来源：用户提供的 repairwms.sql，MySQL 5.7.17-log
-- 仅包含表结构、索引、外键；不含表数据，不保留当前自增计数。
-- 先选择空数据库再执行；不是已有数据库的升级脚本，不要重复执行外键 ADD。
-- 不创建/切换数据库，不删除已有表。
SET NAMES utf8mb4;

-- user_domains
CREATE TABLE IF NOT EXISTS `user_domains` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT COMMENT '域ID',
  `domain_name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '域名称',
  `is_enabled` tinyint(1) NOT NULL DEFAULT '1' COMMENT '是否启用',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_user_domains_name` (`domain_name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='用户域' ;

-- app_users
CREATE TABLE IF NOT EXISTS `app_users` (
  `id` bigint(20) unsigned NOT NULL AUTO_INCREMENT COMMENT '用户ID',
  `uuid` char(36) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '用户UUID',
  `username` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT '登录用户名',
  `display_name` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '显示名称',
  `domain_id` int(10) unsigned NOT NULL COMMENT '所属域ID',
  `password_salt` varbinary(32) NOT NULL COMMENT '密码盐值',
  `password_hash` varbinary(64) NOT NULL COMMENT '密码哈希',
  `password_algorithm` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PBKDF2-SHA256' COMMENT '密码算法',
  `password_iterations` int(10) unsigned NOT NULL DEFAULT '100000' COMMENT 'PBKDF2迭代次数',
  `role` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'user' COMMENT '角色：admin/user/viewer',
  `status` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'active' COMMENT '状态：active/disabled/locked',
  `failed_login_count` int(10) unsigned NOT NULL DEFAULT '0' COMMENT '连续登录失败次数',
  `locked_until` datetime DEFAULT NULL COMMENT '锁定截止时间',
  `last_login_at` datetime DEFAULT NULL COMMENT '最后登录时间',
  `password_changed_at` datetime DEFAULT NULL COMMENT '密码修改时间',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_app_users_uuid` (`uuid`),
  UNIQUE KEY `uk_app_users_username` (`username`),
  KEY `idx_app_users_domain` (`domain_id`),
  KEY `idx_app_users_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Web系统用户' ;

-- user_sessions
CREATE TABLE IF NOT EXISTS `user_sessions` (
  `id` bigint(20) unsigned NOT NULL AUTO_INCREMENT COMMENT '会话ID',
  `user_id` bigint(20) unsigned NOT NULL COMMENT '用户ID',
  `token_hash` binary(32) NOT NULL COMMENT '会话令牌SHA-256哈希',
  `expires_at` datetime NOT NULL COMMENT '过期时间',
  `revoked_at` datetime DEFAULT NULL COMMENT '注销或撤销时间',
  `ip_address` varchar(45) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'IPv4或IPv6',
  `user_agent` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT '客户端信息',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `last_used_at` datetime DEFAULT NULL COMMENT '最后使用时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_user_sessions_token_hash` (`token_hash`),
  KEY `idx_user_sessions_user` (`user_id`),
  KEY `idx_user_sessions_expires` (`expires_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='用户登录会话' ;

-- orders
CREATE TABLE IF NOT EXISTS `orders` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `dtype` varchar(50) NOT NULL,
  `sn` varchar(50) NOT NULL,
  `doid` varchar(50) NOT NULL,
  `dtime` datetime NOT NULL,
  `dnote` varchar(50) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `id` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 ;

-- tester_pallet_scans
CREATE TABLE IF NOT EXISTS `tester_pallet_scans` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `scan_id` char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `sn` varchar(100) NOT NULL,
  `scan_date` date NOT NULL,
  `pallet_number` int(11) NOT NULL,
  `scan_time` datetime(3) NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_tester_pallet_scan_id` (`scan_id`),
  KEY `ix_tester_pallet_date` (`scan_date`,`pallet_number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 ;

-- warehouse_locations
CREATE TABLE IF NOT EXISTS `warehouse_locations` (
  `location_id` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
  `is_disabled` tinyint(1) NOT NULL DEFAULT '0' COMMENT '0=启用，1=禁用',
  `scanned_at` datetime(3) NOT NULL COMMENT '首次扫描时间 UTC',
  PRIMARY KEY (`location_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- checklist_work_orders
CREATE TABLE IF NOT EXISTS `checklist_work_orders` (
  `id` bigint(20) unsigned NOT NULL AUTO_INCREMENT,
  `user_id` bigint(20) unsigned NOT NULL COMMENT 'Assigned app_users.id',
  `device_id` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` varchar(1000) COLLATE utf8mb4_unicode_ci NOT NULL,
  `location` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '' COMMENT '位置，托盘编号字符串',
  `task_at` datetime(3) NOT NULL COMMENT 'Task time in UTC',
  `is_completed` tinyint(3) unsigned NOT NULL DEFAULT '0' COMMENT '0 incomplete, 1 complete',
  `completed_at` datetime(3) DEFAULT NULL,
  `audit_status` tinyint(3) unsigned NOT NULL DEFAULT '0' COMMENT 'Only 0 is distributable',
  `version` int(10) unsigned NOT NULL DEFAULT '0' COMMENT 'Optimistic concurrency version',
  `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  PRIMARY KEY (`id`),
  KEY `idx_work_orders_user_audit_id` (`user_id`,`audit_status`,`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci ;

-- checklist_work_order_sources
CREATE TABLE IF NOT EXISTS `checklist_work_order_sources` (
  `source_key` char(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `work_order_id` bigint(20) unsigned DEFAULT NULL,
  PRIMARY KEY (`source_key`),
  KEY `ix_work_order_source_order` (`work_order_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

-- sku_sn_batches
CREATE TABLE IF NOT EXISTS `sku_sn_batches` (
  `batch_id` char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `batch_number` char(12) NOT NULL,
  `created_at` datetime(3) NOT NULL,
  `item_count` int(11) NOT NULL,
  PRIMARY KEY (`batch_id`),
  KEY `ix_sku_sn_batch_time` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- sku_sn_batch_items
CREATE TABLE IF NOT EXISTS `sku_sn_batch_items` (
  `batch_id` char(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `line_number` int(11) NOT NULL,
  `sku` varchar(150) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
  `sn` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
  PRIMARY KEY (`batch_id`,`line_number`),
  UNIQUE KEY `uq_sku_sn_batch_sn` (`batch_id`,`sn`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- shelved_pallet_data
CREATE TABLE IF NOT EXISTS `shelved_pallet_data` (
  `uuid` char(36) NOT NULL COMMENT '唯一标识',
  `number` int(11) NOT NULL COMMENT '出库编号',
  `sn` varchar(100) NOT NULL COMMENT '产品序列号',
  `sku` varchar(150) NOT NULL COMMENT 'SKU',
  `type` varchar(100) DEFAULT NULL COMMENT '类型',
  `processing_method` varchar(100) DEFAULT NULL COMMENT '处理方式',
  `processing_time` datetime DEFAULT NULL COMMENT '处理时间',
  `shelving_date` date NOT NULL COMMENT '上架日期',
  `pallet_number` varchar(100) NOT NULL COMMENT '托盘号',
  `shelving_time` time NOT NULL COMMENT '上架时间',
  PRIMARY KEY (`uuid`),
  KEY `idx_shelved_pallet_sn` (`sn`),
  KEY `idx_shelved_pallet_sku` (`sku`),
  KEY `idx_shelved_pallet_number` (`pallet_number`),
  KEY `idx_shelved_pallet_date` (`shelving_date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='上架托盘数据';

-- 所有表建立后添加外键
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
