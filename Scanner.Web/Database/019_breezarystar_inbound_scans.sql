-- Run in the existing Scanner.Web database; MySQL 5.7.17 compatible.
-- Creates a separate table for PDA inbound scans, without changing Tencent receipt data.
CREATE TABLE IF NOT EXISTS `breezarystar_inbound_scans` (
  `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `scan_id` CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `device_id` CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `cache_location` TINYINT UNSIGNED NOT NULL,
  `barcode` TEXT NOT NULL,
  `scanned_at` DATETIME(3) NOT NULL COMMENT 'UTC device scan time',
  `uploaded_at` DATETIME(3) NOT NULL COMMENT 'UTC first successful server upload time',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_breezary_inbound_scan` (`scan_id`),
  KEY `ix_breezary_inbound_location` (`cache_location`, `id`),
  KEY `ix_breezary_inbound_device` (`device_id`, `id`),
  KEY `ix_breezary_inbound_time` (`scanned_at`)
) ENGINE=InnoDB DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
-- Repeated barcode contents are allowed: each physical scan has a separate scan_id.
-- API validation restricts cache_location to 1-100 and barcode to 4096 characters.
