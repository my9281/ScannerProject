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
