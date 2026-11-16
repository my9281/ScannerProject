-- Run after 022 and 023. Append-only PDA scan events; timestamps UTC.
CREATE TABLE IF NOT EXISTS carrier_receipt_scans (
 scan_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
 domain_id INT NOT NULL,
 operator_user_id BIGINT UNSIGNED NOT NULL,
 operator_username VARCHAR(100) NOT NULL,
 device_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 batch_id CHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 batch_number VARCHAR(100) NOT NULL,
 site_id VARCHAR(100) NOT NULL,
 carrier VARCHAR(10) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 tracking_number VARCHAR(40) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 raw_barcode TEXT NOT NULL,
 scanned_at DATETIME(3) NOT NULL,
 uploaded_at DATETIME(3) NOT NULL,
 KEY ix_receipt_domain_time(domain_id,scanned_at),
 KEY ix_receipt_package(domain_id,carrier,tracking_number),
 KEY ix_receipt_batch(domain_id,batch_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
