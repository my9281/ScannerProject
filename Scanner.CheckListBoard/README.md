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
| 操作文档 | checklist.read | 服务端 Markdown、截图和 Excel 附件 |

admin/user/viewer 按服务器返回权限显示入口；viewer 不可修改工单。服务端独立校验授权，按钮禁用不能代替 API 权限。

操作文档通过登录会话读取 Web 发布的文档目录，支持 SOP、图片和参考 Excel 下载分享。更新文档内容只需更新服务端资源；入口与发布说明见 [文档浏览与发布](../docs/文档浏览与发布.md)。工作台现有九个入口，卡片区域可滚动。

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

## 工单位置与保存更新

部署前执行 `010_work_order_location.sql`：新增 location VARCHAR(100)，将旧描述开头“托盘号：编号；”拆出到位置列，同时移除该描述片段并增加 version。新上传独立传 location 字符串。平板显示位置及逐行已保存/未保存，只有服务器确认成功才标记已保存。完成状态保存不修改审核位；WPF 或 PDA 写入 shelved_pallet_data 表即上架；同一事务按 SN 将对应工单 audit_status 设为 1 并增加 version，平板仅下载审核位 0，因此上架后过滤掉。

Release 包体设置、构建参数与验收见 [MAUI 包体优化](../docs/MAUI包体优化.md)。

工单查看在全部分页下载完成后按位置自然升序排列（1、2、10；A2、A10），空位置排最后，同位置按 ID 排列，序号按显示顺序重新生成。保存完成状态保持当前顺序，重新下载应用同一排序规则。
