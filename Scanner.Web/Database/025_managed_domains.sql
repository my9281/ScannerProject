-- Existing domains preserve their self-registration behavior. Web-created domains are private.
SET @sql=IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE()
 AND TABLE_NAME='user_domains' AND COLUMN_NAME='allows_self_registration')=0,
 'ALTER TABLE user_domains ADD COLUMN allows_self_registration TINYINT(1) NOT NULL DEFAULT 1',
 'SELECT ''field exists''');
PREPARE migration_stmt FROM @sql;
EXECUTE migration_stmt;
DEALLOCATE PREPARE migration_stmt;
