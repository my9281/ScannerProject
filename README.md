# 壹仓扫描系统

包含 Windows WPF 仓库工作台、PDA 扫码、平板检查单、Web 和原 Mac MAUI 客户端。主应用使用 .NET 10，共享库的多目标支持不代表 WPF 仍为 .NET Framework 4.7.2。

## 文档入口

| 文档 | 内容 |
| --- | --- |
| [总需求与实现](request.md) | 全平台需求、WPF 目录、Web/API/SQL/部署、Mac MAUI、验收与限制 |
| [PDA AndroidTester](Scanner.AndroidTester/README.md) | 良品区、库位、SKU/SN、打印、恢复和配置 |
| [平板 CheckListBoard](Scanner.CheckListBoard/README.md) | 登录、权限、真实工单、待接入模块 |
| [PDA 配置](Scanner.AndroidTester/APPSETTINGS.md) | 嵌入 XML 地址与密钥 |
| [PDA 打印](docs/AndroidTester打印.md) | 蓝牙 SDK、配对、标签 |
| [共享工程](docs/shared-projects.md) | 模型和工程引用 |
| [SQL 目录](Scanner.Web/Database) | 手动部署，兼容性见总文档 |
| [历史归档](docs/history) | 整理前说明，不作为当前平台状态依据 |

Web 和原 Mac MAUI 统一记录在总文档，专项接口说明保留作技术补充。Scanner.MaUI 已接入业务，不是空模板。

## 构建

```powershell
dotnet build Scanner.WPF/Scanner.WPF.csproj
dotnet build Scanner.Web/Scanner.Web.csproj
dotnet build Scanner.AndroidTester/Scanner.AndroidTester.csproj -f net10.0-android
dotnet build Scanner.CheckListBoard/Scanner.CheckListBoard.csproj -f net10.0-android
```

需 .NET 10 SDK，Android 另需 MAUI 工作负载/Android SDK，Mac 需 Apple 工具链。测试在 tests。现场 MySQL 5.7.17，库位建表 CHECK 兼容问题仍需处理，SQL 由维护人员手动部署。
