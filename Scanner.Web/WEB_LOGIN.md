# CheckListBoard Web 登录

## 改动文件

- `Scanner.Web/Services/AccountSecurity.cs`：密码兼容校验、状态判断、权限和登录提示。
- `Scanner.Web/Services/MySqlAccountService.cs`：登录事务、失败计数、锁定恢复、会话复查。
- `Scanner.Web/Services/IAccountService.cs`：身份响应增加 permissions/message。
- `Scanner.Web/Controllers/AccountController.cs`：统一失败提示及禁止缓存身份响应。
- `Scanner.Web/Models/AccountRequests.cs`：登录输入长度约束。
- `Scanner.Web/Filters/AccountPermissionAttribute.cs`：服务端权限过滤及身份 Claims。
- `Scanner.Web/wwwroot/login.html`：展示输入校验提示和可访问状态信息。
- `Scanner.Web/wwwroot/account.html`：展示角色、权限与登录提示。
- `Scanner.Web/Database/003_user_sessions.sql`：缺失会话表时的部署脚本。
- `tests/WebAccount.Tests/Program.cs`、`WebAccount.Tests.csproj`：自动验证。
- `Scanner.Web/README.md`、`Scanner.Web/WEB_LOGIN.md`：更新部署、流程与测试说明。

## 工程位置与入口

`Scanner.CheckListBoard` 是 .NET 10 MAUI 工程，Shell/MainPage 中各检测模块当前为待接入页面，并没有 Web 路由或数据库访问层。浏览器入口沿用 `Scanner.Web` 的 ASP.NET Core Controller + wwwroot 静态页面，数据访问沿用 `IMySqlConnectionFactory`、MySqlConnector 和 `MySqlAccountService`，会话沿用随机 Bearer 令牌及数据库 `user_sessions`，没有另建认证框架。

- `/login.html` 或 `/account/login`：直接用户名、密码登录，不要求设备访问密钥。
- `POST /api/account/login`：JSON `{ "username": "alice", "password": "..." }`。
- `/account.html`：登录成功后展示用户名、显示名称、所属域、角色、权限、对应登录提示、会话到期时间。
- `GET /api/account/me`、`POST /api/account/logout`：使用 `Authorization: Bearer <token>`。
- 注册入口仍按当前 Program.cs 的规则关闭。设备上传接口保留原有 API Key 授权。

## 数据库与部署

使用上传 SQL 中的 app_users 结构，不修改该表。连接使用现有 `Database` 配置（建议通过环境变量提供凭据），需 `Database:Enabled=true`。所属域依赖现有 `user_domains(id, domain_name, is_enabled)`。

现有服务已依赖 `user_sessions`，但仓库此前缺少建表脚本。如果数据库中没有此表，先在对应数据库执行 `Database/003_user_sessions.sql`；已有表需核对脚本所列字段，不自动更改现有结构。新增脚本只补齐原有数据库会话的部署依赖，未在实际数据库上执行。

所有锁定、登录、会话 DATETIME 值约定 UTC。部署前确认已有数据采用同一时间约定，尤其是其他应用写入的 locked_until/password_changed_at。运行站点时使用 HTTPS。

## 核心流程

1. 校验输入并去掉用户名首尾空格（允许 1–100 字符）；密码保留空格，最多 4096 字符。
2. 参数化查询及事务内 `SELECT ... FOR UPDATE` 锁住账户行，避免并发失败计数丢失。
3. active 且未处于未来锁定期的账户可登录；disabled、停用域、未知角色均拒绝。locked 有到期时间且已到期可尝试；locked 无到期时间视为永久锁定。
4. 从数据库读取二进制 salt/hash、算法、迭代次数，仅支持 PBKDF2-SHA256。按保存的 hash 实际字节长度生成 UTF-8 密码派生值，使用固定时间比较；兼容 32/64 字节哈希及最多 32 字节盐。无效算法/参数拒绝，绝不回退为明文校验。未知用户名执行常规 PBKDF2 工作。
5. 密码错误累计 failed_login_count，5 次后设置 status=locked、locked_until=UTC 当前时间+15 分钟。到期后下一次错误从 1 重新计数。成功恢复 active，清零失败计数和锁定日期，更新 last_login_at。
6. 同一事务中生成 32 字节随机令牌，数据库仅保存 SHA-256 token_hash；会话有效期 8 小时。事务提交后才返回身份及令牌。网页令牌保存在 sessionStorage。
7. me/权限检查从数据库复查会话撤销、到期、用户状态、锁定时间、域状态及改密时间；角色变化随查询生效。退出标记 revoked_at。身份接口返回 Cache-Control: no-store。

未知用户名、错误密码、锁定和停用均返回同一 401 提示：“登录失败：用户名或密码错误，或账户暂不可用，请稍后重试或联系管理员。”不向未认证请求披露账户存在性及具体状态。输入错误返回 400；网页展示校验提示。

| role | 权限 | 登录提示 |
| --- | --- | --- |
| admin | checklist.read、checklist.write、system.configure | 管理员登录成功，可查看、操作检测记录及配置系统。 |
| user | checklist.read、checklist.write | 普通用户登录成功，可查看及操作检测记录。 |
| viewer | checklist.read | 只读用户登录成功，仅可查看检测记录。 |

目前检测模块尚无业务 API，权限名称定义了后续接口接入边界。`[AccountPermission("checklist.write")]` 可用于新业务 Controller/Action：服务端复查会话并返回 401/403，成功后设置 HttpContext.User 的姓名、角色及域 Claims。不要仅靠前端隐藏按钮授权，也不要将 Web 会话等同于设备 API Key。

## 验证

```powershell
dotnet build Scanner.Web/Scanner.Web.csproj --no-restore -m:1 -p:UseSharedCompilation=false
dotnet run --project tests/WebAccount.Tests/WebAccount.Tests.csproj -p:BuildInParallel=false -p:UseSharedCompilation=false
dotnet run --project Scanner.Web/Scanner.Web.csproj
```

自动测试覆盖 Python hashlib 独立生成的 32/64 字节 PBKDF2 向量、中文及尾部空格、错误密码、错误算法、非法迭代次数、锁定到期边界、禁用账户/域、未知角色、角色权限矩阵以及登录/me/logout 的返回。会话接口测试使用替身服务，不连接生产数据库。

数据库集成验收请使用隔离测试库及测试账户：

1. 按上传结构准备 admin/user/viewer 账户，密码使用 UTF-8 PBKDF2-SHA256，原始二进制盐/哈希及实际迭代次数存入对应字段；准备已有域表及会话表。
2. 浏览器无 X-Upload-Key 登录，检查三种权限和提示；刷新账户页仍能获取身份。
3. 错误密码连续 5 次，核对 failed_login_count=5、status=locked 及 locked_until；正确密码在锁定期间仍拒绝。
4. 将测试账户锁定时间设为过去；错误密码重新计数为 1，正确密码成功后计数归零、locked_until=NULL、last_login_at 更新。
5. 验证 disabled、永久 locked、域停用、未知用户名均返回相同 401，响应不含 salt/hash/具体状态。
6. 登录后撤销会话、设置 expires_at 为过去、禁用用户/域、设置未来 locked_until、更新 password_changed_at 为会话建立后，分别确认 me 返回 401。退出后旧令牌不可继续使用。
7. 并发错误登录验证计数无丢失；人为使会话插入失败，验证登录成功字段更新随事务回滚。

未执行实际数据库集成验收，不应将替身接口测试视为 MySQL 事务/SQL 的运行验证。
