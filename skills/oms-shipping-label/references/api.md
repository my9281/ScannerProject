# OMS 接口和现有代码

API 域名：`https://ntm8zfn.oms.eccangtms.com`；统一前缀 `/alpaca-oms`。此契约来自已观察的网页请求路径及公开网页脚本，2026-10-22 核对。

| 操作 | 请求 | 关键字段 |
| --- | --- | --- |
| 登录 | POST `/alpaca-auth/login` | username、password、remember_me=false、systemBelong=3；code=1，result.token |
| 常用渠道 | GET `/oms/order/getCommonProduct` | result[] 的 id/name |
| 发件地址 | GET `/oms/omsUserShipperAddress/all` | result[] 的 shipperName/shipperAddress1 等；界面选择第一条 |
| 单位字典 | GET `/sys/dict/getDictItems/weight_size_unit` | result[] 的 text/value，选择 INCH/LB 对应值，不能假定数值编码 |
| 试算 | POST `/oms/order/submit` | result.orderFeeDetail.settleTotalPrice/settleCurrency |
| 正式下单 | POST `/oms/order/payOrder` | 与试算使用同一请求体；result.id/realPrice |
| 面单 | GET `/oms/order/getLabel?ids=<orderId>&lang=` | result.mergeUrl |

除登录外，OMS 请求头有 `X-Access-Token`、`X-System-Belong: 3`。网页另加 `lang`，部分 GET 请求另加 `_t` 时间戳。普通响应检查 success=true；不要将登录 code=1 与 BLUETTI msgCode=0 混淆。

请求体顶层：`customerNo`、`productId`、`weightSizeUnit`、`currencyCode=USD`、`goodsType=3`、`boxCount=1`、`multipleFlag=1`、`consigneeShipper`、`orderDetailList`。

地址对象收件字段：`consigneeName/Email/Phone/CountryCode/StateOrProvince/City/Address1/PostCode`；发件字段用 `shipper` 前缀，按所选地址回填。包裹首行字段：`boxNo=null`、`boxQuantity=1`、`boxWeight`、`boxLength`、`boxWidth`、`boxHeight`。

`submit` 与 `payOrder` 分开调用；确认后的收费请求不可自动重试。新程序应在网络调用前按客户单号建立持久尝试记录，结果不明先核对已有订单。

## 代码与验证范围

随附 assets/label-tool 只有离线标注/导出和 Windows 打印，不调用收费 API。

原项目（存在时）位于 `D:/GitRepos/ScannerProject`：

- `Scanner.WPF/OmsShippingWindow.cs`：账号、资料、试算、金额确认、下单和已有面单入口。
- `Scanner.WPF/Services/OmsShippingService.cs`：API 请求、地址拆分、防重复下单。
- `Scanner.WPF/Services/OmsLabelDocument.cs`：PDF 解码、标注、PNG/PDF 导出和打印。
- `tests/OmsShipping.Tests`：模拟 API、防重复付款、真实面单渲染、标注外像素不变验证。

浏览器填单、下单、获取 PDF 已实测；离线 C# 标注、导出和向 Rongta 驱动发送单份 4×6 打印任务已实测。新写的 API 登录/试算/付款代码只做过模拟测试，不据此宣称生产 API 全流程通过。新任务优先使用用户当前已登录页面或经过实际验证的 API 会话；不要从页面隐藏存储中擅取令牌。
