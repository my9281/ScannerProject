-- MySQL 5.7：先备份，再在 Web 使用的数据库执行。可重复执行。
SET @location_exists = (SELECT COUNT(*) FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='checklist_work_orders' AND COLUMN_NAME='location');
SET @location_sql = IF(@location_exists=0,
 'ALTER TABLE `checklist_work_orders` ADD COLUMN `location` VARCHAR(100) NOT NULL DEFAULT '''' COMMENT ''位置，托盘编号字符串'' AFTER `description`',
 'SELECT ''location already exists'' AS message');
PREPARE location_statement FROM @location_sql;
EXECUTE location_statement;
DEALLOCATE PREPARE location_statement;

-- 仅处理旧自动匹配描述格式：托盘号：编号；SKU：...
-- 同时拆出位置、移除描述中的托盘段、增加版本以拒绝平板旧快照。
UPDATE `checklist_work_orders`
SET `location`=TRIM(SUBSTRING_INDEX(SUBSTRING(`description`,5),'；',1)),
    `description`=SUBSTRING(`description`,LOCATE('；',`description`)+1),
    `version`=`version`+1
WHERE `location`=''
  AND `description` LIKE '托盘号：%'
  AND LOCATE('；',`description`)>5
  AND CHAR_LENGTH(TRIM(SUBSTRING_INDEX(SUBSTRING(`description`,5),'；',1))) BETWEEN 1 AND 100;

-- 可选：若需同步历史已上架工单，确认这些 SN 确实应停止下发后执行：
-- UPDATE checklist_work_orders w
-- SET w.audit_status=1, w.version=w.version+1
-- WHERE w.audit_status<>1
--   AND EXISTS (SELECT 1 FROM shelved_pallet_data s WHERE s.sn=w.device_id);
