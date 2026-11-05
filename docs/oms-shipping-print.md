# OMS 下单与面单打印

主界面入口：**OMS 下单与面单打印**。BLUETTI 面单查询窗口也增加了「选中记录 → OMS 下单」，可带入收件资料、工单编号、RMA、型号和 SN。

## 使用流程

1. 输入 OMS 账号及密码，登录并读取可用渠道、发件地址和单位。会话只保留在当前窗口，不保存账号密码。
2. 默认选择 FedEx-美东-NJ 渠道及发件地址列表第一条，请核对窗口显示的发件人资料。
3. 填写收件信息，地址格式为 `街道/城市/州/邮编/美国`。邮编自动截取横线之前的部分，街道连续空格合并。
4. 填写长宽高和重量，箱数固定 1，单位 INCH/LB，申报币种 USD。
5. 试算费用，核对后点击「确认费用并下单」。修改输入后旧试算失效。付款前有金额确认弹窗，付款不自动重试；同一客户单号的持久尝试记录会阻止重复付款。超时后应先在 OMS 订单列表检查，不要直接删除记录。
6. 下单成功后订单 ID 自动带入，点击「按订单 ID 获取面单」。已有订单也可单独获取，无需重新下单。
7. 输入 RMA、型号、SN，更新预览。三行放在上方收件信息右侧空白，起点 x=170pt、上缘约 y=61pt，行距 11pt；超长文字缩小字体，仍不能容纳则提示。
8. 保存 PNG/PDF，或选择本地打印。需使用 4×6 英寸纸张和 Windows 打印机驱动，无需 OMS 专属打印服务。

也可直接下载已生成的 PDF 地址，或离线打开本地 PDF 标注打印。当前只接受单页、纵向 4×6 英寸面单，其他模板需调整布局。标注修改后旧预览失效，须更新预览。

## API 契约

从页面已加载的公开 JS 核实，域名固定为 `https://ntm8zfn.oms.eccangtms.com`：

| 操作 | 请求 |
| --- | --- |
| 登录 | `POST /alpaca-oms/alpaca-auth/login` |
| 渠道 | `GET /alpaca-oms/oms/order/getCommonProduct` |
| 发件地址 | `GET /alpaca-oms/oms/omsUserShipperAddress/all` |
| 单位字典 | `GET /alpaca-oms/sys/dict/getDictItems/weight_size_unit` |
| 试算 | `POST /alpaca-oms/oms/order/submit` |
| 下单 | `POST /alpaca-oms/oms/order/payOrder` |
| 面单 | `GET /alpaca-oms/oms/order/getLabel?ids=订单ID&lang=` |

请求头为 X-Access-Token、X-System-Belong: 3。getLabel 读取 result.mergeUrl；下载 PDF 不携带 OMS 会话头，来源限定为此次观察到的 erp-us-oss.eccang.com。

试算与下单使用相同请求体：customerNo、productId、weightSizeUnit、currencyCode=USD、goodsType=3、boxCount=1、multipleFlag=1、consigneeShipper、orderDetailList。包裹字段为 boxQuantity/boxWeight/boxLength/boxWidth/boxHeight。试算读取 result.orderFeeDetail.settleTotalPrice/settleCurrency；下单读取 result.id。

本次按当前账号演示的美国境内 ground 流程实现，不含国际报关字段。账号/渠道要求不同时需补充。

## 文件和验证

使用 Windows 内置 PDF 解码，在 300 DPI 下叠加文字，输出 1200×1800 PNG 或无损 RGB PDF。原始 PDF 保存在 LocalApplicationData/Scanner/OMS/Labels；付款记录在 Orders，只存客户单号、状态、时间和订单 ID。

WPF 编译通过。tests/OmsShipping.Tests 使用模拟 HTTP 验证 API 参数、认证、地址和邮编处理、费用结果、面单下载和防重复付款。真实 FedEx PDF 已通过 C# 渲染/导出，导出 PDF 可重新打开；逐像素比较确认标注区域之外（含条码）无变化。

新代码的 OMS 登录、试算、付款尚未用生产账号完整联调。本次验证没有再创建收费订单，也没有实际发送打印任务。实体打印还需验证驱动纸张和条码扫描效果。
