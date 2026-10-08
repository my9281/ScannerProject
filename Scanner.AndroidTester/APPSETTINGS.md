# 安卓上传配置

编辑项目根目录的 `appsettings.xml`：

- `UploadBaseUrl`：上传服务器地址。
- `UploadApiKey`：上传密码，对应服务器的 `X-Upload-Key`。

良品区（托盘）扫描、库位扫描和 SKU/SN 批次上传共用配置，应用启动时读取。文件作为嵌入资源随 APK 打包，修改后需要重新编译并安装。密码包含 `&`、`<`、双引号等 XML 特殊字符时需分别使用 `&amp;`、`&lt;`、`&quot;`。
