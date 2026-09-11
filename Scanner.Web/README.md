# WMS 文件管理站点

该 ASP.NET Core 站点支持通过网页或 API 上传、浏览、预览和下载 TXT / JSON 文件。文件默认按 UTC 日期保存到 `App_Data/uploads/yyyyMMdd`，该目录不会被 Git 跟踪，也不能通过静态文件地址直接访问。

## API

- `POST /api/uploads/scans`：上传文件，表单字段为 `file` 和可选的 `deviceName`。
- `GET /api/uploads`：列出已上传文件。
- `GET /api/uploads/{id}/content`：读取文件内容，供网页预览。
- `GET /api/uploads/{id}/download`：下载文件。
- `GET /health`：健康检查。

除健康检查外，所有 API 在配置了密钥时都需要请求头 `X-Upload-Key`。上传仅接受有效的 `.txt` 或 `.json` 文件，默认最大 10 MB；在线预览默认最大 1 MB。

## 配置

生产环境建议使用环境变量：

```text
Upload__ApiKey=一段足够长的随机密钥
Upload__Directory=/var/lib/ymstar/uploads
Upload__MaxBytes=10485760
Upload__MaxPreviewBytes=1048576
```

未配置 `Upload__ApiKey` 时接口允许匿名访问，只适合本地测试。生产环境还需确保运行站点的账户对上传目录有读写权限。

## 运行与发布

```powershell
dotnet run --project Scanner.Web.csproj
dotnet publish Scanner.Web.csproj -p:PublishProfile=IIS-SelfContained
```

发布到 IIS 时，将发布目录的全部文件部署到站点物理目录，并安装匹配版本的 ASP.NET Core Hosting Bundle。应用程序池使用“无托管代码”。
