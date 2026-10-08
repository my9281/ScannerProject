CREATE TABLE IF NOT EXISTS `warehouse_locations` (
    `location_id` VARCHAR(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
    `is_disabled` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '0=启用，1=禁用',
    `scanned_at` DATETIME(3) NOT NULL COMMENT '首次扫描时间 UTC',
    PRIMARY KEY (`location_id`),
    CONSTRAINT `ck_warehouse_location_disabled` CHECK (`is_disabled` IN (0, 1))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

