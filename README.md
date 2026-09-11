# 幽梦运单之星扫描系统

工程名称已于2026-09-12调整为 `Scanner.WPF` 和 `Scanner.MaUI`。目录与命名空间归属见 [项目结构审计](docs/项目结构审计.md)；运行 `powershell -File tests/AuditProjectLayout.ps1` 可复查全部解决方案源码。

YM-Star Scanner System 是一款扫码记录、工单匹配、标签打印、入库检测、出库核对和库位费比对工具。Windows 完整业务基准位于 `Scanner` WPF 项目；`Scanner.MaUI` 提供 Android 小屏界面及 Windows / Intel macOS 迁移实现，当前仍有功能差异。

## 文档入口

- [项目功能说明](docs/项目功能说明.md)：各项功能、使用流程、平台差异和待办。
- [Excel 项目进度表](outputs/doc-progress-20260908/项目进度表.xlsx)：功能加入日期、Git依据、本次迁移登记与当前状态。
- [原始需求文档](request.md)：历史需求基准，部分平台描述尚未更新，应结合功能说明阅读。
- [上传站点说明](Scanner.Web/README.md)：配套Web服务的运行说明。

## 平台状态（2026-09-08）

| 平台 | 当前能力 | 主要限制 |
|---|---|---|
| Windows WPF | 扫码、打印、工单、入库/出库/库位费模块 | Windows专用，依赖.NET Framework 4.7.2 |
| Android MAUI | 单列扫码、日志、工单及新增文件处理入口 | 自动打印禁用，新增页需小屏实测 |
| Intel macOS MAUI | x64代码编译、文件处理、PDF标签及系统打印面板 | 原生打包与打印未实测，迁移未全部对齐 |
| Windows MAUI | 跨平台业务和Windows打印实现 | 不等同于WPF完整功能 |

MAUI已接入XLSX紧急工单导入，但CSV仍是旧列映射；自定义型号保存、入库完整统计、出库托盘打印、新页面多语言尚待补齐。4×4标签目前为缩放布局。编译通过不等于可安装Mac版本已经交付。

## 开发与构建

Windows WPF：使用Visual Studio / MSBuild及.NET Framework 4.7.2开发工具生成 `Scanner.WPF/Scanner.WPF.csproj`。

MAUI：安装.NET 10 SDK及目标平台工作负载，在仓库根目录执行：

```powershell
dotnet restore Scanner.MaUI/Scanner.MaUI.csproj
dotnet build Scanner.MaUI/Scanner.MaUI.csproj -f net10.0-android
dotnet build Scanner.MaUI/Scanner.MaUI.csproj -f net10.0-maccatalyst -p:RuntimeIdentifier=maccatalyst-x64
dotnet run --project tests/BarcodeClassification.Tests/BarcodeClassification.Tests.csproj
```

Mac原生打包、签名与运行需要Mac及匹配的Xcode/工作负载环境。前述对话已有编译和17项条码/OID测试记录，本次文档整理未重新运行构建。下方原有使用说明主要适用于Windows WPF；MAUI差异以项目功能说明为准。

## 使用前准备

1. 使用 Windows 电脑并安装 .NET Framework 4.7.2。
2. 安装标签打印机驱动，将需要使用的打印机设置为 Windows 默认打印机。
3. 准备 4×6 或 4×4 英寸标签纸；程序默认使用 4×6，可在主界面切换，并按无页边距方式打印。
4. 连接扫码枪，并将扫码枪设置为键盘输入模式，扫码结束发送 Enter。
5. 确保电脑能够访问 `https://repair-rms.vercel.app`。
6. 如需中文尾号播报，在 Windows 语音设置中安装中文语音，并确认默认音频设备可正常播放。

## 启动程序

开发环境中使用 Visual Studio 打开 `Scanner.slnx`，将 `Scanner.WPF` 设为启动项目后运行。

命令行编译示例：

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" Scanner.WPF\Scanner.WPF.csproj /t:Build /p:Configuration=Release /m
```

生成文件默认位于 `Scanner.WPF\bin\Release`。

## 登录

1. 输入维修系统用户名和密码。
2. 可勾选“记住密码”。保存内容使用 Windows 当前用户的 DPAPI 加密，其他 Windows 用户不能直接解密。
3. 登录成功后进入扫描主界面。
4. 已保存且仍有效的会话会在下次启动时自动使用。
5. 无需在线工单时，可点击“本地扫描模式”直接进入；该模式不请求登录和在线工单接口，仍可扫描、记录、导入紧急工单和打印。

## 日常扫描流程

1. 确认主界面显示的默认打印机正确。
2. 根据需要选择“不打印”“打印一次”或“打印两次”；默认打印两次，选择会保存在当前用户配置中。
3. 将光标保持在扫码输入框；程序通常会自动将焦点恢复到这里。
4. 扫描编码，扫码枪发送 Enter 后系统自动处理。
5. 查看右侧处理状态，确认记录、打印或错误结果。

同一次程序运行期间，相同编码只写入日志一次，但打印仍按对应业务规则执行。

## 打印规则

主界面的“纸张规格”可在 `4 × 6` 和 `4 × 4` 之间切换。默认值为 `4 × 6`；选择会保存到当前 Windows 用户的 `user.config`，下次启动自动恢复。两种规格分别使用独立布局和对应的打印机纸张尺寸。

### 普通编码

- 可选择不打印、打印一次或打印两次；所选份数会保存到当前 Windows 用户的 `user.config`，下次启动自动恢复。
- 选择打印时，程序异步播报实际打印编号的末五位。
- 如果编码匹配到工单 SN，实际打印和播报使用工单 SN。

### OID 编码

以下格式按 OID 处理：

- 34 位纯数字。
- 12 位纯数字 FedEx 编号。
- `1Z` 开头并后接 16 位字母或数字的编号。
- `420` 地区码开头、总长度超过 20 位的完整编码（不含空白及扫码枪前缀 `]C1`）。

`420` 开头且长度不超过 20 位的编码按单独地区码拦截，不记录、不打印；超过 20 位时不再仅因地区码前缀而拦截。

每次扫描 OID 都会播报“O I D”。同一个 OID 第一次只记录、不打印；从第二次扫描开始遵循当前自动打印份数。OID 次数在程序关闭后清零。

### 设备型号

系统可从扫描内容识别指定设备型号。识别成功后，标签突出显示“型号、编号末五位”，例如：

```text
AC200PL  12345
```

支持型号：

```text
AC2A AC2P EB3A AC50B AC60 AC70 AC180 AC180P AC180T
AC200L AC200P AC200M AC200PL AC240 AC300 AC500
EL10 EL30V2 EL100V2 EL200V2 EL300 EL400
B230 B300 B300K B300K2 B300S B500K
PS54 EB55 EB70 PINA AP300 SP100L PV350 PV200
```

当本次需要打印、但无法从实际打印编号中识别型号时，程序会弹出型号选择窗口：

- 在上方列表选择型号后按 Enter，立即使用该型号继续打印；双击或点击“使用所选型号”效果相同。
- 在下方输入新型号后按 Enter，会将型号加入列表、保存配置并用于本次打印。
- 自定义型号保存在 `%LOCALAPPDATA%\YM-Star Scanner\custom-models.config`，每行一个；下次启动仍会显示在列表中，并参与自动型号识别。
- 取消选择时，本次扫描记录仍保留，但不会打印。

## 工单刷新

- 程序进入主界面时自动获取当月工单备注。
- 点击“立即刷新”可手动重新获取。
- 扫描值与在线工单的 SN 或运单号一致时，会使用匹配到的工单信息。
- 紧急工单标签会显示紧急标识和备注。
- 如果提示会话过期，请重新登录。

## 导入紧急工单

点击“导入紧急工单”并选择 `.xlsx` 工作簿或 UTF-8 CSV 文件。程序按照固定列读取：

| 数据 | Excel 列 | 从 0 开始的索引 |
|---|---:|---:|
| SN | H | 7 |
| 备注（故障描述） | K | 10 |
| 售后处理类型 | L | 11 |
| 退货运单号 | O | 14 |
| 工单状态 | S | 18 |

- 只有 O 列号码去除空白后长度大于 4，且 S 列状态严格等于“待收件”的行才会导入；不满足条件的行不会生成 SN 或 OID 规则。
- H 列 SN 与扫描编号精确匹配时，该面单显示“紧急”并备注 K 列内容。
- 扫描到的 OID 只要包含 O 列号码，就会启用该号码对应的紧急上下文；之后扫描的所有非 OID 面单都显示“紧急”并备注 K 列内容，直到下一个 OID。
- 下一个 OID 会先清除上一组状态；即使新 OID 没有匹配，也不会继续沿用上一组。
- L 列只要包含“维修”（例如“退回维修”），对应面单就增加“修”字。
- 空行会跳过；SN 和运单号中的空格、制表符、换行符会全部删除，备注中的空行和换行会压缩为空格。
- 多行共享同一运单号时会合并为一条 OID 规则。导入规则优先于在线工单，重新导入会替换上一次导入内容。

## 切换语言

主界面右上角提供三个语言按钮：

- `中`：简体中文。
- `EN`：英语。
- `ES`：西班牙语。

登录和主界面均支持切换语言；入库检测、出库检测、库位付费比对和型号选择窗口也使用同一套中、英、西班牙语资源。MAUI 登录和主页面同步支持三种语言。

英文界面使用项目内嵌的 **Bickham Script Pro Semibold**，无需另行安装字体。中文、西班牙语保持原有字体，扫描编号和型号保留等宽字体便于辨认；导入原始数据、导出字段及打印标签格式不随界面语言变化。部分底层或系统异常仍显示原始消息。

运行 `powershell -NoProfile -ExecutionPolicy Bypass -File tests/Localization.Tests/Run.ps1` 可验证三种语言资源、嵌入字体并生成六个 WPF 窗口的预览（位于测试目录的 `bin/previews`）。

## 日报

主界面底部的功能按钮固定为单独一行并保持等宽。点击“日报”后，程序在可执行文件同目录的 `tempexcel` 文件夹生成并打开 Excel。工作表包含“时间、日期、内容、备注”四列，其中“时间”为从当天往前三个月起至本月 15 日止的每日日期，“日期”为对应星期；内容和备注预留给后续填写与功能扩展。

## 日志位置

扫描记录：

```text
%USERPROFILE%\Documents\SN Label Printer\scanned_codes.txt
```

错误日志：

```text
%USERPROFILE%\Documents\SN Label Printer\error.log
```

点击主界面的“打开扫描日志”可直接查看扫描记录。每个唯一编码在单次程序运行期间只记录一次。

## 常见问题

### 扫描后没有打印

检查以下项目：

1. 自动打印是否选择了“打印一次”或“打印两次”。
2. 当前编码是否为第一次扫描的 OID；第一次 OID 按规则不打印。
3. Windows 默认打印机是否正确并处于在线状态。
4. 打印队列是否暂停、缺纸或报错。
5. 查看界面处理状态和 `error.log`。

### 没有语音

1. 检查系统音量和默认音频设备。
2. 确认 Windows 已安装中文 TTS 语音。
3. OID 使用字母播报，普通打印使用编号末五位的中文数字播报。

### 相同编号没有新增日志

这是预期行为。程序在一次运行期间对扫描记录去重；重启程序后内存去重状态会重置。

### 无法获取工单

检查网络连接、账号权限和登录会话。在线接口单次最多返回 200 条，当前版本未自动翻页。

### 紧急工单导入失败

确认文件为 `.xlsx` 或 UTF-8 CSV，并检查 H、K、L、O、S 列表头是否分别为 SN码、故障描述、售后处理类型、退货运单号、工单状态。只有 O 列号码长度大于 4 且 S 列为“待收件”的行会被导入。

## 入库检测统计

- 基础数据中的处理日期按北京时间读取，统计和“当月待检测 CSV”导出前统一减去 12 小时，作为新泽西时间使用。
- 跨月临界数据按换算后的日期归入当前月、上一个月、上两个月或其余月份。
- 状态统计表最右侧“总量”列显示每个分类在所有月份中的合计数量。
- “导出简化版”按已导入的 SN 清单生成 UTF-8 TXT。匹配记录每行输出 `SN ✔`，类型包含“维修”时输出 `SN ○`；基础表中不存在的 SN 输出 `SN 不存在`。

## 项目结构

```text
Scanner.WPF/
  Helpers/       紧急工单导入、对话框、网络、打印和日志辅助类
  Languages/     中文、英文和西班牙语资源
  Models/        登录和工单数据模型
  Services/      登录、扫描、OID、型号、语音和工单服务
  ViewModels/    主窗口 MVVM 逻辑
  MainWindow.*   主扫描界面
  LoginWindow.*  登录界面
Scanner.MaUI/     已接入业务的 .NET MAUI 项目（Android / Windows / Intel macOS，迁移中）
Scanner.Web/   扫描日志上传配套站点
docs/           项目功能说明
outputs/doc-progress-20260908/  Excel项目进度表
```

完整业务要求参见 [request.md](request.md)。
