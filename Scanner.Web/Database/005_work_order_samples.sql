-- Optional test data only. Run 004 first. Re-running this script inserts another ten rows.
SET NAMES utf8mb4;
INSERT INTO `checklist_work_orders`
    (`user_id`, `device_id`, `description`, `task_at`, `is_completed`, `completed_at`, `audit_status`)
SELECT u.`id`, sample.`device_id`, sample.`description`, UTC_TIMESTAMP(3),
       sample.`is_completed`, IF(sample.`is_completed`=1, UTC_TIMESTAMP(3), NULL), 0
FROM `app_users` u
CROSS JOIN (
    SELECT 'TEST-DEV-001' AS `device_id`, '检查设备外观与机身完整性' AS `description`, 0 AS `is_completed`
    UNION ALL SELECT 'TEST-DEV-002', '核对设备序列号和标签', 1
    UNION ALL SELECT 'TEST-DEV-003', '检查电源及开机状态', 0
    UNION ALL SELECT 'TEST-DEV-004', '检测屏幕显示和触控功能', 0
    UNION ALL SELECT 'TEST-DEV-005', '检查接口与连接稳定性', 1
    UNION ALL SELECT 'TEST-DEV-006', '核验安全检测项目', 0
    UNION ALL SELECT 'TEST-DEV-007', '检测电池及充电功能', 0
    UNION ALL SELECT 'TEST-DEV-008', '检查设备运行噪声', 1
    UNION ALL SELECT 'TEST-DEV-009', '复核检测评级与记录', 0
    UNION ALL SELECT 'TEST-DEV-010', '完成出库前最终检查', 0
) sample
WHERE u.`username`='my9281';
