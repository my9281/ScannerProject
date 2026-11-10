CREATE TABLE IF NOT EXISTS `sku_sn_batches` (
  `batch_id` CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `batch_number` CHAR(12) NOT NULL,
  `created_at` DATETIME(3) NOT NULL,
  `item_count` INT NOT NULL,
  PRIMARY KEY (`batch_id`),
  KEY `ix_sku_sn_batch_time` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS `sku_sn_batch_items` (
  `batch_id` CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `line_number` INT NOT NULL,
  `sku` VARCHAR(150) COLLATE utf8mb4_bin NOT NULL,
  `sn` VARCHAR(100) COLLATE utf8mb4_bin NOT NULL,
  PRIMARY KEY (`batch_id`, `line_number`),
  UNIQUE KEY `uq_sku_sn_batch_sn` (`batch_id`, `sn`),
  CONSTRAINT `fk_sku_sn_batch` FOREIGN KEY (`batch_id`)
    REFERENCES `sku_sn_batches` (`batch_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
