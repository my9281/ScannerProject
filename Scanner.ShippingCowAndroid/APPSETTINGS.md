# 安卓上传配置

编辑项目根目录的 `appsettings.xml`：

- `UploadBaseUrl`：上传服务器地址。
- `UploadApiKey`：上传密码，对应服务器的 `X-Upload-Key`。
- `ReceivingUsername`：包裹收货后台登录账号，默认 `PDA01`。
- `ReceivingPassword`：包裹收货账号密码，由管理员填写；保留首尾空格，不会作为设备上传密钥使用。

包裹收货默认配置：

```xml
<add key="UploadBaseUrl" value="https://wms.ymforever.com/" />
<add key="ReceivingUsername" value="PDA01" />
<add key="ReceivingPassword" value="" />
```

填写 `ReceivingPassword` 后重新编译并安装 APK。进入包裹收货页自动连接，页面不提供账号密码登录框。账号 `PDA01` 需要预先在 Web 中创建并具备收货权限。P0实收直接绑定账号所属域，不要求先配置BLUETTI预报站点，也不要求存在预报工单；内部收货范围由Web返回，不在使用者界面显示。

后台优先校验同账号、同服务器的缓存会话，过期时使用配置重新登录；上传时会直接使用已有会话提交，不预先查询预报、切换批次或刷新业务范围。上传收到401时最多重新登录并重试一次，403不会反复登录。网络异常时，已确认身份仍可离线扫码，未确认队列保留。页面只显示扫码表格、待上传数量及简短操作结果。

良品区（托盘）扫描、库位扫描和 SKU/SN 批次上传继续使用 `UploadApiKey`；包裹收货使用独立的配置账号登录。配置在应用启动时读取，文件作为嵌入资源随 APK 打包，修改后需要重新编译并安装。密码包含 `&`、`<`、双引号等 XML 特殊字符时需分别使用 `&amp;`、`&lt;`、`&quot;`。
