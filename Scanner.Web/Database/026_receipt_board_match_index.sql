-- Optional for correctness. Run after 023 to speed up board matching.
SET @sql = IF(EXISTS (
    SELECT 1 FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='bluetti_receiving_forecasts'
      AND INDEX_NAME='ix_bluetti_forecast_match'
), 'SELECT ''index exists''',
   'ALTER TABLE bluetti_receiving_forecasts ADD KEY ix_bluetti_forecast_match (domain_id,carrier,tracking_number(40),issue(1))');
PREPARE migration_stmt FROM @sql;
EXECUTE migration_stmt;
DEALLOCATE PREPARE migration_stmt;
