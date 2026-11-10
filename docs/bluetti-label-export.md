# BLUETTI 单 SN 查询并追加 Excel（WPF）

入口：主界面自动同步区域 → **面单查询与导出**。

1. 在主界面系统设置中配置 BLUETTI 账号、密码和售后站点。WPF 使用自身加密保存的会话，与 Codex 浏览器登录分开。
2. 选择要追加的 `.xlsx` 文件。可选择新的文件路径，也可选择已有的面单文件；选择路径本身不会创建文件。
3. 输入或扫描 **一个 SN**，按回车，或点击「查询并追加一行」。
4. 查到唯一对应工单并获取完整面单后，立即保存到 Excel 的下一行。新文件先创建表头；已有文件保留之前的行。
5. 继续输入下一个 SN。下方表格只显示本窗口已成功写入的记录。

**查不到不输出**：不会创建新文件、追加空行或修改已有 Excel。查询错误、登录失效、数据不完整或多个工单匹配时同样不写入。写入失败时 SN 留在输入框，可重试。重复查询同一个 SN 并成功会再次追加一行。

已有 Excel 的第一张工作表须具有下面的前 16 列表头；不符合时保留原文件并提示。表格对象和受保护的工作表暂不支持追加。若文件正在 Excel 中打开并被占用，请关闭后查询。

日期、SN、SKU、型號、長闊高 L x W x H (INCH)、重量（lb）、面單價格、Fedex櫃號、货箱号、RMA编号、工单编号、姓名、邮箱、电话、地址、备注。

日期来自窗口所选日期（默认今天），型号从 `P-型号-...` 的 SKU 提取。当前查询接口不提供包装尺寸、重量、价格、FedEx 柜号和货箱号，这些字段留空，可在 Excel 中补填。不会套用示例值。SN、电话等使用文本单元格，保留前导零。

## 查询 API

API 请求契约依据网站已加载的页面资源核对：

- `/views/outboundOperationManage/outboundWorkOrderPool/index.vue?v=20261013_01`
- `/views/outboundOperationManage/outboundWorkOrderPool/noodleList.vue?v=20261013_01`
- `/assets/utils/api.js`

均调用 `https://gwpry.bluettipower.com`，使用现有 BLUETTI 登录令牌及售后站点头：

1. `POST /api/bluas/client/outOperation/salePage`：请求体包含 `sn`、`siteId`、`pageNo`、`pageSize` 和页面默认空筛选字段。读取 `data.content`、`data.totalElements`（兼容线上返回的数字字符串和 JSON 整数）；对 `productMaterialVO[].sn` 做精确匹配并核对分页，避免模糊搜索误配。
2. `POST /api/bluas/client/outOperation/customerInfo?workOrderId=...&orderType=...`：与网页相同，请求体为空。从返回的 `customerName`、`customerEmail`、`customerPhone`、`shippingAddress`（空时使用 `customerAddress`）读取面单信息。

此功能直接使用 HTTP API，不加载 WebView2 或模拟点击，不领单、不执行出库。每次查询最多 60 秒，可取消。

## 验证

WPF 编译通过。`tests/BluettiLabels.Tests` 验证 API 请求方式和参数、精确 SN 匹配、地址映射、新建/追加行、查不到不修改文件、歧义及失败时不写入、保留原数据、文件占用、表头校验、Excel 文本保真。

2026/10/22 已使用 WPF 保存的有效会话完成真实 API 联调：查询 `AC3002222000215036`，校验 RMA `LOG-CRP26100294`、工单 `afsa261005163754987mu`，读取客户面单信息并写入工作区测试 Excel。线上 `totalElements` 为字符串的回归用例已补充，非法总数仍拒绝写入。诊断只输出字段名和类型，不输出令牌或客户资料。
