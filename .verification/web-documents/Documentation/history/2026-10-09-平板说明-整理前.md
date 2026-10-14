# 壹仓检测 / CheckListBoard

## 当前工单数据流程

工单页已切换为真实服务端下载/更新，不再使用下文历史阶段的本地测试数据。登录返回 userId；下载强制按会话用户 ID 与审核位 0 筛选，保存只提交完成状态和版本号。重新下载可更新任务列表。数据库建表、十条可选测试数据、接口契约与部署步骤见 [WORK_ORDERS.md](../Scanner.Web/WORK_ORDERS.md)。本地 WorkOrderStore 保留作旧文件测试工具，不再由工单页面调用。

## 当前应用图标与开屏

仅应用图标和原生开屏使用上传参考图中的蓝金色标识。登录、工作台和工单页面标题均未更改，board_banner.png 背景保持原样。最终开屏品牌文案以用户明确指定的“壹仓·海外仓”为准，纯白色，无其他公司名称或英文副标题。

- `Resources/AppIcon/brand_logo.png`：内置 image_gen 根据上传小图制作的透明放大 logo，应用图标前景 appiconfg.svg 内嵌使用它。
- `Resources/Splash/splash.svg`：蓝金色 logo 与白色“壹仓·海外仓”；文字使用 Microsoft YaHei Bold 字体轮廓，避免平台缺字和位图文字锯齿。
- `Resources/AppIcon/brand_reference.png`：原始参考图片，未用于开屏文字。
- `Scanner.CheckListBoard.csproj`：应用图标资源引用及开屏黑底/尺寸配置。

logo 的内置 image_gen 最终提示词：

> Precisely restore and upscale ONLY the supplied tiny logo as a crisp high resolution flat brand mark on fully transparent background. Preserve the exact original blue swooping circular orbital symbol and gold lower-right crescent shapes, their arrangement, distinctive small gaps and proportions. Faithful tracing, no redesign, no added text, no other symbols, no shadows, no gradients, no new details. Center logo with 12 percent transparent margin on a square canvas. This is an app icon asset.

## 简约界面与工单查看

登录页保留“幽梦之星 · 云上检查单”的标题、左侧表单及 board_banner.png 全屏背景，输入框采用透明底与天青细线，标签弱化字号和颜色。桌面保留左侧布局，宽度小于 760 时表单自动铺开，并可滚动以容纳软键盘。

工作台使用同一固定背景图和黑色半透明卡片，八个模块按两列排列，第 8 项为“工单查看”。该入口要求 checklist.read，三种标准角色均可查看。点击通过当前 Shell 的导航栈打开 WorkOrdersPage，页面提供返回按钮。

工单页背景图固定不随滚动移动。表格背景 `#99000000` 为 60% 不透明黑色，文字不设置透明度；表头固定，CollectionView 数据区独立上下滚动并支持虚拟化。列为序号、设备ID、描述、完成状态。最后一列采用原生 Switch 双向绑定：0/未完成为红色，1/已完成为绿色。首次打开生成 10 条明确标记为 TEST-DEV 的测试记录，三条初始完成。列表绑定 ObservableCollection<WorkOrderRow>，不调用接口。右上角保存按钮将当前记录写入设备应用数据目录的 JSON 文件（临时文件写入后替换），再次进入读取已保存数据；未点击保存的修改不会持久化。文件按服务地址和用户名隔离。普通用户和管理员可切换和保存，viewer 只读。返回时有未保存修改会提示是否放弃。

本次改动：LoginPage.xaml/.cs、MainPage.xaml/.cs、新增 WorkOrdersPage.xaml/.cs、Models/WorkOrderRow.cs、Services/WorkOrderStore.cs。设备验收应确认工单入口打开和返回正常、背景图在三个页面保留、10 条测试工单上下滚动且表头固定，切换完成状态后点击保存，再次进入确认恢复。测试项目增加开关通知、0/1 映射和真实本地文件保存/覆盖检查，共 27 项通过。

## 启动与登录

此 MAUI 工程现在默认打开原生 `LoginPage`；开屏使用黑底蓝金色 logo 与白色“壹仓·海外仓”，登录页展示专属 banner。登录成功后替换窗口根页面为检测工作台，不把登录页留在返回栈；每次重新启动仍显示登录页。

默认使用 `https://wms.ymforever.com/`，登录页的“服务设置”可修改 HTTPS 地址。调用现有 Scanner.Web 的 `/api/account/login`，密码仅通过 HTTPS 发往服务端，沿用 app_users 的校验逻辑。服务端需要部署本次 Web 登录改动，返回 token、role、permissions、message 和 expiresAt；数据库配置与会话表依赖见 `../Scanner.Web/WEB_LOGIN.md`。

“记住账号和密码”默认开启，只在成功登录后通过 MAUI SecureStorage 保存用户名、原始密码及服务地址；再次启动预填，不自动登录。取消勾选立即清除本工程保存的凭据。服务地址改变也会清除此前保存的凭据，避免将旧服务账号作为新服务默认值。密码不写入 Preferences 或日志，Preferences 仅保存服务器地址。安全存储不可用时明确提示，不阻止已成功认证的用户进入工作台。Android 已关闭备份，避免恢复无法解密的安全存储数据。

会话仅保留在内存中，退出时调用服务端 logout 并清除本地身份。退出不清除已记住的账号密码，可在登录页取消记住来清除。网络不可用时提示远程会话未撤销。权限从服务端响应读取，viewer 仅可查看评级和历史，user 可以进入检测入口，admin 还可以进入系统配置；检测模块仍为原有待接入占位页面。后续真实业务 API 仍须在服务端使用 AccountPermission 授权，前端禁用按钮只是界面呈现。

## 配色与资源

- 底色 `#080A0C`，表面 `#11161B`，主要文字白色。
- 天青 `#8FCFDA`：登录主按钮、输入字段标题、检测入口。
- 金色 `#D9B866`：品牌细节、系统配置、辅助操作。
- 大红 `#E53935` / 错误文字 `#E84949`：图标点缀、错误和无权限状态。
- `Resources/Images/board_banner.png`：开屏登录 banner。
- `Resources/Images/board_mark.svg`：登录页品牌图标。
- `Resources/AppIcon/appicon.svg`、`brand_logo.png`：平台应用图标。
- `Resources/Splash/splash.svg`：蓝金色标识及白色“壹仓·海外仓”，项目文件中开屏背景已设为黑色。

banner 使用内置 image_gen 生成，最终提示词：

> Use case: stylized-concept. Asset type: widescreen opening banner background for CheckListBoard industrial quality inspection mobile app. Create a premium restrained abstract black banner, landscape 3:2 composition. Deep pure black background, sculptural thin porcelain sky-cyan arcs and a sweeping cyan illuminated contour on the right half, a subtle fine metallic gold orbital line, a very small vivid Chinese red accent near the lower right. Elegant precision, calm high-end instrument panel aesthetic. Left half mostly pure black negative space so white app title can be overlaid in native UI. Absolutely no text, no letters, no watermark, no mockup devices. Crisp sophisticated lighting, no rainbow colors, no purple, no busy texture.

当前应用图标已按上传参考图更换为蓝金色 logo；appiconfg.svg 已替换为新 logo 的 SVG 包装，当前图标引用见本文件开头。

## 验证

```powershell
dotnet build Scanner.CheckListBoard/Scanner.CheckListBoard.csproj -f net10.0-windows10.0.19041.0 --no-restore -m:1 -p:UseSharedCompilation=false
dotnet build Scanner.CheckListBoard/Scanner.CheckListBoard.csproj -f net10.0-android --no-restore -m:1 -p:UseSharedCompilation=false -p:Aapt2DaemonMaxInstanceCount=1
dotnet run --project tests/CheckListBoard.Auth.Tests/CheckListBoard.Auth.Tests.csproj -p:UseSharedCompilation=false
```

客户端自动检查采用替身 HTTP 与替身存储，覆盖路由/请求字段、保留密码空格、角色权限、退出请求、错误提示、过期/未知角色拒绝、HTTPS 地址校验、凭据读写及清除。平台 SecureStorage 加密及真实服务登录需要设备验收：

1. 启动确认黑底开屏图标，随后进入登录页，检查小屏滚动和软键盘遮挡情况。
2. 使用测试账号登录，核对三种角色的提示及入口状态；退出应返回登录页。
3. 保持记住勾选，成功登录后完全退出再启动，确认账号和密码预填但没有自动进入工作台。
4. 取消记住、重启，确认凭据已清除；错误密码应停留登录页并展示服务端提示。
5. 关闭网络后测试连接错误、退出提示；测试存储不可用时的提示。
6. 设置错误服务地址，确认提示；部署后台后用真实 MySQL 测试账户进行完整认证验收。

## 改动文件

`App.xaml.cs`、`MauiProgram.cs`、`AppShell.xaml/.cs` 接入登录启动和成功导航；`LoginPage.xaml/.cs` 实现登录界面和凭据保存；`Services/AccountClient.cs` 连接服务及维护内存会话；`MainPage.xaml/.cs` 实现统一主题、角色呈现及退出；项目文件和 AppIcon/Splash/Images 更新品牌资源；AndroidManifest.xml 关闭备份。客户端测试在 `tests/CheckListBoard.Auth.Tests`。
