# SOP 多语言阅读指引

[中文目录](README.md) | [English guide](../sop-en/Language-Guide.md) | [Guía en español](../sop-es/Language-Guide.md) | [文档总目录](../README.md)

更新：2026-11-09。

## 1. 从哪里进入

1. Web：登录后进入“我的账户 → 操作文档与 SOP”；Checklist 平板：登录后进入“工作台 → 操作文档”。需要 `checklist.read` 阅读权限。
2. 在文档总目录或本目录选择 [中文](README.md)、[English](../sop-en/README.md) 或 [Español](../sop-es/README.md)，再选择作业。Web 左侧也按三种语言分组。
3. 每篇 SOP 顶部的 中文 / English / Español 指向**同一流程**的其他语言；“返回本语言目录”返回当前语言的任务清单。
4. 平板可通过文档内链接切换语言；刷新目录会重新选择默认中文 SOP 目录，再次选择所需语言即可。

## 2. 文档语言与应用语言

切换文档不会切换应用按钮语言。AndroidTester PDA 在首页选择 English / Español，默认英语并记住选择。Web、WPF、Checklist 和 after-sales 的中文按钮在译文中保留原名并配本语言解释，不表示这些系统均提供语言设置。

| 中文概念/按钮 | English explanation | Explicación en español |
| --- | --- | --- |
| 良品区扫描 | Scan good stock | Escanear stock apto |
| 记录 | Record | Registrar |
| 上传 | Upload | Cargar al servidor |
| 完成（PDA） | Done: return, not upload | Volver; no carga datos |
| 入库检测 | Receiving inspection | Inspección de recepción |
| 检测通过 | Inspection passed | Inspección aprobada |
| 维修处理 | Repair processing | Tratamiento de reparación |
| 翻新处理 | Refurbishment | Reacondicionamiento |
| 良品上架 | Good-stock putaway | Ubicación de stock apto |
| 详情 / 打印 | Details / Print | Detalles / Imprimir |
| 开始出库 / 完成出库 | Start / Complete outbound | Iniciar / Completar salida |

本表的西班牙语是含义说明，不承诺与某一版本按钮逐字一致；识别实际页面时结合 SOP 保留的中文原名及截图。SN、SKU、RMA、OMS、WPF、INB、UNB 等代码不翻译，录入时保留完整字符及前导零。

## 3. 截图、附件和示例

三语共用中文 SOP 的截图和 Excel，译文中的链接直接打开相同资源，图片中的中文不会自动翻译。中文按钮旁的译文用于定位；截图中的日期、SKU、数量、运费和客户资料仅为示例。实际操作使用当前工单完整数据、实际日期/数量和原始作业照片，不把打码示例图上传为作业凭证。

在线阅读以 Markdown 为准。现有收货 Word 文件是独立文件，本轮翻译维护 Markdown；在线阅读器不提供 Word 下载入口。Excel 通过受保护链接下载，Checklist 下载后用分享菜单保存或打开。源码与未发布文件链接仅供仓库查阅。

## 4. 如何判断流程是否可执行

- 收货、良品区、上架、返修有正文；良品区仍待现场验收，上架止于“送达待确认区”，不能标记正式上架完成。
- 太阳能板测试、报废、月底结算、年度结算的中文源文仅占位，翻译版也保留“待梳理”，需先补齐规则。
- “已整理”表示已有文档内容，不代表现场批准或验收。翻译不增加权限，不把待确认事项变成正式规则。
- 良品区换托、已上架移库、PDA 上传、工单完成、正式上架是不同操作；不得互相替代。label 仅写 OK 也不能直接认定检测通过。

## 5. 遇到差异或打不开时

不同语言有歧义时，对照同一篇中文源文并向负责人核实；影响 SN、SKU、处理判定、日期、单位或提交动作时暂停相关步骤，记录章节与差异，不自行猜测。维护人员更新中文源文后，应同步两种译文、三语目录状态与链接。

打不开时先确认登录和权限，点击“刷新目录”后重试。新增文档、图片和附件需重新发布 Scanner.Web 的 `Documentation/` 资源；仅提交文件不会更新正在运行的服务。详见[文档浏览与发布](../文档浏览与发布.md)（中文）。
