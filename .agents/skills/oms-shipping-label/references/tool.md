# 离线标注与本地打印工具

工具包自带 C# 源码，依赖 Windows 10 1809+ 和 .NET 10 SDK（更高 SDK 可构建），无第三方 NuGet 包、网页或 OMS 本地服务依赖。打印仍需 Windows 打印机驱动。使用系统 dotnet 或已发现的 SDK 路径。

从 skill 目录调用 PowerShell 脚本，路径和数据作为独立参数传递；不要拼接执行字符串。`BuildDirectory` 必须是当前任务允许写入的本地目录。

```powershell
& ./scripts/Invoke-OmsLabel.ps1 -Action Render `
  -BuildDirectory 'D:/Work/output/oms-tool' `
  -SourcePdf 'D:/Work/original.pdf' -OutputPrefix 'D:/Work/output/annotated' `
  -Rma 'RMA-EXAMPLE' -Model 'MODEL' -Serial 'SN-EXAMPLE'
```

生成 `<OutputPrefix>.pdf` 和 `.png`。先读取生成的 PNG 确认标注处于收件信息右侧，原始收件资料及条码未被遮挡。源 PDF 必须是当前订单对应面单，不能套用历史文件。

用户要求实物打印后，检查打印机名称和状态，再执行：

```powershell
& ./scripts/Invoke-OmsLabel.ps1 -Action Print `
  -BuildDirectory 'D:/Work/output/oms-tool' `
  -ImagePath 'D:/Work/output/annotated.png' `
  -PrinterName 'ZPL RP425 Rongta' -JobName 'OMS-<RMA>-<tracking>'
```

打印 1 份、4×6 英寸；程序校验驱动纸张，失败则停止。图像旁会创建 `.print-receipt.txt`，记载尝试/提交情况。标记已经存在时不能自动重跑；先核对实际队列和出纸状态。用户明确要求补打后，保留历史回执，可复制 PNG 到新的任务文件名，再发送新的单份任务。

Windows PDF 组件和打印后台可能在受限执行环境中被拒绝访问。遵循运行时权限机制申请具体操作授权，不用其他方式规避；不要把权限错误误判为 PDF 损坏。

脚本执行成功后退出码为 0；失败时非零且不自动重试打印。打印回执和队列状态只是系统证据，实物出纸由用户确认。
