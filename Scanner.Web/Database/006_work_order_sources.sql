-- Execute after 004_checklist_work_orders.sql. No changes to app_users or existing work order columns.
CREATE TABLE IF NOT EXISTS `checklist_work_order_sources` (
    `source_key` CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    `work_order_id` BIGINT UNSIGNED NULL,
    PRIMARY KEY (`source_key`),
    KEY `ix_work_order_source_order` (`work_order_id`),
    CONSTRAINT `fk_work_order_source_order` FOREIGN KEY (`work_order_id`)
      REFERENCES `checklist_work_orders` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB;
