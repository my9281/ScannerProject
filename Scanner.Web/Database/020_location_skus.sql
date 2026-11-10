CREATE TABLE IF NOT EXISTS warehouse_location_skus (
 location_id VARCHAR(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
 sku VARCHAR(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
 quantity BIGINT NOT NULL DEFAULT 0,
 PRIMARY KEY(location_id,sku),
 FOREIGN KEY(location_id) REFERENCES warehouse_locations(location_id),
 CHECK(quantity >= 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS warehouse_location_sku_history (
 operation_id CHAR(36) NOT NULL PRIMARY KEY,
 location_id VARCHAR(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
 sku VARCHAR(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
 action VARCHAR(16) NOT NULL,
 delta INT NOT NULL,
 quantity_before BIGINT NOT NULL,
 quantity_after BIGINT NOT NULL,
 operator_name VARCHAR(100) NOT NULL,
 created_at DATETIME(3) NOT NULL DEFAULT (UTC_TIMESTAMP(3)),
 INDEX(location_id,sku,created_at),
 FOREIGN KEY(location_id,sku) REFERENCES warehouse_location_skus(location_id,sku)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
