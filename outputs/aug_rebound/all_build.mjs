import fs from 'node:fs/promises';
import {FileBlob,SpreadsheetFile} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const dir='D:/GitRepos/ScannerProject/outputs/aug_rebound/';
const {rows,stats}=JSON.parse(await fs.readFile(dir+'all306_analysis.json','utf8'));const rest=rows.slice(50);
const w=await SpreadsheetFile.importXlsx(await FileBlob.load(dir+'盘库_前50票预付实收计算及差异说明.xlsx'));
const s=w.worksheets.add('全部306票计算说明');
const heads=['排名','追踪号','机型','原预付USD','预付计算过程（反推）','试算预付USD','试算与原预付差USD','实际费用计算（核对明细）','承运净额USD','账单实收USD','差额USD','差距原因及核验重点','证据状态','发货日期','账单Zone','预估重量lb','实重lb','计费重量lb','体积重除数','运输费原价','折扣（负数）','附加费合计','燃油费','账单反推燃油率','吻合公开零售价','候选试算Zone（未核实）','试算报价重量lb','核对表来源行号','按账单Zone试算预付'];
s.getRange('A1:AC307').values=[heads,...rows];for(let r=2;r<=307;r++){s.getRange(`K${r}`).formulas=[[`=J${r}-D${r}`]];if(rows[r-2][5]!==null)s.getRange(`G${r}`).formulas=[[`=F${r}-D${r}`]];}
s.getRange('A:AC').format.columnWidth=19;s.getRange('A:A').format.columnWidth=8;s.getRange('B:B').format.columnWidth=21;s.getRange('B:B').setNumberFormat('@');s.getRange('C:C').format.columnWidth=22;for(const c of ['E','H','L'])s.getRange(`${c}:${c}`).format.columnWidth=c==='L'?80:62;
s.getRange('M:M').format.columnWidth=34;s.getRange('A1:AC1').format={fill:'#284B63',font:{bold:true,color:'#FFFFFF'},wrapText:true,rowHeight:52};s.getRange('A2:AC307').format.wrapText=true;s.getRange('A2:AC307').format.verticalAlignment='top';s.getRange('A2:AC307').format.rowHeight=135;for(const c of ['D','F','G','I','J','K','T','U','V','W','AC'])s.getRange(`${c}2:${c}307`).setNumberFormat('0.00');s.getRange('P2:Q307').setNumberFormat('0.000');s.getRange('X2:X307').setNumberFormat('0.00%');s.freezePanes.freezeRows(1);s.freezePanes.freezeColumns(3);
s.getRange('J309').values=[['全量差额合计']];s.getRange('K309').formulas=[['=SUM(K2:K307)']];s.getRange('K309').setNumberFormat('0.00');
const n=w.worksheets.add('全量及剩余部分汇总');
const restMatched=rest.filter(r=>r[8]!==null).length;const restClose=rest.filter(r=>r[6]!==null&&Math.abs(r[6])<=.15).length;
const items=[['核验项目','全部306票','剩余256票','口径/结论'],
['记录数',306,256,'全部按差额倒序；剩余指排名51—306。'],
['补收差额合计USD',stats.diff_total,rest.reduce((t,r)=>t+Number(r[10]),0),'按未舍入数据汇总，显示2位小数。'],
['找到费用核对明细',stats.matched,restMatched,'运输费、折扣、附加费、燃油与承运净额逐票核对。'],
['缺费用核对明细',stats.missing,256-restMatched,'保留原预付与实收，不推造费用分项或原因。'],
['预付试算差≤0.15美元',stats.trial_close,restClose,'同一组折扣假设下金额接近；不是确认原始报价规则。'],
['有明细但预付未还原',stats.trial_available-stats.trial_close,restMatched-restClose,'需原始API报价回包、尺寸、附加服务及合同。'],
['7月13日无折扣、除数139批次',stats.july13,rest.filter(r=>r[13]==='20260713'&&r[20]===0&&r[18]===139).length,'基础运费吻合公开零售价；核验账户渠道、折扣和燃油计价，不直接认定错收。'],
['吻合公开零售价',stats.retail,rest.filter(r=>r[24]===true).length,'按最终计费重量与账单Zone核对，零售价含燃油的说明与另列燃油之间存在待核实疑点。'],
['预估≤50lb、实际>50lb且有重量费',stats.cross50,rest.filter(r=>r[11].includes('预估未超过50lb')).length,'使用预估重量字段，不等同于客户申报重量；不据此认定客户低报。'],
['预估≤110lb、实际>110lb且有Oversize',stats.cross110,rest.filter(r=>r[11].includes('预估未超过110lb')).length,'需实际称重和适用合同证明；此类门槛费用会造成跳涨。'],
['实际重量不高于预估',stats.actual_not_greater,rest.filter(r=>r[11].includes('实际重量不高于预估')).length,'不能以增重解释这些票的差额。'],
['列有重量附加费（不代表低报）',stats.weight_fee_charged,null,'70票列有重量附加费；其中只有18票属于预估≤50lb实际>50lb，不能把其他票都归因于跨门槛。'],
['列有住宅派送费',stats.residential_fee,rest.filter(r=>r[11].includes('住宅派送费')).length,'确认有此费用；是否预付漏估待核验报价。'],
['列有派送区域/偏远费',stats.delivery_area_fee,rest.filter(r=>r[11].includes('派送区域/偏远费')).length,'DAS各子类别按票去重；与住宅费用可同时存在。'],
['列有尺寸额外处理费',stats.dimension_fee,rest.filter(r=>r[11].includes('尺寸额外处理费')).length,'需原始包裹尺寸、测量单位和合同。'],
['列有包装额外处理费',stats.packaging_fee,rest.filter(r=>r[11].includes('包装额外处理费')).length,'需包装照片、FedEx处理记录及报价明细。'],
['列有地址更正费',stats.address_fee,rest.filter(r=>r[11].includes('地址更正费')).length,'需原始面单与更正后的地址记录。'],
['尝试不同Zone的反推',stats.zone_hypothesis,rest.filter(r=>r[25]!==null&&r[25]!==r[14]).length,'这是数值试算，不是已确认的分区错误；不能说客户手选了分区。'],
['分类关系',null,null,'不同因素可重叠，数量不能直接相加；金额吻合不能证明归因。'],
['报价试算方法',null,null,'预估重量向上取整（近整数0.01lb内视为kg舍入），标准运输费假设优惠39%、重量/超大费优惠50%；燃油率由核对金额反推到0.25个百分点。'],
['分区试算方法',null,null,'优先账单Zone；若偏差>1美元且其他Zone可改善至少0.50美元，另列候选。AC列保留按账单Zone计算的预付；原报价Zone未获证实。'],
['数据缺口与限制',null,null,'最低收费、住宅/偏远等服务、申报尺寸、历史费率、报价API回包不足；不能用现有假设强行解释所有记录。'],
['收费展示',null,null,'承运净额与账单实收分别列示，内部收费倍率不展示。原有基础表、前10和前50分析页均保留。'],
['来源：基础',null,null,'盘库_前50票预付实收计算及差异说明.xlsx，本次读取版本。'],
['来源：核对',null,null,'bluetti 打单成本价_中文表头.xlsx，Data页；全量逐票说明AB列给出来源行号。'],
['官方标准费率',null,null,'https://images.fedex.com/content/dam/fedex/us-united-states/services/Service_Guide_2026.pdf；当前9月更新版仅作试算参考，历史协议需核对。'],
['官方零售费率',null,null,'https://www.fedex.com/content/dam/fedex/us-united-states/services/FedEx_Retail_Rates_2026.pdf；当前已核验1—100lb对应表价。'],
['优先补证顺序',null,null,'先核对7月13日批次发票和账户协议，再复核重量跨门槛票及候选Zone票；补齐55票缺失账单。']];
n.getRange(`A1:D${items.length}`).values=items;n.getRange('A:A').format.columnWidth=47;n.getRange('B:C').format.columnWidth=20;n.getRange('D:D').format.columnWidth=106;n.getRange(`A1:D${items.length}`).format.wrapText=true;n.getRange('A1:D1').format={fill:'#284B63',font:{bold:true,color:'#FFFFFF'}};n.getRange('B3:C3').setNumberFormat('0.00');n.getRange(`A1:D${items.length}`).format.autofitRows();
w.recalculate();console.log((await w.inspect({kind:'table',range:'全量及剩余部分汇总!A1:D13',tableMaxRows:13,tableMaxCols:4,maxChars:2700})).ndjson);
await fs.writeFile(dir+'all306-summary.png',new Uint8Array(await (await w.render({sheetName:n.name,range:'A1:D13',scale:1.2,format:'png'})).arrayBuffer()));
await fs.writeFile(dir+'all306-detail.png',new Uint8Array(await (await w.render({sheetName:s.name,range:'D52:H53',scale:1.2,format:'png'})).arrayBuffer()));
await (await SpreadsheetFile.exportXlsx(w)).save(dir+'盘库_全部306票费用计算及差异分析.xlsx');
