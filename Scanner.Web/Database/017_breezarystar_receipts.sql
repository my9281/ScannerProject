-- Creates one new table only; no data deletion or changes to existing tables.
CREATE TABLE IF NOT EXISTS `breezarystar_receipts` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `target_key` CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `file_id` VARCHAR(255) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `sheet_id` VARCHAR(128) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `record_id` VARCHAR(128) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `work_order_no` VARCHAR(160) NOT NULL DEFAULT '',
  `sku` VARCHAR(160) NOT NULL DEFAULT '',
  `tracking_no` VARCHAR(160) NOT NULL DEFAULT '',
  `actual_tracking_no` VARCHAR(160) NOT NULL DEFAULT '',
  `channel` VARCHAR(64) NOT NULL DEFAULT '',
  `receipt_status` VARCHAR(64) NOT NULL DEFAULT '',
  `workflow_status` VARCHAR(160) NOT NULL DEFAULT '',
  `ship_date_ms` BIGINT NULL,
  `receipt_date_ms` BIGINT NULL,
  `remark` TEXT NULL,
  `issue_description` TEXT NULL,
  `remote_values_json` LONGTEXT NOT NULL COMMENT 'Last pulled Tencent field values, preserving original types',
  `pending_values_json` LONGTEXT NOT NULL COMMENT 'Only locally changed fields awaiting push',
  `base_values_json` LONGTEXT NOT NULL COMMENT 'Original remote values of pending fields, for conflict checks',
  `sync_status` VARCHAR(16) CHARACTER SET ascii NOT NULL DEFAULT 'clean',
  `last_error` VARCHAR(1000) NOT NULL DEFAULT '',
  `version` BIGINT NOT NULL DEFAULT 1,
  `last_pulled_at` DATETIME(3) NULL,
  `last_pushed_at` DATETIME(3) NULL,
  `updated_at` DATETIME(3) NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_breezary_target_record` (`target_key`, `record_id`),
  KEY `ix_breezary_pending` (`target_key`, `sync_status`),
  KEY `ix_breezary_order` (`work_order_no`),
  KEY `ix_breezary_tracking` (`tracking_no`),
  KEY `ix_breezary_actual_tracking` (`actual_tracking_no`)
) ENGINE=InnoDB DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- All DATETIME values above are UTC. JSON is validated by the application;
-- no JSON functions, CHECK constraints, generated columns, or MySQL 8 features required.
