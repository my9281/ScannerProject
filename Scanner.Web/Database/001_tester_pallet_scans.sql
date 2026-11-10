CREATE TABLE IF NOT EXISTS `tester_pallet_scans` (
    `id` BIGINT NOT NULL AUTO_INCREMENT,
    `scan_id` CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    `sn` VARCHAR(100) NOT NULL,
    `scan_date` DATE NOT NULL,
    `pallet_number` INT NOT NULL,
    `scan_time` DATETIME(3) NOT NULL,
    PRIMARY KEY (`id`),
    UNIQUE KEY `uq_tester_pallet_scan_id` (`scan_id`),
    UNIQUE KEY `uq_tester_pallet_sn` (`sn`),
    KEY `ix_tester_pallet_date` (`scan_date`, `pallet_number`),
    CONSTRAINT `ck_tester_pallet_number` CHECK (`pallet_number` BETWEEN 1 AND 100)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
