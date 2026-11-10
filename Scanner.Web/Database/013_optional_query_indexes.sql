-- 可选性能优化：先在测试库 EXPLAIN/测量，再选择执行。仅新增索引，无数据删除。
SET @sql=IF((SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='tester_pallet_scans' AND INDEX_NAME='ix_active_pallet_time')=0,
 'ALTER TABLE tester_pallet_scans ADD KEY ix_active_pallet_time (is_archived,pallet_number,scan_time)', 'SELECT ''index exists''');
PREPARE migration_stmt FROM @sql; EXECUTE migration_stmt; DEALLOCATE PREPARE migration_stmt;
SET @sql=IF((SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='shelved_pallet_data' AND INDEX_NAME='ix_shelving_date_time')=0,
 'ALTER TABLE shelved_pallet_data ADD KEY ix_shelving_date_time (shelving_date,shelving_time,number)', 'SELECT ''index exists''');
PREPARE migration_stmt FROM @sql; EXECUTE migration_stmt; DEALLOCATE PREPARE migration_stmt;
SET @sql=IF((SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='shelved_pallet_data' AND INDEX_NAME='ix_shelving_pallet_date')=0,
 'ALTER TABLE shelved_pallet_data ADD KEY ix_shelving_pallet_date (pallet_number,shelving_date,shelving_time)', 'SELECT ''index exists''');
PREPARE migration_stmt FROM @sql; EXECUTE migration_stmt; DEALLOCATE PREPARE migration_stmt;
INSERT INTO schema_migrations (migration_id) VALUES ('013_optional_query_indexes') ON DUPLICATE KEY UPDATE migration_id=migration_id;
