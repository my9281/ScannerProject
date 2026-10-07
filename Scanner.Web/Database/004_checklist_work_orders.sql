-- MySQL 5.7+. UTC timestamps. audit_status=0 is distributable; any other value is withheld.
CREATE TABLE IF NOT EXISTS `checklist_work_orders` (
    `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `user_id` BIGINT UNSIGNED NOT NULL COMMENT 'Assigned app_users.id',
    `device_id` VARCHAR(100) NOT NULL,
    `description` VARCHAR(1000) NOT NULL,
    `task_at` DATETIME(3) NOT NULL COMMENT 'Task time in UTC',
    `is_completed` TINYINT UNSIGNED NOT NULL DEFAULT 0 COMMENT '0 incomplete, 1 complete',
    `completed_at` DATETIME(3) NULL,
    `audit_status` TINYINT UNSIGNED NOT NULL DEFAULT 0 COMMENT 'Only 0 is distributable',
    `version` INT UNSIGNED NOT NULL DEFAULT 0 COMMENT 'Optimistic concurrency version',
    `created_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    `updated_at` DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
    PRIMARY KEY (`id`),
    KEY `idx_work_orders_user_audit_id` (`user_id`, `audit_status`, `id`),
    CONSTRAINT `fk_work_orders_user` FOREIGN KEY (`user_id`) REFERENCES `app_users` (`id`) ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Example task for the previously created account; inserts nothing if the user is absent.
-- INSERT INTO checklist_work_orders (user_id, device_id, description, task_at, audit_status)
-- SELECT id, 'DEV-001', '检查设备外观', UTC_TIMESTAMP(3), 0 FROM app_users WHERE username='my9281';
-- Withhold a task (and invalidate any downloaded version):
-- UPDATE checklist_work_orders SET audit_status=1, version=version+1 WHERE id=1;
