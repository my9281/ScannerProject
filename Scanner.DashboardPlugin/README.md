# 主界面信息插件

独立 WPF 类库，包含新泽西/北京时钟、Newark 日出日落、天气及宿主版本显示。
目标框架为 .NET 10 Windows WPF，无第三方包，不引用 Scanner.WPF。

## 构建及安装

```powershell
dotnet build Scanner.DashboardPlugin/Scanner.DashboardPlugin.csproj -c Release
```

将生成的 `Scanner.DashboardPlugin.dll` 放进主程序目录下的 `Plugins` 文件夹，重启程序即可加载。
移走此 DLL 并重启即可移除插件。运行时不支持热替换或卸载，替换前关闭程序。
构建/发布 Scanner.WPF 时会自动构建插件，并将 DLL 放进输出/发布目录的 `Plugins` 文件夹。

## 加载约定 v1

主程序仅按固定路径加载 `Plugins/Scanner.DashboardPlugin.dll`，反射调用：

```csharp
Scanner.DashboardPlugin.PluginEntry.CreateView(System.Version hostVersion)
// 返回 System.Windows.FrameworkElement，在 WPF UI 线程调用。
```

版本参数由宿主的自动更新版本源提供，显示的是主程序版本。
宿主不直接引用插件程序集；DLL 缺失、损坏或入口不兼容时跳过插件。
插件通过 Loaded/Unloaded 启停时钟并取消网络请求。DLL 与主程序在同一进程运行，非隔离沙箱，仅安装可信 DLL。

天气和日出日落继续使用原有 Open-Meteo 接口；拆分工程不会改变数据服务的使用条款。
本次未为项目新增开源许可证，公开开源时需另行确定许可证。

## 验证

```powershell
dotnet run --project tests/DashboardPlugin.Tests -- <插件DLL绝对路径> [预览PNG绝对路径]
```

检查缺失/损坏/不兼容 DLL、动态实例化和嵌入 XAML、宿主版本传递、独立依赖及卡片布局。

语言通过宿主动态资源 DashboardCulture（zh-CN / en-US / es-ES）同步，资源缺失时默认中文。切换语言会立即重绘时钟、星期、天气、日出日落、版本标题和断网提示，并复用缓存数据，不额外请求天气接口。长文本省略显示，悬停查看全文。
