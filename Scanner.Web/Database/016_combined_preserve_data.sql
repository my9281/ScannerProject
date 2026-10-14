-- repairwms 合并迁移：MySQL 5.7.17，2026-10-10
-- 先备份并暂停写入；基于用户提供的现有 11 张表。
-- 不删除业务记录，不转换历史时间，不回填历史审核位。
-- 执行后发布新版 Web/WPF/PDA。DDL 不支持整包回滚，出错停止。
USE `repairwms`;
SET NAMES utf8mb4;
SET @migration_before_rows=(SELECT COUNT(*) FROM tester_pallet_scans);

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


-- 保留数据。011 后执行；先停写和备份。旧数据周期统一初始为 1，不擅自回填历史审核位。
CREATE TABLE IF NOT EXISTS inventory_device_state (
 sn VARCHAR(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
 cycle_number BIGINT UNSIGNED NOT NULL DEFAULT 1,
 is_shelved TINYINT(1) NOT NULL DEFAULT 0,
 updated_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
 PRIMARY KEY (sn)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
SET @sql=IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='checklist_work_orders' AND COLUMN_NAME='cycle_number')=0,
 'ALTER TABLE checklist_work_orders ADD COLUMN cycle_number BIGINT UNSIGNED NOT NULL DEFAULT 1',
 'SELECT ''cycle field exists''');
PREPARE migration_stmt FROM @sql; EXECUTE migration_stmt; DEALLOCATE PREPARE migration_stmt;
-- 有效良品区优先于历史上架，不把已再次入区的返修错误标为已上架。
INSERT INTO inventory_device_state (sn,cycle_number,is_shelved)
 SELECT p.sn,1,0 FROM tester_pallet_scans AS p WHERE p.is_archived=0
 ON DUPLICATE KEY UPDATE cycle_number=inventory_device_state.cycle_number;
INSERT INTO inventory_device_state (sn,cycle_number,is_shelved)
 SELECT s.sn,1,1 FROM shelved_pallet_data AS s
 ON DUPLICATE KEY UPDATE cycle_number=inventory_device_state.cycle_number;
-- 保留原字符串的大小写；匹配通过 active_sn/state 的不敏感规则。
INSERT INTO schema_migrations (migration_id) VALUES ('012_device_cycles') ON DUPLICATE KEY UPDATE migration_id=migration_id;


CREATE TABLE IF NOT EXISTS warehouse_location_status_history (
 id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
 location_id VARCHAR(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
 is_disabled TINYINT(1) NOT NULL,
 operator_user_id BIGINT UNSIGNED NOT NULL,
 reason VARCHAR(500) NOT NULL,
 changed_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
 KEY ix_location_status_history (location_id,changed_at),
 CONSTRAINT fk_location_status_location FOREIGN KEY (location_id) REFERENCES warehouse_locations(location_id),
 CONSTRAINT fk_location_status_operator FOREIGN KEY (operator_user_id) REFERENCES app_users(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
INSERT INTO schema_migrations (migration_id) VALUES ('014_location_status_audit') ON DUPLICATE KEY UPDATE migration_id=migration_id;


-- 先停写，保留全部既有账号和会话，默认版本 0 保持现有有效会话。
SET @sql=IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='app_users' AND COLUMN_NAME='credential_version')=0,
 'ALTER TABLE app_users ADD COLUMN credential_version INT UNSIGNED NOT NULL DEFAULT 0', 'SELECT ''field exists''');
PREPARE migration_stmt FROM @sql; EXECUTE migration_stmt; DEALLOCATE PREPARE migration_stmt;
SET @sql=IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='user_sessions' AND COLUMN_NAME='credential_version')=0,
 'ALTER TABLE user_sessions ADD COLUMN credential_version INT UNSIGNED NOT NULL DEFAULT 0', 'SELECT ''field exists''');
PREPARE migration_stmt FROM @sql; EXECUTE migration_stmt; DEALLOCATE PREPARE migration_stmt;
-- 管理员未来变更密码时，必须在同一事务更新密码哈希/盐及 credential_version=credential_version+1。
-- 无需物理删除旧会话，版本不匹配即失效；禁止仅改变 password_changed_at 代替撤销。

-- 首次部署本触发器；重复部署时先检查 SHOW TRIGGERS，不重复创建。
-- 密码材料变化自动增加凭据版本，杜绝维护 SQL 忘记撤销旧会话。
DELIMITER $$
CREATE TRIGGER scanner_users_password_version
BEFORE UPDATE ON app_users FOR EACH ROW
BEGIN
 IF NOT (NEW.password_hash <=> OLD.password_hash)
    OR NOT (NEW.password_salt <=> OLD.password_salt)
    OR NOT (NEW.password_algorithm <=> OLD.password_algorithm)
    OR NOT (NEW.password_iterations <=> OLD.password_iterations) THEN
   SET NEW.credential_version=OLD.credential_version+1;
   SET NEW.password_changed_at=UTC_TIMESTAMP();
 END IF;
END$$
DELIMITER ;

INSERT INTO schema_migrations (migration_id) VALUES ('015_credential_versions') ON DUPLICATE KEY UPDATE migration_id=migration_id;

SELECT @migration_before_rows AS before_rows, COUNT(*) AS after_rows, COUNT(*)=@migration_before_rows AS all_rows_preserved FROM tester_pallet_scans;
SELECT migration_id,applied_at FROM schema_migrations ORDER BY migration_id;
