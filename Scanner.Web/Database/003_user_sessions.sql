-- Only needed if the existing account service's user_sessions table is absent.
-- Does not alter app_users. All DATETIME values in this session flow use UTC.
CREATE TABLE IF NOT EXISTS `user_sessions` (
    `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    `user_id` BIGINT UNSIGNED NOT NULL,
    `token_hash` BINARY(32) NOT NULL,
    `expires_at` DATETIME NOT NULL,
    `created_at` DATETIME NOT NULL,
    `last_used_at` DATETIME NOT NULL,
    `revoked_at` DATETIME NULL,
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_user_sessions_token` (`token_hash`),
    KEY `idx_user_sessions_user` (`user_id`),
    KEY `idx_user_sessions_expiry` (`expires_at`),
    CONSTRAINT `fk_user_sessions_user` FOREIGN KEY (`user_id`) REFERENCES `app_users` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
