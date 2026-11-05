# BLUETTI After-sales 工作流记录

网站入口：https://after-sales.bluettipower.com/
出库页面：https://after-sales.bluettipower.com/index.html#/afterSaleClient/inventory/client/outBoundRefactor/route/v2/index

登录由用户完成，后续复用已登录页面。通过页面的 SN 查询定位出库记录。具体后续步骤待用户演示后整理；不记录密码或令牌。

已验证步骤：SN查询并核对RMA/SN后，点击领单发件；点击产品行的出库；在出库物流信息选择FedEx；填写对应OMS运输单号（仅数字）。填写后核对出库SN、出库SKU、物流商和运输单号。页面另有开始出库按钮，当前教学步骤尚未执行。注意：首次产品点击出库后，页面自动带出的出库SKU末尾为011，而库存SKU末尾为010；不要擅自改动，后续需明确规则。

用户确认：出库SKU由010切换为011是正确规则。出库时间按页面原时间加12小时换为北京时间，正确处理跨日；本次2026-10-28 19:40:41改为2026-10-29 07:40:41。此规则为用户当前流程指定，不推断为全年固定时差。填写后离开时间框并核对值保留。
