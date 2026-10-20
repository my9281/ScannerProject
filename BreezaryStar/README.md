# BreezaryStar

`ScannerProject.BreezaryStar.WPF` 是独立的 .NET 10 WPF 收货数据测试应用。当前界面通过 Scanner.Web 读取腾讯智能表格，腾讯文档 OAuth 授权和凭据留在服务器。

## 先配置服务器

按 [Scanner.Web 腾讯文档接入说明](../Scanner.Web/TENCENT_DOCS.md) 配置、部署并完成管理员 OAuth 授权。默认目标为「Breezary 风扇 - 维修美东 / 收货记录表」。独立智能表与当前 Open API 的兼容性需要真实授权后验证。

## 运行 WPF

```powershell
dotnet run --project BreezaryStar/ScannerProject.BreezaryStar.WPF/ScannerProject.BreezaryStar.WPF.csproj
```

也可以打开 `Scanner.slnx`，将 BreezaryStar 下的 WPF 项目设为启动项目。

1. 服务器地址默认 `https://wms.ymforever.com`，可在窗口修改或通过本机 `BREEZARYSTAR_SERVER_URL` 环境变量提供。生产站点要求 HTTPS 根地址，本机测试可用 `http://127.0.0.1:端口`。
2. 输入 **Scanner 管理员账号和密码**。不是腾讯文档账号，也不是个人 MCP Token。密码不保存到文件，不修剪首尾空格。
3. 点击「服务器授权管理」可打开服务器的 `/tencent-docs.html`；浏览器中需另外登录 Scanner 管理员账号并连接腾讯文档。
4. 点击「拉取数据」，程序登录 Scanner.Web 后分页获取 `/api/tencent-docs/records`，直接读取服务器 MySQL 本地数据，展示动态列和记录 ID。此过程不要求腾讯在线或令牌有效；首次使用先在网页同步入库，建表及自动同步配置见 [本地同步说明](../Scanner.Web/BREEZARY_SYNC.md)。
5. 读取结束、失败或取消后，关闭本次临时 Scanner 会话。最多读取 10000 条记录，超限会报告失败；不会将部分结果标为成功。

如果看到「服务器没有此接口」，请先部署新版 Scanner.Web。若窗口仍显示「个人 Token / 获取 Token」，运行的是旧版本，需要关闭旧窗口后重新编译启动。

## 代码与验证

- `Services/ScannerServerReader.cs`：当前 WPF 使用的服务器登录和分页读取客户端。
- `Services/TencentDocsReader.cs`：保留的早期 MCP 测试客户端，兼容 `field_values` / `values` 及 `record_id` / `recordID`。当前界面不再使用个人 MCP Token。
- 自动生成表格列，支持文本、选项、数组值；列表只读，不执行状态回写。

```powershell
dotnet run --project tests/BreezaryStar.Tests/BreezaryStar.Tests.csproj
```

测试通过内存 HTTP 响应验证两种记录字段格式、MCP JSON/SSE、分页，以及服务器登录、授权状态、未部署提示、失败处理及临时会话注销。真实腾讯账号的授权与读取仍需部署后验证。
