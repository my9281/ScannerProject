# 壹仓扫描系统

包含 Windows WPF 仓库工作台、PDA 扫码、平板检查单、Web 和原 Mac MAUI 客户端。主应用使用 .NET 10，共享库的多目标支持不代表 WPF 仍为 .NET Framework 4.7.2。

## 文档入口

最近整理：2026-10-21。解决方案共 **17 个工程**：7 个应用、4 个服务端工程、6 个共享/平台支持工程。各工程职责、平台和依赖见 [工程总览](docs/项目结构审计.md)。上架 SOP 当前覆盖至送达待确认区，完整闭环仍有缺口，见 [上架 SOP 完备性检查](docs/上架SOP完备性检查.md)。

| 文档 | 内容 |
| --- | --- |
| [文档总目录](docs/README.md) | 操作 SOP、技术资料与历史归档 |
| [系统使用与维护](docs/系统使用与维护.md) | 首次安装、数据库与账号初始化、客户端配置、升级、备份与排障 |
| [PDA 良品区操作](docs/sop/良品区操作SOP.md) | SN 入托、上传核对、良品区换托及移库限制 |
| [仓库操作 SOP](docs/sop/README.md) | 收货、测试、上架、良品区、返修、报废和结算 |
| [工程总览](docs/项目结构审计.md) | 全部 17 个工程的职责、平台、依赖与遗留目录 |
| [上架 SOP 完备性检查](docs/上架SOP完备性检查.md) | 已覆盖步骤、缺口、系统行为及闭环验收条件 |
| [总需求与实现](request.md) | 全平台需求、WPF 目录、Web/API/SQL/部署、Mac MAUI、验收与限制 |
| [PDA AndroidTester](Scanner.AndroidTester/README.md) | 良品区、库位、SKU/SN、打印、恢复和配置 |
| [平板 CheckListBoard](Scanner.CheckListBoard/README.md) | 登录、权限、真实工单、待接入模块 |
| [PDA 配置](Scanner.AndroidTester/APPSETTINGS.md) | 嵌入 XML 地址与密钥 |
| [PDA 打印](docs/AndroidTester打印.md) | 蓝牙 SDK、配对、标签 |
| [WPF 发布与自动更新](docs/WPF发布与自动更新.md) | 精简发布、默认更新开关、版本清单与部署 |
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

## 当前数据库结构归档

2026-10-09 用户提供的实际数据库结构已整理到 [数据库档案](docs/database/README.md)，包含 [纯结构 SQL](docs/database/repairwms-schema-2026-10-09.sql)，不含任何表数据。当前现场工单已含 location，库位表没有 CHECK；此前关于 007 原始迁移脚本兼容性的记录指仓库脚本，不代表现场表仍缺失或不兼容。
