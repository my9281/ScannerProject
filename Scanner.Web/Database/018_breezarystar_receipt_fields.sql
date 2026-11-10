-- Only needed if the earlier 017 table has already been created.
-- MySQL 5.7.17: repeatable additive migration, no data deletion.
SET @breezary_fields = CONCAT_WS(', ',
  IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='breezarystar_receipts' AND COLUMN_NAME='actual_tracking_no'), NULL,
     'ADD COLUMN actual_tracking_no VARCHAR(160) NOT NULL DEFAULT '''' AFTER tracking_no'),
  IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='breezarystar_receipts' AND COLUMN_NAME='workflow_status'), NULL,
     'ADD COLUMN workflow_status VARCHAR(160) NOT NULL DEFAULT '''' AFTER receipt_status'),
  IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='breezarystar_receipts' AND COLUMN_NAME='receipt_date_ms'), NULL,
     'ADD COLUMN receipt_date_ms BIGINT NULL AFTER ship_date_ms'),
  IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='breezarystar_receipts' AND COLUMN_NAME='issue_description'), NULL,
     'ADD COLUMN issue_description TEXT NULL AFTER remark'),
  IF(EXISTS(SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='breezarystar_receipts' AND INDEX_NAME='ix_breezary_actual_tracking'), NULL,
     'ADD INDEX ix_breezary_actual_tracking (actual_tracking_no)')
);
SET @breezary_fields_sql = IF(@breezary_fields='', 'SELECT ''Breezary fields already exist'' AS message',
  CONCAT('ALTER TABLE breezarystar_receipts ', @breezary_fields));
PREPARE breezary_fields_statement FROM @breezary_fields_sql;
EXECUTE breezary_fields_statement;
DEALLOCATE PREPARE breezary_fields_statement;

-- New columns are populated by the next pull/local edit/push; raw JSON is retained.
