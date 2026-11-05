# Warning 与代码优化清单

审计日期：2026-10-21。基于当前工作区（包含已有未提交修改）；本次只整理问题，不修改业务实现。优先级：P1 为可能影响正确性、稳定性的问题，P2 为性能与维护改进，P3 为工程治理。

## 1. 构建验证范围

| 工程 | 本次结果 | 说明 |
| --- | --- | --- |
| Scanner.Web | 重新编译通过，0 warning / 0 error | 同时覆盖 Server.Model、DAL、BLL 的当前目标 |
| Scanner.WPF | 重新编译通过，0 warning / 0 error | 同时覆盖引用的共享工程当前目标，不代表所有共享目标均通过 |
| Scanner.AndroidTester | 构建通过，16 warning / 0 error | 16 条均来自打印 SDK 生成的绑定代码；未做设备实测 |
| 其他 MAUI、BreezaryStar、独立测试工程 | 未完整构建 | 不据此宣称全解决方案零告警；Apple 目标另需对应工具链 |

日志：`build/warning-audit-web.log`、`build/warning-audit-wpf.log`、`build/warning-audit-android.log`。日志可能被忽略规则排除，不作为版本库中的长期凭证。

Web/WPF 使用 `dotnet build <工程> --no-restore --no-incremental --disable-build-servers -m:1 -v minimal`；Android 使用 `-f net10.0-android --no-restore --disable-build-servers -m:1 -v minimal`。设置 `DOTNET_CLI_TELEMETRY_OPTOUT=1` 避免受限环境读取遥测注册表；禁用构建服务器后成功执行 Web/WPF 编译。未执行在线包还原或漏洞审计、发布验证、设备实测。

当前自动选择 SDK `11.0.100-rc.2.26504.105`，出现 `NETSDK1057` **message**，并非 warning。仓库未发现 `global.json`；建议固定经验证的 SDK，避免不同机器选用不同预览工具链。

## 2. Warning 分类

| 编号 | 来源及数量口径 | 含义与影响 | 建议 |
| --- | --- | --- | --- |
| CS0108 | 本次 Android 日志按完整告警行去重 5 条 | `BleInterface`、`BluetoothInterface`、`WiFiInterface`、`UsbInterface`、`SerialPortInterface` 的 `Print` 隐藏基类成员 | P2：检查 Java API 与生成签名，在绑定元数据中修正映射或有意隐藏的语义 |
| CS0114 | 本次 Android 日志去重 3 条 | 生成成员隐藏可重写基类成员 | P2：逐个核对对应 Java 方法，确定应 override 还是有意隐藏；不得机械批量加 new |
| CS8764 | 本次 Android 日志去重 8 条 | 生成重写成员返回值的可空性与基类不一致 | P2：确认 SDK 对 null 的实际约定，再修正元数据可空标注；调用侧保留必要检查 |
| NU1900 | 历史日志出现 9 次文本命中，不是 9 个独立问题 | NuGet 漏洞数据源访问失败，表示检查未完成，不能推断存在漏洞或没有漏洞 | P1（交付前）：在可访问源的环境重新还原并完成审计，不宜永久关闭检查 |
| MSB3026 | `special-label-build.log` 历史文本命中 60 次，包含重试和摘要重复 | WPF 运行进程占用 DLL，构建复制重试；属于构建环境问题 | P2：构建前正常退出应用，运行目录与构建目录分开；本次 WPF 未复现 |

16 条 Android 告警均位于 `Scanner.Rongta.Android/obj/Debug/net10.0-android/generated/src/`。**不要直接改 obj 下的生成文件**，应检查 `Scanner.Rongta.Android/Transforms/Metadata.xml`；该文件已有其他绑定修正规则，且当前有未提交修改。修改前后需做干净构建，并验证蓝牙连接、打印、断连和重连。若供应商绑定确实无法修复，可在绑定工程中记录原因后局部抑制，避免全局屏蔽业务工程 warning。

## 3. 已有源码证据的优化项

| 优先级 | 位置 | 观察及影响 | 建议与验收 |
| --- | --- | --- | --- |
| P1 | `Scanner.MaUI/Controllers/OperationsPageController.cs:16,57,95` | 北京转新泽西固定减 12 小时；月度筛选使用设备 `DateTime.Today`。固定偏移不能覆盖全年时区差，设备月份也可能与业务月份不同 | 先确认输入日期的时区约定，再统一使用业务时区转换和业务月界；覆盖冬夏时间、月初月末、Excel 日期序号和带偏移输入 |
| P1 | `Scanner.WPF/Controllers/MainWindowController.cs:65` | 非事件方法 `ActivateScanner` 为 async void，初始化 await 外无异常处理；调用者不能等待或捕获异步失败，且初始化前已置成功标记 | 改为 Task，由事件入口 await 并处理异常；初始化失败应允许重试，验证异常不退出应用且不留下错误状态 |
| P1 | `Scanner.MaUI/Services/SessionStore.cs:26` | `SaveSessionAsync` 先更新内存 Current，再写 SecureStorage；存储失败时内存与持久化状态不同 | 持久化成功后再提交 Current，或失败回滚；模拟安全存储失败，检查登录状态一致性 |
| P2 | `Scanner.WPF/Services/AuthService.cs:45`、`WorkOrderRemarkService.cs:49` | HttpResponseMessage 未显式释放，异常分支也未统一收尾 | 为 response 增加 using 生命周期；保持共享 HttpClient 复用，覆盖成功、401、非成功响应与解析失败 |
| P2 | `Scanner.Server.DAL/ShelvedPalletRepository.cs:13` | `GetAllAsync` 使用 SELECT *，排序后整表装入字典列表；数据增长会增加数据库、内存和响应负担 | 先量测典型数据规模；增加分页/查询条件，评估排序索引。若当前接口需要动态列，保留兼容路径而非直接删列 |
| P2 | `Scanner.MaUI/Controllers/OperationsPageController.cs:37,41,73,87` | 选择文件后的同步读取、解析、XLSX 写入及费用计算仍可能运行在 UI 线程；async 包装不自动转为后台执行 | 将纯文件及计算部分拆到后台或流式管线；界面更新留在主线程。用大文件验证响应时间、内存与输出一致性 |
| P2 | `Scanner.MaUI/Controllers/OperationsPageController.cs:94` | RunAsync 设置 IsBusy，但自身没有重入检查 | 检查界面是否确实禁止重复操作；必要时增加互斥门和取消令牌，验证连续点击不会并发导入、导出或共享文件 |
| P2 | `Scanner.MaUI/Services/SessionStore.cs:21`、`Scanner.WPF/Controllers/MainWindowController.cs:54,152` | 安全存储读取与缓存/同步异常只降级提示或静默处理，维护时缺少原因 | 保留对用户友好的提示，增加脱敏日志、操作阶段和关联标识；不记录口令、令牌、完整业务数据 |
| P2 | `Scanner.WPF/Controllers/MainWindowController.cs:234` | Dispose 取消 CancellationTokenSource 并解除事件，但没有释放令牌源 | 结合进行中的同步任务设计取消、等待和释放次序；验证窗口关闭时不会访问已关闭界面或产生未观察异常 |
| P2 | `Scanner.WPF/Services/AuthService.cs:15`、`WorkOrderRemarkService.cs:15`、`Scanner.MaUI/Controllers/MauiComposition.cs:13` | 服务地址分散硬编码，部分网络方法不支持调用者取消 | 集中管理地址与超时，保持现有站点边界；把 CancellationToken 传递到网络请求，验证切换配置、超时和关窗取消 |
| P3 | `Scanner.CheckListBoard` 与 `BreezaryStar/Scanner.BreezaryStar.CheckBoard` | 登录、文档和工单页面存在平行实现，后续修复容易遗漏另一套 | 对比真实差异后抽取共享服务及页面逻辑，以配置保留品牌差异；先避免一次性大规模迁移 |
| P3 | `Scanner.WPF/Helpers/PrintingHelper.cs`、`Scanner.WPF/ViewModels/MainWindowViewModel.cs`、`Scanner.MaUI/Controllers/OperationsPageController.cs` | 打印、扫描状态和文件业务集中，部分方法压缩成单行，审查及异常定位成本较高 | 按标签渲染、打印传输、导入、匹配、导出职责分步拆分；保留现有行为，用已有相关测试回归 |

以上为静态证据和风险分析，不代表每项已在运行中复现。UI 事件处理器使用 async void 合法；静态或 DI 单例 HttpClient 也已有复用，不能仅凭关键字搜索认定缺陷。

## 4. 工程治理与执行顺序

1. **先处理正确性与异常路径**：确认日期输入规则，修复业务时区、扫描初始化异步边界及会话保存一致性。仅针对相关行为补充回归验证。
2. **集中治理 Android 绑定告警**：按 3 类逐项修正 Metadata，保存可复现的干净构建结果和打印实测记录。先不要全局启用 warning-as-error。
3. **做资源与性能小步优化**：响应释放、取消传播、UI 文件处理、查询分页；以大文件及大数据量测量结果指导优化。
4. **建立构建基线**：固定 SDK，按平台配置构建任务，将独立 tests 工程纳入持续验证。项目使用不少 beta/rc 依赖，例如 WPF 的 Newtonsoft.Json、DI、System.Speech；应逐项核对必要性并做兼容回归，本次未查询线上最新版本或漏洞，不能直接指定升级版本。
5. **渐进增强静态检查**：WPF 当前 Nullable=disable，共享库部分未启用 Nullable，零 warning 不代表已消除空引用风险。先按文件/模块启用，再为新增业务告警设置门禁；用统一格式和依赖版本管理减少平行工程漂移。
6. **仓库产物管理**：检查根目录临时制图脚本、预览图片、build 日志、output 文件和 WmsUploadSite 上传样例的归档与忽略规则；先确认用途，不直接删除。日志和上传样例还需避免包含真实敏感业务内容。

本次没有修改已有业务文件，也没有执行数据库变更、依赖升级、发布或设备操作。
