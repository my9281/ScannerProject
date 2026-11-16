-- Run after 022_carrier_receiving.sql. UTC timestamps; MySQL 5.7 compatible.
-- P0 uses receiving_sync_settings.site_id to bind PDA scope, with sync disabled.
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
