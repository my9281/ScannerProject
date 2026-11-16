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
