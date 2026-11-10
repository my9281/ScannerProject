-- 先停写，保留全部既有账号和会话，默认版本 0 保持现有有效会话。
SET @sql=IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='app_users' AND COLUMN_NAME='credential_version')=0,
 'ALTER TABLE app_users ADD COLUMN credential_version INT UNSIGNED NOT NULL DEFAULT 0', 'SELECT ''field exists''');
PREPARE migration_stmt FROM @sql; EXECUTE migration_stmt; DEALLOCATE PREPARE migration_stmt;
SET @sql=IF((SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='user_sessions' AND COLUMN_NAME='credential_version')=0,
 'ALTER TABLE user_sessions ADD COLUMN credential_version INT UNSIGNED NOT NULL DEFAULT 0', 'SELECT ''field exists''');
PREPARE migration_stmt FROM @sql; EXECUTE migration_stmt; DEALLOCATE PREPARE migration_stmt;
-- 管理员未来变更密码时，必须在同一事务更新密码哈希/盐及 credential_version=credential_version+1。
-- 无需物理删除旧会话，版本不匹配即失效；禁止仅改变 password_changed_at 代替撤销。

-- 首次部署本触发器；重复部署时先检查 SHOW TRIGGERS，不重复创建。
-- 密码材料变化自动增加凭据版本，杜绝维护 SQL 忘记撤销旧会话。
DELIMITER $$
CREATE TRIGGER scanner_users_password_version
BEFORE UPDATE ON app_users FOR EACH ROW
BEGIN
 IF NOT (NEW.password_hash <=> OLD.password_hash)
    OR NOT (NEW.password_salt <=> OLD.password_salt)
    OR NOT (NEW.password_algorithm <=> OLD.password_algorithm)
    OR NOT (NEW.password_iterations <=> OLD.password_iterations) THEN
   SET NEW.credential_version=OLD.credential_version+1;
   SET NEW.password_changed_at=UTC_TIMESTAMP();
 END IF;
END$$
DELIMITER ;

INSERT INTO schema_migrations (migration_id) VALUES ('015_credential_versions') ON DUPLICATE KEY UPDATE migration_id=migration_id;
