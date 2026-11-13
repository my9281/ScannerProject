-- Execute before deploying the updated Web API and WPF/PDA clients.
CREATE TABLE IF NOT EXISTS repair_scan_records (
 id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
 scan_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 sn VARCHAR(50) NOT NULL,
 sku VARCHAR(50) NOT NULL,
 started_at DATETIME(3) NOT NULL COMMENT 'UTC: first code scanned',
 status VARCHAR(20) NOT NULL DEFAULT '开始维修',
 uploaded_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
 UNIQUE KEY uq_repair_scan_id (scan_id),
 KEY ix_repair_scan_sn (sn),
 KEY ix_repair_scan_started (started_at)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
