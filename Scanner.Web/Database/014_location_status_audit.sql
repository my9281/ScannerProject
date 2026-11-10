CREATE TABLE IF NOT EXISTS warehouse_location_status_history (
 id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
 location_id VARCHAR(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL,
 is_disabled TINYINT(1) NOT NULL,
 operator_user_id BIGINT UNSIGNED NOT NULL,
 reason VARCHAR(500) NOT NULL,
 changed_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
 KEY ix_location_status_history (location_id,changed_at),
 CONSTRAINT fk_location_status_location FOREIGN KEY (location_id) REFERENCES warehouse_locations(location_id),
 CONSTRAINT fk_location_status_operator FOREIGN KEY (operator_user_id) REFERENCES app_users(id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
INSERT INTO schema_migrations (migration_id) VALUES ('014_location_status_audit') ON DUPLICATE KEY UPDATE migration_id=migration_id;
