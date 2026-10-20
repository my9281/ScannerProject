# BLUETTI Excel 导出接口

2026-10-20 使用本机已授权登录会话记录两个按钮的真实网络请求，并通过纯 C# HTTP 请求完成连续下载和导入验证：客诉单 2,233 条，基础表 8,876 条。没有保存密码或 Authorization Token 到抓包记录或本文。

两个接口均为同步 POST：服务器生成完成后直接返回 Excel 二进制，无任务 ID、无轮询。实际响应 `Content-Type: application/vnd.ms-excel`，内容为 ZIP 格式的 `.xlsx`。浏览器出现的 OPTIONS 是 CORS 预检，C# 不需要额外发送。

## 公共请求头

```text
Authorization: <登录接口返回的原始 token，不额外添加 Bearer>
x-app-resource: <当前所选站点 ID>
x-app-key: 18880A4BA6CF791157564C2ECD0
x-os: web
Accept-Language: zh-CN
Content-Type: application/json;charset=UTF-8
```

不需要 Cookie。站点 ID 从当前会话获取，不在代码中固定账号或站点。

## DTC 客诉单 → 预报 / 工单

`POST https://gwpry.bluettipower.com/api/bluas/client/workOrder/postSale/exportCustomerDemands`

```json
{
  "type": 1, "purchaseChannels": "", "isOverseasReceiving": "", "submitTime": [],
  "shippingOrderNo": "", "sn": "", "status": "", "afterStatus": "", "afterStatusList": [],
  "postSaleNo": "", "handler": "", "customerPhone": "", "customerName": "", "customerEmail": "",
  "afterSaleTypeList": [], "dataSourceList": [], "afterSaleSource": "", "returnReason": "",
  "orderNo": "", "model": "", "rmaNo": "", "siteId": "<当前所选站点 ID>", "customerType": "",
  "state": 1, "afterSaleReason": "", "ticketId": "", "creBy": "", "orderByField": "", "orderBy": "",
  "pageSize": 20, "pageNo": 1, "idList": []
}
```

保留实际页面默认筛选条件，包括 `state: 1`，不推断为整个站点所有历史记录。虽然请求带分页参数，本次返回文件包含 2,233 条可导入记录，并非只导出 20 条。

## 入库检测表 → 基础表

`POST https://gwpry.bluettipower.com/api/bluas/client/wareHousingInspection/exportInspection`

```json
{"inpectionResultList": [], "pageNo": 1, "pageSize": 20, "jobStatusList": []}
```

路径中的 `wareHousingInspection` 大小写，以及参数 `inpectionResultList` 的拼写均按实际请求保留。站点通过 `x-app-resource` 指定。本次返回 8,876 条可导入基础记录。

## 客户端处理

`BluettiApiExporter` 使用 HttpClient，长时间等待服务器生成；收到实际文件字节后再启动下载计时。基础表生成期限默认 30 分钟，客诉单 10 分钟，传输期限 15 分钟，读取程序 config 配置。使用流式写入，不将完整响应读入字符串。验证 XLSX ZIP 结构后才交给原导入器。

401/403、认证错误 JSON 触发登录配置提示；HTML、JSON、截断文件和非工作簿不会导入。失败删除本次文件，成功才更新缓存标记、清理同类旧文件。生产一键刷新不再启动网页或 WebView2；网页抓包和菜单导出代码仅作为开发诊断工具保留。
