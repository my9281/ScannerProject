-- 退货收货功能合并迁移 022 + 023 + 024
-- 整理日期：2026-11-16；适用：现有 Scanner.Web MySQL 数据库，MySQL 5.7+。
-- 使用方法：先在数据库工具中选中 Web 使用的数据库，再【执行整个文件】。
-- 不预设库名、不自动切库；不要在 mysql、sys、information_schema 等系统库执行。
-- 本文件自包含，无需 SOURCE，也无需再逐个执行 022/023/024。
-- 新建缺少的表，并在字段/索引不存在时追加；不含 DELETE、TRUNCATE 或 DROP TABLE。
-- 重复执行不会清空数据，但不会修复已有同名字段或索引的不同定义。
-- DDL 会隐式提交：遇到错误立即停止并保留报错；不要用“忽略错误继续”模式。
-- 先执行结构变更，再发布对应 Web/PDA。末尾包含只读检查结果。
-- 仅包含现有 P0 收货及 P1 同步基础，不包含 P2 维修 checklist 或 P3 任务 Dashboard。
-- 账号、密码、站点由 Web 管理员界面配置；此处不写入明文凭据或虚构站点。
-- 本文件由 tools/BuildReceivingSql.ps1 生成，修改编号迁移后重新生成。

SELECT DATABASE() AS target_database, VERSION() AS server_version;

-- =====================================================================
-- 022_carrier_receiving.sql
-- =====================================================================
-- Existing Scanner.Web database; MySQL 5.7+. UTC timestamps.
-- Rerunnable CREATEs preserve existing tables/data; they do not repair different definitions.
CREATE TABLE IF NOT EXISTS carrier_settings (
 domain_id INT NOT NULL,
 carrier VARCHAR(10) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 enabled BOOLEAN NOT NULL DEFAULT FALSE,
 sandbox BOOLEAN NOT NULL DEFAULT TRUE,
 client_id VARCHAR(512) NOT NULL DEFAULT '',
 secret_protected TEXT NOT NULL,
 revision INT NOT NULL DEFAULT 1,
 updated_by VARCHAR(100) NOT NULL,
 updated_at DATETIME(3) NOT NULL,
 PRIMARY KEY(domain_id,carrier)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS carrier_receiving (
 id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
 domain_id INT NOT NULL,
 carrier VARCHAR(10) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 tracking_number VARCHAR(40) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 reference VARCHAR(200) NOT NULL DEFAULT '',
 created_at DATETIME(3) NOT NULL,
 created_by VARCHAR(100) NOT NULL,
 received_at DATETIME(3) NULL,
 received_by VARCHAR(100) NULL,
 carrier_status VARCHAR(500) NOT NULL DEFAULT '',
 delivered BOOLEAN NOT NULL DEFAULT FALSE,
 checked_at DATETIME(3) NULL,
 PRIMARY KEY(id),
 UNIQUE KEY uq_carrier_receiving(domain_id,carrier,tracking_number),
 KEY ix_carrier_receiving_domain(domain_id,id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- =====================================================================
-- 023_bluetti_receiving_sync.sql
-- =====================================================================
-- Run after 022_carrier_receiving.sql. UTC timestamps; MySQL 5.7 compatible.
-- P0 receipt scope comes from the authenticated WMS domain, not this site configuration.
-- P1 uses the remaining synchronization settings and forecast tables.
CREATE TABLE IF NOT EXISTS receiving_sync_settings (
 domain_id INT NOT NULL PRIMARY KEY,
 enabled BOOLEAN NOT NULL DEFAULT FALSE,
 username VARCHAR(200) NOT NULL DEFAULT '',
 password_protected TEXT NOT NULL,
 site_id VARCHAR(100) NOT NULL DEFAULT '',
 interval_seconds INT NOT NULL DEFAULT 1800,
 auto_track BOOLEAN NOT NULL DEFAULT FALSE,
 track_interval_seconds INT NOT NULL DEFAULT 1800,
 revision INT NOT NULL DEFAULT 1,
 next_run_at DATETIME(3) NOT NULL,
 last_attempt_at DATETIME(3) NULL,
 last_success_at DATETIME(3) NULL,
 last_error VARCHAR(500) NOT NULL DEFAULT '',
 last_row_count INT NOT NULL DEFAULT 0,
 last_issue_count INT NOT NULL DEFAULT 0,
 current_run CHAR(36) CHARACTER SET ascii NOT NULL DEFAULT '',
 updated_by VARCHAR(100) NOT NULL,
 updated_at DATETIME(3) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS bluetti_receiving_forecasts (
 id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
 domain_id INT NOT NULL,
 site_id VARCHAR(100) NOT NULL,
 source_key CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 rma VARCHAR(200) NOT NULL,
 carrier VARCHAR(40) NOT NULL,
 tracking_number VARCHAR(200) NOT NULL,
 issue VARCHAR(200) NOT NULL,
 source_json MEDIUMTEXT NOT NULL,
 first_seen_at DATETIME(3) NOT NULL,
 last_seen_at DATETIME(3) NOT NULL,
 run_id CHAR(36) CHARACTER SET ascii NOT NULL,
 UNIQUE KEY uq_bluetti_forecast(domain_id,site_id,source_key),
 KEY ix_bluetti_forecast_run(domain_id,run_id,id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS receiving_sync_runs (
 run_id CHAR(36) CHARACTER SET ascii NOT NULL PRIMARY KEY,
 domain_id INT NOT NULL,
 site_id VARCHAR(100) NOT NULL,
 started_at DATETIME(3) NOT NULL,
 finished_at DATETIME(3) NULL,
 status VARCHAR(20) NOT NULL,
 row_count INT NOT NULL DEFAULT 0,
 issue_count INT NOT NULL DEFAULT 0,
 message VARCHAR(500) NOT NULL DEFAULT '',
 KEY ix_receiving_sync_runs(domain_id,started_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
-- Guarded additive ALTER statements: rerunnable, including after partial execution.
SET @scanner_receiving_sql = IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='carrier_receiving' AND COLUMN_NAME='last_attempt_at'),
 'SELECT 1', 'ALTER TABLE carrier_receiving ADD COLUMN last_attempt_at DATETIME(3) NULL');
PREPARE scanner_receiving_stmt FROM @scanner_receiving_sql;
EXECUTE scanner_receiving_stmt;
DEALLOCATE PREPARE scanner_receiving_stmt;
SET @scanner_receiving_sql = IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='carrier_receiving' AND COLUMN_NAME='last_error'),
 'SELECT 1', 'ALTER TABLE carrier_receiving ADD COLUMN last_error VARCHAR(500) NOT NULL DEFAULT ''''');
PREPARE scanner_receiving_stmt FROM @scanner_receiving_sql;
EXECUTE scanner_receiving_stmt;
DEALLOCATE PREPARE scanner_receiving_stmt;
SET @scanner_receiving_sql = IF(EXISTS(SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='carrier_receiving' AND INDEX_NAME='ix_carrier_receiving_poll'),
 'SELECT 1', 'ALTER TABLE carrier_receiving ADD KEY ix_carrier_receiving_poll(domain_id,received_at,last_attempt_at)');
PREPARE scanner_receiving_stmt FROM @scanner_receiving_sql;
EXECUTE scanner_receiving_stmt;
DEALLOCATE PREPARE scanner_receiving_stmt;
SET @scanner_receiving_sql = IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='carrier_settings' AND COLUMN_NAME='retry_after_at'),
 'SELECT 1', 'ALTER TABLE carrier_settings ADD COLUMN retry_after_at DATETIME(3) NULL');
PREPARE scanner_receiving_stmt FROM @scanner_receiving_sql;
EXECUTE scanner_receiving_stmt;
DEALLOCATE PREPARE scanner_receiving_stmt;

-- =====================================================================
-- 024_carrier_receipt_scans.sql
-- =====================================================================
-- Run after 022 and 023. Append-only PDA scan events; timestamps UTC.
-- P0 receipt/time audit. Does not implement the P2 checklist or P3 personal tasks.
CREATE TABLE IF NOT EXISTS carrier_receipt_scans (
 scan_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
 domain_id INT NOT NULL,
 operator_user_id BIGINT UNSIGNED NOT NULL,
 operator_username VARCHAR(100) NOT NULL,
 device_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 batch_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 batch_number VARCHAR(100) NOT NULL,
 site_id VARCHAR(100) NOT NULL,
 carrier VARCHAR(10) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 tracking_number VARCHAR(40) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 raw_barcode TEXT NOT NULL,
 scanned_at DATETIME(3) NOT NULL,
 uploaded_at DATETIME(3) NOT NULL,
 KEY ix_receipt_domain_time(domain_id,scanned_at),
 KEY ix_receipt_package(domain_id,carrier,tracking_number),
 KEY ix_receipt_batch(domain_id,batch_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- =====================================================================
-- 迁移后只读检查
-- =====================================================================
-- 只读结构检查，可单独重复执行。不读取密码、令牌或业务记录。
-- 预期：第一张结果表的 6 行均为 OK；随后两张 MISSING 结果表应无记录。
-- 本检查核对名称是否存在，不替代 SHOW CREATE TABLE 的类型、索引顺序及定义核对。
SELECT DATABASE() AS target_database, VERSION() AS server_version;

SELECT required_tables.table_name,
       IF(actual.TABLE_NAME IS NULL, 'MISSING', 'OK') AS table_status
FROM (
SELECT 'carrier_settings' AS table_name
UNION ALL
SELECT 'carrier_receiving' AS table_name
UNION ALL
SELECT 'receiving_sync_settings' AS table_name
UNION ALL
SELECT 'bluetti_receiving_forecasts' AS table_name
UNION ALL
SELECT 'receiving_sync_runs' AS table_name
UNION ALL
SELECT 'carrier_receipt_scans' AS table_name
) required_tables
LEFT JOIN information_schema.TABLES actual
  ON actual.TABLE_SCHEMA=DATABASE() AND actual.TABLE_NAME=required_tables.table_name
ORDER BY required_tables.table_name;

SELECT required_columns.table_name, required_columns.column_name, 'MISSING' AS column_status
FROM (
SELECT 'carrier_settings' AS table_name, 'domain_id' AS column_name
UNION ALL
SELECT 'carrier_settings' AS table_name, 'carrier' AS column_name
UNION ALL
SELECT 'carrier_settings' AS table_name, 'enabled' AS column_name
UNION ALL
SELECT 'carrier_settings' AS table_name, 'sandbox' AS column_name
UNION ALL
SELECT 'carrier_settings' AS table_name, 'client_id' AS column_name
UNION ALL
SELECT 'carrier_settings' AS table_name, 'secret_protected' AS column_name
UNION ALL
SELECT 'carrier_settings' AS table_name, 'revision' AS column_name
UNION ALL
SELECT 'carrier_settings' AS table_name, 'updated_by' AS column_name
UNION ALL
SELECT 'carrier_settings' AS table_name, 'updated_at' AS column_name
UNION ALL
SELECT 'carrier_receiving' AS table_name, 'id' AS column_name
UNION ALL
SELECT 'carrier_receiving' AS table_name, 'domain_id' AS column_name
UNION ALL
SELECT 'carrier_receiving' AS table_name, 'carrier' AS column_name
UNION ALL
SELECT 'carrier_receiving' AS table_name, 'tracking_number' AS column_name
UNION ALL
SELECT 'carrier_receiving' AS table_name, 'reference' AS column_name
UNION ALL
SELECT 'carrier_receiving' AS table_name, 'created_at' AS column_name
UNION ALL
SELECT 'carrier_receiving' AS table_name, 'created_by' AS column_name
UNION ALL
SELECT 'carrier_receiving' AS table_name, 'received_at' AS column_name
UNION ALL
SELECT 'carrier_receiving' AS table_name, 'received_by' AS column_name
UNION ALL
SELECT 'carrier_receiving' AS table_name, 'carrier_status' AS column_name
UNION ALL
SELECT 'carrier_receiving' AS table_name, 'delivered' AS column_name
UNION ALL
SELECT 'carrier_receiving' AS table_name, 'checked_at' AS column_name
UNION ALL
SELECT 'receiving_sync_settings' AS table_name, 'domain_id' AS column_name
UNION ALL
SELECT 'receiving_sync_settings' AS table_name, 'enabled' AS column_name
UNION ALL
SELECT 'receiving_sync_settings' AS table_name, 'username' AS column_name
UNION ALL
SELECT 'receiving_sync_settings' AS table_name, 'password_protected' AS column_name
UNION ALL
SELECT 'receiving_sync_settings' AS table_name, 'site_id' AS column_name
UNION ALL
SELECT 'receiving_sync_settings' AS table_name, 'interval_seconds' AS column_name
UNION ALL
SELECT 'receiving_sync_settings' AS table_name, 'auto_track' AS column_name
UNION ALL
SELECT 'receiving_sync_settings' AS table_name, 'track_interval_seconds' AS column_name
UNION ALL
SELECT 'receiving_sync_settings' AS table_name, 'revision' AS column_name
UNION ALL
SELECT 'receiving_sync_settings' AS table_name, 'next_run_at' AS column_name
UNION ALL
SELECT 'receiving_sync_settings' AS table_name, 'last_attempt_at' AS column_name
UNION ALL
SELECT 'receiving_sync_settings' AS table_name, 'last_success_at' AS column_name
UNION ALL
SELECT 'receiving_sync_settings' AS table_name, 'last_error' AS column_name
UNION ALL
SELECT 'receiving_sync_settings' AS table_name, 'last_row_count' AS column_name
UNION ALL
SELECT 'receiving_sync_settings' AS table_name, 'last_issue_count' AS column_name
UNION ALL
SELECT 'receiving_sync_settings' AS table_name, 'current_run' AS column_name
UNION ALL
SELECT 'receiving_sync_settings' AS table_name, 'updated_by' AS column_name
UNION ALL
SELECT 'receiving_sync_settings' AS table_name, 'updated_at' AS column_name
UNION ALL
SELECT 'bluetti_receiving_forecasts' AS table_name, 'id' AS column_name
UNION ALL
SELECT 'bluetti_receiving_forecasts' AS table_name, 'domain_id' AS column_name
UNION ALL
SELECT 'bluetti_receiving_forecasts' AS table_name, 'site_id' AS column_name
UNION ALL
SELECT 'bluetti_receiving_forecasts' AS table_name, 'source_key' AS column_name
UNION ALL
SELECT 'bluetti_receiving_forecasts' AS table_name, 'rma' AS column_name
UNION ALL
SELECT 'bluetti_receiving_forecasts' AS table_name, 'carrier' AS column_name
UNION ALL
SELECT 'bluetti_receiving_forecasts' AS table_name, 'tracking_number' AS column_name
UNION ALL
SELECT 'bluetti_receiving_forecasts' AS table_name, 'issue' AS column_name
UNION ALL
SELECT 'bluetti_receiving_forecasts' AS table_name, 'source_json' AS column_name
UNION ALL
SELECT 'bluetti_receiving_forecasts' AS table_name, 'first_seen_at' AS column_name
UNION ALL
SELECT 'bluetti_receiving_forecasts' AS table_name, 'last_seen_at' AS column_name
UNION ALL
SELECT 'bluetti_receiving_forecasts' AS table_name, 'run_id' AS column_name
UNION ALL
SELECT 'receiving_sync_runs' AS table_name, 'run_id' AS column_name
UNION ALL
SELECT 'receiving_sync_runs' AS table_name, 'domain_id' AS column_name
UNION ALL
SELECT 'receiving_sync_runs' AS table_name, 'site_id' AS column_name
UNION ALL
SELECT 'receiving_sync_runs' AS table_name, 'started_at' AS column_name
UNION ALL
SELECT 'receiving_sync_runs' AS table_name, 'finished_at' AS column_name
UNION ALL
SELECT 'receiving_sync_runs' AS table_name, 'status' AS column_name
UNION ALL
SELECT 'receiving_sync_runs' AS table_name, 'row_count' AS column_name
UNION ALL
SELECT 'receiving_sync_runs' AS table_name, 'issue_count' AS column_name
UNION ALL
SELECT 'receiving_sync_runs' AS table_name, 'message' AS column_name
UNION ALL
SELECT 'carrier_receiving' AS table_name, 'last_attempt_at' AS column_name
UNION ALL
SELECT 'carrier_receiving' AS table_name, 'last_error' AS column_name
UNION ALL
SELECT 'carrier_settings' AS table_name, 'retry_after_at' AS column_name
UNION ALL
SELECT 'carrier_receipt_scans' AS table_name, 'scan_id' AS column_name
UNION ALL
SELECT 'carrier_receipt_scans' AS table_name, 'domain_id' AS column_name
UNION ALL
SELECT 'carrier_receipt_scans' AS table_name, 'operator_user_id' AS column_name
UNION ALL
SELECT 'carrier_receipt_scans' AS table_name, 'operator_username' AS column_name
UNION ALL
SELECT 'carrier_receipt_scans' AS table_name, 'device_id' AS column_name
UNION ALL
SELECT 'carrier_receipt_scans' AS table_name, 'batch_id' AS column_name
UNION ALL
SELECT 'carrier_receipt_scans' AS table_name, 'batch_number' AS column_name
UNION ALL
SELECT 'carrier_receipt_scans' AS table_name, 'site_id' AS column_name
UNION ALL
SELECT 'carrier_receipt_scans' AS table_name, 'carrier' AS column_name
UNION ALL
SELECT 'carrier_receipt_scans' AS table_name, 'tracking_number' AS column_name
UNION ALL
SELECT 'carrier_receipt_scans' AS table_name, 'raw_barcode' AS column_name
UNION ALL
SELECT 'carrier_receipt_scans' AS table_name, 'scanned_at' AS column_name
UNION ALL
SELECT 'carrier_receipt_scans' AS table_name, 'uploaded_at' AS column_name
) required_columns
LEFT JOIN information_schema.COLUMNS actual
  ON actual.TABLE_SCHEMA=DATABASE() AND actual.TABLE_NAME=required_columns.table_name
 AND actual.COLUMN_NAME=required_columns.column_name
WHERE actual.COLUMN_NAME IS NULL
ORDER BY required_columns.table_name, required_columns.column_name;

SELECT required_indexes.table_name, required_indexes.index_name, 'MISSING' AS index_status
FROM (
SELECT 'carrier_settings' AS table_name, 'PRIMARY' AS index_name
UNION ALL
SELECT 'carrier_receiving' AS table_name, 'uq_carrier_receiving' AS index_name
UNION ALL
SELECT 'carrier_receiving' AS table_name, 'ix_carrier_receiving_domain' AS index_name
UNION ALL
SELECT 'carrier_receiving' AS table_name, 'PRIMARY' AS index_name
UNION ALL
SELECT 'receiving_sync_settings' AS table_name, 'PRIMARY' AS index_name
UNION ALL
SELECT 'bluetti_receiving_forecasts' AS table_name, 'uq_bluetti_forecast' AS index_name
UNION ALL
SELECT 'bluetti_receiving_forecasts' AS table_name, 'ix_bluetti_forecast_run' AS index_name
UNION ALL
SELECT 'bluetti_receiving_forecasts' AS table_name, 'PRIMARY' AS index_name
UNION ALL
SELECT 'receiving_sync_runs' AS table_name, 'ix_receiving_sync_runs' AS index_name
UNION ALL
SELECT 'receiving_sync_runs' AS table_name, 'PRIMARY' AS index_name
UNION ALL
SELECT 'carrier_receiving' AS table_name, 'ix_carrier_receiving_poll' AS index_name
UNION ALL
SELECT 'carrier_receipt_scans' AS table_name, 'ix_receipt_domain_time' AS index_name
UNION ALL
SELECT 'carrier_receipt_scans' AS table_name, 'ix_receipt_package' AS index_name
UNION ALL
SELECT 'carrier_receipt_scans' AS table_name, 'ix_receipt_batch' AS index_name
UNION ALL
SELECT 'carrier_receipt_scans' AS table_name, 'PRIMARY' AS index_name
) required_indexes
LEFT JOIN information_schema.STATISTICS actual
  ON actual.TABLE_SCHEMA=DATABASE() AND actual.TABLE_NAME=required_indexes.table_name
 AND actual.INDEX_NAME=required_indexes.index_name
WHERE actual.INDEX_NAME IS NULL
ORDER BY required_indexes.table_name, required_indexes.index_name;
