-- MySQL 5.7.17：保留数据迁移，先停写并备份，再在 Web 数据库执行。
-- 不 DELETE/TRUNCATE/DROP 表，不改变历史时间/字符集。
-- DDL 隐式提交，失败必须停止，查明后重试，不能依赖整体回滚。
SET time_zone='+00:00';
CREATE TABLE IF NOT EXISTS schema_migrations (
 migration_id VARCHAR(100) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
 applied_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
) ENGINE=InnoDB;
CREATE TABLE IF NOT EXISTS shelving_requests (
 request_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
 content_hash CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 result_json LONGTEXT NULL,
 created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS pallet_scan_receipts (
 scan_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
 sn VARCHAR(100) NOT NULL,
 content_hash CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NULL,
 is_processed TINYINT(1) NOT NULL DEFAULT 0,
 created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 使用动态 SQL 避免重复增加字段。字段仅由本迁移创建，已有同名字段需核验定义。
SET @sql=IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='tester_pallet_scans' AND COLUMN_NAME='is_archived')=0,
 'ALTER TABLE tester_pallet_scans ADD COLUMN is_archived TINYINT(1) NOT NULL DEFAULT 0',
 'SELECT ''is_archived already exists''');
PREPARE migration_stmt FROM @sql; EXECUTE migration_stmt; DEALLOCATE PREPARE migration_stmt;
SET @sql=IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='tester_pallet_scans' AND COLUMN_NAME='archived_reason')=0,
 'ALTER TABLE tester_pallet_scans ADD COLUMN archived_reason VARCHAR(30) NULL',
 'SELECT ''archived_reason already exists''');
PREPARE migration_stmt FROM @sql; EXECUTE migration_stmt; DEALLOCATE PREPARE migration_stmt;
SET @sql=IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='tester_pallet_scans' AND COLUMN_NAME='archived_at')=0,
 'ALTER TABLE tester_pallet_scans ADD COLUMN archived_at DATETIME(3) NULL',
 'SELECT ''archived_at already exists''');
PREPARE migration_stmt FROM @sql; EXECUTE migration_stmt; DEALLOCATE PREPARE migration_stmt;

-- 保留重复行，只标记归档；现有列比较规则下保留扫描时间最新/id 最大者。
CREATE TEMPORARY TABLE IF NOT EXISTS scanner_migration_pallet_keep (
 sn VARCHAR(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci PRIMARY KEY,
 keep_id BIGINT NOT NULL
) ENGINE=InnoDB;
INSERT INTO scanner_migration_pallet_keep (sn,keep_id)
SELECT p.sn COLLATE utf8mb4_unicode_ci,MAX(p.id)
FROM tester_pallet_scans p
JOIN (
 SELECT sn COLLATE utf8mb4_unicode_ci AS sn_key,MAX(scan_time) AS newest_time
 FROM tester_pallet_scans WHERE is_archived=0
 GROUP BY sn COLLATE utf8mb4_unicode_ci
) newest ON p.sn COLLATE utf8mb4_unicode_ci=newest.sn_key AND p.scan_time=newest.newest_time
WHERE p.is_archived=0
GROUP BY p.sn COLLATE utf8mb4_unicode_ci
ON DUPLICATE KEY UPDATE keep_id=VALUES(keep_id);
UPDATE tester_pallet_scans p
JOIN scanner_migration_pallet_keep k ON p.sn COLLATE utf8mb4_unicode_ci=k.sn
SET p.is_archived=1,p.archived_reason='duplicate',p.archived_at=UTC_TIMESTAMP(3)
WHERE p.is_archived=0 AND p.id<>k.keep_id;

SET @sql = IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='tester_pallet_scans' AND COLUMN_NAME='active_sn')=0,
 'ALTER TABLE tester_pallet_scans ADD COLUMN active_sn VARCHAR(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci GENERATED ALWAYS AS (CASE WHEN is_archived=0 THEN sn ELSE NULL END) STORED',
 'SELECT ''active_sn already exists''');
PREPARE migration_stmt FROM @sql; EXECUTE migration_stmt; DEALLOCATE PREPARE migration_stmt;
SET @sql = IF((SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='tester_pallet_scans' AND INDEX_NAME='uq_tester_pallet_active_sn')=0,
 'ALTER TABLE tester_pallet_scans ADD UNIQUE KEY uq_tester_pallet_active_sn (active_sn)',
 'SELECT ''active SN index already exists''');
PREPARE migration_stmt FROM @sql; EXECUTE migration_stmt; DEALLOCATE PREPARE migration_stmt;
-- 替换旧全表 SN 唯一索引为有效行唯一索引，不删除记录。
SET @sql = IF((SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='tester_pallet_scans' AND INDEX_NAME='uq_tester_pallet_sn')>0,
 'ALTER TABLE tester_pallet_scans DROP INDEX uq_tester_pallet_sn',
 'SELECT ''no legacy global SN index''');
PREPARE migration_stmt FROM @sql; EXECUTE migration_stmt; DEALLOCATE PREPARE migration_stmt;
INSERT INTO pallet_scan_receipts (scan_id,sn,is_processed)
SELECT p.scan_id,p.sn,1 FROM tester_pallet_scans AS p
ON DUPLICATE KEY UPDATE is_processed=pallet_scan_receipts.is_processed;
SET @sql = IF((SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='checklist_work_orders' AND INDEX_NAME='idx_work_order_device_audit')=0,
 'ALTER TABLE checklist_work_orders ADD KEY idx_work_order_device_audit (device_id,audit_status)',
 'SELECT ''work order index already exists''');
PREPARE migration_stmt FROM @sql; EXECUTE migration_stmt; DEALLOCATE PREPARE migration_stmt;
INSERT INTO schema_migrations (migration_id) VALUES ('011_inventory_safety') ON DUPLICATE KEY UPDATE migration_id=migration_id;
-- 验收只输出计数：有效 SN 重复应为零，总表行数应与迁移前一致。
SELECT COUNT(*) AS total_preserved_rows, SUM(is_archived=0) AS active_rows, SUM(is_archived=1) AS archived_rows FROM tester_pallet_scans;
SELECT COUNT(*) AS duplicate_active_groups FROM (SELECT active_sn FROM tester_pallet_scans WHERE is_archived=0 GROUP BY active_sn HAVING COUNT(*)>1) duplicates;
