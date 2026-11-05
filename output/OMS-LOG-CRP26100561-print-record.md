# 本单面单与打印记录

- 客户单号：afsa261007145801451bc
- RMA：LOG-CRP26100561
- 型号：AC200MAX
- SN：ac200m2338001712741
- 收件人：Julian foster
- 页面显示下单成功：43.52 USD。本次开始读取时订单已经成功，未再次点击付款。
- FedEx 运单号：879014580746。
- 原始 PDF：OMS-afsa261007145801451bc-label.pdf。
- 三行标记添加至上方收件信息右侧，输出 pdf/OMS-label-LOG-CRP26100561.pdf 和同名 PNG。
- C# 命令行复用 OmsLabelDocument 完成渲染、标注、导出和本地打印，没有调用网页配套的打印服务。
- 打印机：ZPL RP425 Rongta，驱动 RONGTA RP425(ZPL 203DPI)，USB001。
- 已成功向 Windows 打印系统发送 1 份，纸张 4×6 英寸，任务名 OMS-LOG-CRP26100561-879014580746。工具返回 SUBMITTED，物理出纸尚需用户确认。
- 发送回执保存在 PNG 文件旁的 .print-receipt.txt；防止误执行命令导致重复打印。
