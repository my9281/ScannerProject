# 公共项目（第一阶段）

- `Scanner.Models`：两端使用的数据模型。保留既有命名空间，以免改变调用和序列化契约。`MauiContracts` 中的登录、工单 DTO 暂时仍与 WPF DTO 分开，分别使用 System.Text.Json 和 Newtonsoft.Json；本阶段不合并接口契约。
- `Scanner.Helpers`：跨平台 Excel 读取、检测匹配、导出、紧急工单导入，以及基础表缓存。纯计算和无状态操作继续使用静态方法，不强行为每个类添加接口。
- `Scanner.DI`：统一注册应用级 `ChecklistDataCache` 单例。WPF 在 App 创建并释放 `ScannerDependencies`，通过构造函数向窗口传递缓存；MAUI 调用 `AddScannerSharedServices`，使用自身容器创建页面，没有额外嵌套容器。

引用方向：客户端 → DI / Helpers / Models；DI → Helpers → Models。公共项目不引用客户端项目。原有源文件已移动，不再通过 Compile Link 共享。

Models 目标为 netstandard2.0。Helpers 和 DI 多目标 net472 / net10.0，以保留紧急工单 CSV 使用的 TextFieldParser 行为，并兼容 WPF 的 .NET Framework 4.7.2 和 MAUI。

打印、界面文本、窗口弹框，以及仍依赖客户端服务的 Helper 暂留各客户端。这次没有改变打印渲染或网络调用流程，也没有把全部服务都改成 DI。

基础表生命周期仍是一次应用运行：导入失败保留旧值，退出后清空。缓存已不是静态全局状态；每个应用容器拥有自己的实例。

验证：`dotnet run --project tests/SharedProjects.Tests -f net10.0`；Windows 还可构建并运行 net472 测试。现有条码测试继续位于 `tests/BarcodeClassification.Tests`。
