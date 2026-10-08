-- 在目标数据库中执行。MySQL/MariaDB 使用不同的删除 CHECK 语法。
-- 旧版 MySQL 不保存/执行 CHECK；不存在此约束时无需修改，范围由 PDA 和服务端校验。
SET @pallet_check_exists = (
    SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
    WHERE CONSTRAINT_SCHEMA = DATABASE()
      AND TABLE_NAME = 'tester_pallet_scans'
      AND CONSTRAINT_NAME = 'ck_tester_pallet_number'
      AND CONSTRAINT_TYPE = 'CHECK'
);
SET @pallet_upgrade_sql = IF(
    @pallet_check_exists > 0,
    CONCAT(
        'ALTER TABLE `tester_pallet_scans` ',
        IF(LOCATE('MariaDB', VERSION()) > 0, 'DROP CONSTRAINT ', 'DROP CHECK '),
        '`ck_tester_pallet_number`, ADD CONSTRAINT `ck_tester_pallet_number` CHECK (`pallet_number` BETWEEN 1 AND 100)'
    ),
    'SELECT ''未发现旧 CHECK 约束，无需删除；PDA 和服务端已校验托盘号 1–100。'' AS message'
);
PREPARE pallet_upgrade_statement FROM @pallet_upgrade_sql;
EXECUTE pallet_upgrade_statement;
DEALLOCATE PREPARE pallet_upgrade_statement;
