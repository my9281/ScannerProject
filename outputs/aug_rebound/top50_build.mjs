import fs from 'node:fs/promises';
import {FileBlob,SpreadsheetFile} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const dir='D:/GitRepos/ScannerProject/outputs/aug_rebound/';
const {rows,stats}=JSON.parse(await fs.readFile(dir+'top50_analysis.json','utf8'));
const w=await SpreadsheetFile.importXlsx(await FileBlob.load(dir+'盘库_前十票预付实收计算及差异说明.xlsx'));
const s=w.worksheets.add('前50逐票计算说明');
const heads=['排名','追踪号','机型','原预付USD','预付计算过程（反推）','试算预付USD','试算与原预付差USD','实际费用计算（核对明细）','承运净额USD','账单实收USD','差额USD','差距原因及核验重点','证据状态','发货日期','账单Zone','预估重量lb','实重lb','计费重量lb','体积重除数','运输费原价','折扣（负数）','附加费合计','燃油费','账单反推燃油率','吻合公开零售价','试算Zone','试算报价重量lb','核对表来源行号','按账单Zone试算预付'];
s.getRange('A1:AC51').values=[heads,...rows];
for(let r=2;r<=51;r++){s.getRange(`K${r}`).formulas=[[`=J${r}-D${r}`]];if(rows[r-2][5]!==null)s.getRange(`G${r}`).formulas=[[`=F${r}-D${r}`]];}
s.getRange('A:AC').format.columnWidth=19;s.getRange('A:A').format.columnWidth=8;s.getRange('B:B').format.columnWidth=21;s.getRange('B:B').setNumberFormat('@');s.getRange('C:C').format.columnWidth=22;
for(const c of ['E','H','L'])s.getRange(`${c}:${c}`).format.columnWidth=c==='L'?80:62;
s.getRange('M:M').format.columnWidth=34;s.getRange('A1:AC1').format={fill:'#284B63',font:{bold:true,color:'#FFFFFF'},wrapText:true,rowHeight:52};s.getRange('A2:AC51').format.wrapText=true;s.getRange('A2:AC51').format.verticalAlignment='top';s.getRange('A2:AC51').format.rowHeight=135;
for(const c of ['D','F','G','I','J','K','T','U','V','W','AC'])s.getRange(`${c}2:${c}51`).setNumberFormat('0.00');s.getRange('P2:Q51').setNumberFormat('0.000');s.getRange('X2:X51').setNumberFormat('0.00%');
s.freezePanes.freezeRows(1);s.freezePanes.freezeColumns(3);
s.getRange('J53').values=[['前50差额合计']];s.getRange('K53').formulas=[['=SUM(K2:K51)']];s.getRange('K53').setNumberFormat('0.00');
const summary=w.worksheets.add('前50分析汇总');const items=[
['核验项目','结果','说明'],
['前50差额合计USD',stats.diff_total,'按基础表未舍入差额降序；显示2位小数。'],
['找到核对明细',stats.matched,'运输费、负折扣、其他附加费、燃油合计与承运净额逐票核对。'],
['缺核对明细',stats.missing,'仅保留原预付和实收；不推造费用和原因。'],
['预付试算接近（差≤0.15美元）',stats.trial_close,'共40票有试算；接近不等于证明实际报价规则。'],
['现有假设未还原预付',stats.trial_available-stats.trial_close,'需原始报价回包、尺寸及折扣条件；逐票列出偏差。'],
['7月13日无折扣、除数139批次',stats.july13,'16票基础运输费吻合官方零售价；优先查渠道或费率账户。'],
['吻合官方公开零售价',stats.retail,'按最终计费重量和账单Zone核对；零售价含燃油，明细另列燃油存在重复计入疑点，非已确认错收。'],
['预估≤50lb、实际>50lb且有重量费',stats.cross50,'重量门槛因素；不等于全部差额均由重量造成。'],
['预估≤110lb、实际>110lb且有Oversize',stats.cross110,'110lb门槛因素；需原始称重证据。'],
['实际重量不高于预估',stats.actual_not_greater,'不能以增重解释此类记录的差额。'],
['尝试调整试算Zone',stats.zone_hypothesis,'9票尝试更接近金额的Zone；仅反推，部分仍未还原，不是9票已确认分区错误。'],
['分类关系',null,'上述原因可以重叠，数量不能直接相加。'],
['试算方法',null,'预估重量向上取整（距整数小于0.01lb视为kg舍入）；按标准运费假设折扣39%，重量/超大费假设折扣50%，燃油率从账单金额反推至0.25个百分点。'],
['分区选择',null,'先用账单Zone试算；偏差>1美元且其他Zone能至少改善0.50美元时才另列候选。保留按账单Zone的试算金额于AC列，便于对比。'],
['明确限制',null,'原始报价尺寸、最低收费、住宅/偏远费、历史合同等未充分提供，未加入无证据的金额凑数；未还原的6票需继续查证。'],
['内部收费',null,'承运净额与账单实收分别列出；既有内部收费倍率不展示。此表含费率折扣分析，分享前请按用途选择列。'],
['基础来源',null,'盘库_前十票预付实收计算及差异说明.xlsx；保留已有基础表及全部工作表。'],
['核对来源',null,'bluetti 打单成本价_中文表头.xlsx，Data页；逐票说明AB列给出源表行号。'],
['官方标准费率',null,'https://images.fedex.com/content/dam/fedex/us-united-states/services/Service_Guide_2026.pdf（117—119、127—128页，当前为9月更新版，历史合同需复核）'],
['官方零售费率',null,'https://www.fedex.com/content/dam/fedex/us-united-states/services/FedEx_Retail_Rates_2026.pdf（70—71页）'],
['原始实际重量缺失',null,'873867357360与873599566482原始实际重量为空，仅保留计费重量，不据此推断实际增重。']
];summary.getRange(`A1:C${items.length}`).values=items;summary.getRange('A:A').format.columnWidth=45;summary.getRange('B:B').format.columnWidth=18;summary.getRange('C:C').format.columnWidth=105;summary.getRange(`A1:C${items.length}`).format.wrapText=true;summary.getRange('A1:C1').format={fill:'#284B63',font:{bold:true,color:'#FFFFFF'}};summary.getRange('B2').setNumberFormat('0.00');summary.getRange(`A1:C${items.length}`).format.autofitRows();
w.recalculate();console.log((await w.inspect({kind:'table',range:'前50分析汇总!A1:C12',tableMaxRows:12,tableMaxCols:3,maxChars:2400})).ndjson);
await fs.writeFile(dir+'top50-summary.png',new Uint8Array(await (await w.render({sheetName:summary.name,range:'A1:C12',scale:1.2,format:'png'})).arrayBuffer()));
await (await SpreadsheetFile.exportXlsx(w)).save(dir+'盘库_前50票预付实收计算及差异说明.xlsx');
