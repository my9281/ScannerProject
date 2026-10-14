-- 只读预检：在 repairwms 数据库执行，不修改数据。不包含生产数据样例。
-- 重复 SN 按当前列排序规则计数（之后仍须检查目标比较规则碰撞）。
SELECT COUNT(*) AS duplicate_sn_groups FROM (
 SELECT sn FROM tester_pallet_scans GROUP BY sn HAVING COUNT(*)>1
) duplicates;
SELECT COUNT(*) AS invalid_pallet_rows FROM tester_pallet_scans WHERE pallet_number NOT BETWEEN 1 AND 100;
SELECT COUNT(*) AS invalid_scan_date_rows FROM tester_pallet_scans WHERE scan_date<>DATE(scan_time);
SELECT COUNT(*) AS invalid_location_flags FROM warehouse_locations WHERE is_disabled NOT IN (0,1);
SELECT COUNT(*) AS invalid_work_order_flags FROM checklist_work_orders
 WHERE is_completed NOT IN (0,1) OR audit_status NOT IN (0,1);
SELECT COUNT(*) AS inconsistent_completion_rows FROM checklist_work_orders
 WHERE (is_completed=0 AND completed_at IS NOT NULL) OR (is_completed=1 AND completed_at IS NULL);
SELECT COUNT(*) AS empty_work_order_location_rows FROM checklist_work_orders WHERE TRIM(location)='';
SELECT COUNT(*) AS unresolved_source_rows FROM checklist_work_order_sources WHERE work_order_id IS NULL;
SELECT COUNT(*) AS mismatched_batch_counts FROM sku_sn_batches b
 LEFT JOIN (SELECT batch_id,COUNT(*) AS item_count FROM sku_sn_batch_items GROUP BY batch_id) i ON i.batch_id=b.batch_id
 WHERE b.item_count<>COALESCE(i.item_count,0);
SELECT COUNT(*) AS duplicate_batch_number_groups FROM (
 SELECT batch_number FROM sku_sn_batches GROUP BY batch_number HAVING COUNT(*)>1
) duplicates; -- 允许重复，不直接视为错误。
SELECT COUNT(*) AS expired_or_revoked_sessions FROM user_sessions
 WHERE expires_at<=UTC_TIMESTAMP() OR revoked_at IS NOT NULL;
SELECT COUNT(*) AS invalid_user_roles_or_states FROM app_users
 WHERE role NOT IN ('admin','user','viewer') OR status NOT IN ('active','disabled','locked');
SELECT COUNT(*) AS active_tasks_with_shelving_history FROM checklist_work_orders w
 WHERE w.audit_status=0 AND EXISTS (SELECT 1 FROM shelved_pallet_data s WHERE s.sn=w.device_id);
-- 上一项是候选异常：可能是合法新周期，必须人工核验，不能直接全部过滤。
SELECT TABLE_NAME,COLUMN_NAME,COLUMN_TYPE,COLLATION_NAME FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA=DATABASE() AND COLUMN_NAME IN ('sn','device_id','sku','pallet_number','location','location_id');
SELECT TABLE_NAME,INDEX_NAME,NON_UNIQUE,SEQ_IN_INDEX,COLUMN_NAME FROM information_schema.STATISTICS
 WHERE TABLE_SCHEMA=DATABASE() ORDER BY TABLE_NAME,INDEX_NAME,SEQ_IN_INDEX;
