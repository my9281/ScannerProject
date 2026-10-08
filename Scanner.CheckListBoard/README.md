# 壹仓检测平板 / Scanner.CheckListBoard

更新：2026-10-09。原生 .NET 10 MAUI，主要 Android 平板，最低 API 21；声明的其他平台需独立验收。[总文档](../request.md) 统一记录服务端和部署。

## 完成范围与工作台

已完成登录、记住凭据、权限工作台、真实工单分页下载和完成状态保存。工单页不再生成 TEST-DEV 本地样例；旧 WorkOrderStore 仅保留为历史工具/测试，不参与当前页面。

| 入口 | 权限 | 状态 |
| --- | --- | --- |
| 入库检测、安全检测、裸机检测、整机检测 | checklist.write | 待接入 |
| 查看评级、历史查看 | checklist.read | 待接入 |
| 系统配置 | system.configure | 待接入 |
| 工单查看 | checklist.read | 真实服务器读写 |

admin/user/viewer 按服务器返回权限显示入口；viewer 不可修改工单。服务端独立校验授权，按钮禁用不能代替 API 权限。

## 登录、凭据和退出

启动 LoginPage，默认 HTTPS 地址 https://wms.ymforever.com/，服务设置允许修改 HTTPS 地址。POST `/api/account/login` 返回 userId、token、role、permissions、message、expiresAt。成功替换根页面，不保留登录返回栈。

记住账号密码默认开启，仅成功登录后保存到 SecureStorage；重启预填，不自动登录。取消勾选或修改地址清除旧凭据。Preferences 只存服务地址；安全存储不可用提示但不阻止成功登录。Android 关闭备份。

会话仅内存，重启需登录，到期返回登录。退出 POST `/api/account/logout` 并清除本地会话，不清除已记住凭据；网络失败仍本机退出，远程会话按有效期到期。

## 工单下载与保存

GET `/api/checklist-work-orders` 携带 Bearer，强制当前用户、审核位 0；afterId 按 ID 分页，每页最多 200，客户端持续拉取后显示完整列表。即使管理员也不能用 userId 读取别人。

固定表头，列表虚拟化独立滚动；列为序号、设备 ID、描述、完成状态。原生 Switch 未完成红色、完成绿色。有 checklist.write 的 user/admin 可以切换，viewer 只读。

修改先留在页面，点击保存逐条 PUT `/api/checklist-work-orders/{id}`，提交 isCompleted 和当前 version。服务端核验归属/审核/版本，成功版本加一，完成时间 UTC，取消完成清空时间。

保存非整批事务：成功项保留新版本，失败后未成功项保留待重试，提示部分保存失败。401 需登录，403 无权限，409 冲突需重新下载核对。下载失败提示错误，不回退样例数据。重新下载、页面返回及系统返回存在未保存修改时提示放弃确认；保存中限制操作。

工单生成与基础表匹配由 WPF 自动匹配完成，平板目前不承担审核和任务分配。

## 外观与资源

三个页面保留固定 board_banner.png 背景；工作台两列八张半透明黑卡，天青/金色/红色状态。图标与 PDA 同一壹仓标识，透明区域保持透明。当前 csproj 引用 brand_logo.png，原生开屏黑底；历史文字开屏与本地样例描述已归档。

## 工程和部署

LoginPage：登录、地址和凭据；MainPage：身份、权限、退出；WorkOrdersPage：分页、修改、版本保存和离开提示；Models/WorkOrderRow：行状态；Services/AccountClient 与工单客户端：会话和 HTTP。

服务器需数据库、账号会话/工单/来源去重 SQL；WPF 生成工单依赖启用的 my9281 账号及有效域。样例 SQL 仅用于测试，不重复写生产。详见 [总文档](../request.md)、[账号接口](../Scanner.Web/WEB_LOGIN.md)、[工单接口](../Scanner.Web/WORK_ORDERS.md)。

## 构建与验收

```powershell
dotnet build Scanner.CheckListBoard/Scanner.CheckListBoard.csproj -f net10.0-android
```

需 .NET 10 SDK、MAUI Android 工作负载、Android SDK。tests/CheckListBoard.Auth.Tests 覆盖相关逻辑，现场仍须验收凭据/地址切换、三角色、会话到期、分页、完成/取消、409、部分失败、断网、未保存退出、软键盘和滚动。其他七个模块待实现后验收。
