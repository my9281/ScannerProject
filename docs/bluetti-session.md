# WPF 自动同步数据

主程序 `Scanner.WPF` → 第一个功能框 **自动同步数据** → **一键刷新**。沿用原手动刷新图标；同时保留“手动导入基础表”和“手动导入预报/工单”两个入口。登录配置位于页面最底部“系统设置”。

一键刷新在后台运行，不弹出网页或同步窗口。按钮下方显示“检查配置 → 下载并导入 → 成功/失败”状态；从检查配置开始，整个主界面置灰、禁用并显示等待光标，直至任务和下载资源清理结束。成功、配置错误及执行失败均在 finally 中恢复界面，防止二次导入或操作其他功能。配置缺失、损坏、账号认证或站点权限有问题时提示前往“系统设置”。完成后显示预报/工单与基础表各自成功导入的数量；部分失败时保留已成功部分并显示数量。

每次打开主窗口，会自动从本地 Downloads 缓存恢复上次成功导入的客诉单和基础表，恢复预报匹配规则、基础记录和月报原始数据，不依赖联网或重新登录。启动加载期间同样置灰禁用主界面。两项独立恢复，一项损坏不影响另一项；缺失时跳过，损坏时提示刷新或手动导入。主界面显示恢复数量。

`latest-forecast.txt` 和 `latest-base.txt` 记录最近成功导入的相对文件路径。只有导入成功才更新标记并删除同类旧文件。兼容旧版本未创建标记的缓存，按目录时间顺序尝试读取有效文件；不会将单次失败下载标记为成功缓存。

1. 到主页面最底部最后一项 **系统设置**，输入 BLUETTI 售后账号和密码，点击“登录并获取站点”。
2. 选择可用站点；如需保存账号密码，勾选“保存账号密码到本地 config 文件”，点击“验证站点并保存本地会话”或“保存配置”。
3. 返回首个功能框的“自动同步数据”。程序优先验证本地 Token，若缺失或过期则读取已保存配置重新登录，并验证站点权限。
4. 未保存账号密码且会话失效时，需前往“系统设置”重新登录；网络错误保留会话，便于稍后重试。
5. “清除本机会话”只删除本机保存的凭证，不会调用远端退出接口或使浏览器中的登录失效。
6. 在主界面点击“一键刷新”，后台依次导出并导入 DTC 客诉单和入库检测表；结果直接显示在主界面。

数据对应关系：

| 网站文件 | WPF 数据 | 导入路径 |
| --- | --- | --- |
| DTC客诉单导出 | 预报 / 工单匹配规则 | 与手动“导入工单”共用 `ApplyForecastFile` 和 `UrgentWorkOrderImportHelper` |
| 入库检测表 → 导出 | 共享基础表、月报原始数据 | 与手动基础表导入共用 `ChecklistDataCache.ImportBase` |

同步使用所选售后站点和已抓取的页面默认筛选参数，不继承外部浏览器的临时筛选。新数据替换对应的内存数据集。每项下载和解析成功后才更新该项；如果第二项失败，第一项成功结果保留。登录、选站和导出全部通过 HTTP 接口完成，不启动网页或 WebView2。

两个导出接口和请求参数已在实际网页上抓取，见 [导出接口说明](bluetti-export-api.md)。直接请求服务器返回的 Excel 文件，不依赖页面路由、菜单、按钮渲染或浏览器运行时。网页导出代码仅保留作开发诊断工具。

导出等待分两个阶段：服务器生成客诉单默认 10 分钟、基础表默认 30 分钟；收到实际文件字节后，下载阶段另计 15 分钟，期限互不占用。每个接口只提交一次，不因等待较久重复触发生成。

期限读取程序配置文件 `Scanner.WPF.dll.config` 的 appSettings（源码模板为 `Scanner.WPF/App.config`），修改后重启生效：

| 配置项 | 默认值 |
| --- | --- |
| `BluettiForecastGenerationTimeoutMinutes` | 10 |
| `BluettiBaseGenerationTimeoutMinutes` | 30 |
| `BluettiDownloadTimeoutMinutes` | 15 |

发生错误时区分服务器生成、下载超时；其他错误显示异常类型。阶段和详细异常记录到 `%LOCALAPPDATA%\Scanner\Bluetti\sync.log`，不记录密码、Token 或导出数据内容，日志大小限制约 256 KB。旧 `BluettiExportPageTimeoutSeconds` 仅供开发用网页诊断工具使用，正式同步不加载页面。

Token 和站点信息存放在 `%LOCALAPPDATA%\Scanner\Bluetti\session.dat`，通过 Windows DPAPI `CurrentUser` 加密。不与 Scanner 本身的账号会话混用。

**账号密码属于运行时配置，不写入代码。** 勾选保存后，生成独立 XML 文件 `%LOCALAPPDATA%\Scanner\Bluetti\bluetti.config`，`appSettings` 内保存 `Username`、`ProtectedPassword`、`SiteId`。密码通过 DPAPI `CurrentUser` 加密，原值不会以明文写入文件；仅当前 Windows 用户可解密，文件不可直接复制给其他用户使用。取消勾选后点击“保存配置”可删除已保存账号密码；“清除本机会话”仅删除 Token。配置文件已加入 Git 忽略规则。登录仍按官网方式对密码取大写 SHA-256 摘要提交。

主页面手动刷新按钮已移除；原有启动时的后台数据加载保留。

`BluettiApiExporter` 使用 HttpClient 发送 JSON POST，附加现有会话的 Token 和站点标识。响应流直接写入本地文件，验证 XLSX ZIP 结构再交给导入器；JSON、HTML 错误或截断文件不会替换缓存。正式同步不依赖 Microsoft Edge WebView2 Runtime。

保存会话不会自动登录外部浏览器，也不会延长服务端 Token 的有效期。下载文件保存在 `%LOCALAPPDATA%\Scanner\Bluetti\Downloads\`。每种文件仅保留最近一次成功导入的版本：本次成功后删除该目录内同类旧文件并移除空目录；失败文件删除，上次成功文件保留。旧文件被占用时提示清理未完成，下次同步重试。仅清理本功能目录内的已知文件名，不删除手动导入文件或用户“下载”目录的文件。会话会加密，导出的 Excel 文件按原样保存。

实现依据（2026-10-20 读取的公开前端源码）：

- https://after-sales.bluettipower.com/views/account/login.vue
- https://after-sales.bluettipower.com/views/account/site.vue
- https://after-sales.bluettipower.com/webjars/global/main.js
- https://after-sales.bluettipower.com/webjars/global/env.js
- https://after-sales.bluettipower.com/assets/scripts/module.js

验证：

```powershell
dotnet run --project tests/BluettiSession.Tests/BluettiSession.Tests.csproj
dotnet run --project tests/WpfLocalization.Tests/WpfLocalization.Tests.csproj -- D:/GitRepos/ScannerProject
```

协议测试使用模拟网关，检查密码摘要、请求头、站点筛选、过期响应和本地加密存储，并渲染三种语言窗口。内嵌浏览器测试通过离线页面验证导航、会话恢复、自动点击和下载捕获。

已使用实际已保存账号完成纯 HTTP 连续同步验证：DTC 客诉单导入 2,233 条预报记录，入库检测表导入 8,876 条基础记录。回归测试覆盖请求参数、认证头、流式下载、错误响应、超时、取消、启动缓存恢复和旧文件清理。服务端若调整接口或增加验证码，需要相应更新客户端。
