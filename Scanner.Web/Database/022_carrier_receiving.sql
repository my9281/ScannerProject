-- Existing Scanner.Web database; MySQL 5.7+. UTC timestamps.
-- Rerunnable CREATEs preserve existing tables/data; they do not repair different definitions.
CREATE TABLE IF NOT EXISTS carrier_settings (
 domain_id INT NOT NULL,
 carrier VARCHAR(10) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 enabled BOOLEAN NOT NULL DEFAULT FALSE,
 sandbox BOOLEAN NOT NULL DEFAULT TRUE,
 client_id VARCHAR(512) NOT NULL DEFAULT '',
 secret_protected TEXT NOT NULL,
 revision INT NOT NULL DEFAULT 1,
 updated_by VARCHAR(100) NOT NULL,
 updated_at DATETIME(3) NOT NULL,
 PRIMARY KEY(domain_id,carrier)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS carrier_receiving (
 id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
 domain_id INT NOT NULL,
 carrier VARCHAR(10) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 tracking_number VARCHAR(40) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 reference VARCHAR(200) NOT NULL DEFAULT '',
 created_at DATETIME(3) NOT NULL,
 created_by VARCHAR(100) NOT NULL,
 received_at DATETIME(3) NULL,
 received_by VARCHAR(100) NULL,
 carrier_status VARCHAR(500) NOT NULL DEFAULT '',
 delivered BOOLEAN NOT NULL DEFAULT FALSE,
 checked_at DATETIME(3) NULL,
 PRIMARY KEY(id),
 UNIQUE KEY uq_carrier_receiving(domain_id,carrier,tracking_number),
 KEY ix_carrier_receiving_domain(domain_id,id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
