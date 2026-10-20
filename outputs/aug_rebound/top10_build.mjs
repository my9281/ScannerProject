import fs from 'node:fs/promises';
import {FileBlob,SpreadsheetFile} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const dir='D:/GitRepos/ScannerProject/outputs/aug_rebound/';
const w=await SpreadsheetFile.importXlsx(await FileBlob.load(dir+'盘库_核对表_重量统一lb.xlsx'));const s=w.worksheets.getItemAt(0);
const tailFormulas=s.getRange('A308:X308').formulas;const used=s.getUsedRange();const old=used.values;const header=old[0];const records=old.slice(1).filter(r=>r[0]!==null&&r[0]!=='');const tails=old.slice(1).filter(r=>r[0]===null||r[0]==='');records.sort((a,b)=>(Number(b[2])-Number(b[1]))-(Number(a[2])-Number(a[1])));const sorted=[header,...records,...tails];s.getRange(`A1:X${sorted.length}`).values=sorted;
s.getRange('D2').formulas=[['=C2-B2']];s.getRange(`D2:D${records.length+1}`).fillDown();s.getRange('A:A').setNumberFormat('@');s.getRange('A:A').format.columnWidth=20;
for(let c=0;c<24;c++)if(tailFormulas[0][c])s.getCell(307,c).formulas=[[tailFormulas[0][c]]];const detail=w.worksheets.add('前10差额分析');const headers=['排名','追踪号','机型','预收USD','实收USD','差额USD','运输费原价','折扣（负数）','重量或Oversize费','燃油费','承运净额','明细对账差','发货日期','账单Zone','预估重量lb','实重lb','计费重量lb','体积重除数','试算报价重量lb','试算Zone','标准运费原价','假设折后运输费','假设折后附加费','假设燃油率','假设燃油费','试算预收USD','试算减原预收','发现及待核对事项','核对表来源行号'];
const rows=JSON.parse(await fs.readFile(dir+'top10_analysis.json','utf8'));detail.getRange('A1:AC11').values=[headers,...rows];
for(let i=0;i<rows.length;i++){let r=i+2;detail.getRange(`F${r}`).formulas=[[`=E${r}-D${r}`]];if(rows[i][6]!==null){detail.getRange(`L${r}`).formulas=[[`=K${r}-SUM(G${r}:J${r})`]];detail.getRange(`AA${r}`).formulas=[[`=Z${r}-D${r}`]];}}
detail.getRange('A1:AC1').format={fill:'#284B63',font:{bold:true,color:'#FFFFFF'},rowHeight:54,wrapText:true};detail.getRange('A:AC').format.columnWidth=19;detail.getRange('B:B').format.columnWidth=21;detail.getRange('B:B').setNumberFormat('@');detail.getRange('C:C').format.columnWidth=22;detail.getRange('AB:AB').format.columnWidth=90;detail.getRange('AB2:AB11').format.wrapText=true;detail.getRange('A2:AC11').format.rowHeight=55;detail.getRange('D2:L11').setNumberFormat('0.00');detail.getRange('O2:Q11').setNumberFormat('0.000');detail.getRange('U2:W11').setNumberFormat('0.00');detail.getRange('X2:X11').setNumberFormat('0.00%');detail.getRange('Y2:AA11').setNumberFormat('0.00');detail.freezePanes.freezeRows(1);detail.freezePanes.freezeColumns(3);
const n=w.worksheets.add('分析结论与口径');const notes=[
 ['项目','结论或说明'],
 ['排序与范围','基础明细按差额降序排列，前10条差额合计1284.071美元；9条在核对表中找到，1条缺原始费用明细。'],
 ['共同问题一：7月13日','前10条有6条发货日期20260713，均无Discount行、体积重除数139；6条运输费逐项对应2026 FedEx Retail Rates。'],
 ['燃油重复计入疑点','FedEx Retail Rates Ground表注称已含燃油；6条明细仍在零售价与重量费之上另加25%燃油。须核验原始FedEx发票及合同，不能直接认定错收。'],
 ['六票疑点的量级','若确认零售价已含燃油，单按零售价部分另计25%的金额合计140.8975美元（承运净额口径）；不是已确认退款。'],
 ['整张核对表的日期线索','核对表共25条发货日期20260713，全部无Discount行且体积重除数139；其他日期主要有Discount行且除数166。应优先核对该批次账户/渠道/导出规则。'],
 ['共同问题二：110lb门槛','差额最高两票预估均不超过110lb，实际116.4lb和113.4lb，Oversize费用160和137.50美元；美国国内2026年起实重超过110lb可触发Oversize。'],
 ['共同问题三：50lb门槛','873651415340与874516692368预估低于50lb、实际超过50lb，可能报价未含重量附加；报价原始尺寸和实重申报需核对。'],
 ['分区异常待查','873966246974和874516692368账单Zone8；按Zone5和Zone2试算更接近预收。仅为反推，未核验实际邮编分区或退件合同。'],
 ['预收试算口径','按预估重量附近整数磅、标准运价、假设运输折扣39%、重量附加折扣50%、25%或25.25%燃油及既有内部收费规则试算。试算并非实际报价凭证。'],
 ['整数重量口径','29.03kg换算64.0002lb按64lb试算；11.34kg换算25.0004lb按25lb试算，视为原始kg舍入导致。未将此规则改写入原始数据。'],
 ['可还原程度','9条有明细记录的试算与原预收最大相差0.10美元；分区调整、折扣和舍入都属假设。872732293053无法试算。'],
 ['金额对账口径','前10分析承运净额=运输费原价+负折扣+重量/Oversize费+燃油；实收沿用基础表。内部收费倍率不在结果表展示。'],
 ['需索取的证据','7月13日批次的原始FedEx发票、账户折扣/费率协议、API报价回包、原始标签起讫邮编和重量尺寸、评级调整原因。'],
 ['来源：基础表','盘库_核对表_重量统一lb.xlsx，本次读取版本；按行整条排序，原记录与手动修改保留。'],
 ['来源：核对明细','bluetti 打单成本价_中文表头.xlsx，Data页；前10分析最后一列为来源行号。'],
 ['来源：官方标准运价','https://images.fedex.com/content/dam/fedex/us-united-states/services/Service_Guide_2026.pdf；第117—119、127—128页；当前PDF为9月更新版，7月历史合同仍需核对。'],
 ['来源：官方零售运价','https://www.fedex.com/content/dam/fedex/us-united-states/services/FedEx_Retail_Rates_2026.pdf；第70—71页，费率与燃油包含说明。'],
 ];n.getRange(`A1:B${notes.length}`).values=notes;n.getRange('A:A').format.columnWidth=31;n.getRange('B:B').format.columnWidth=118;n.getRange(`A1:B${notes.length}`).format.wrapText=true;n.getRange('A1:B1').format={fill:'#284B63',font:{bold:true,color:'#FFFFFF'}};n.getRange(`A1:B${notes.length}`).format.autofitRows();
w.recalculate();console.log((await w.inspect({kind:'table',range:'前10差额分析!A1:L4',tableMaxRows:4,tableMaxCols:12,maxChars:1600})).ndjson);
await fs.writeFile(dir+'top10-preview.png',new Uint8Array(await (await w.render({sheetName:detail.name,range:'A1:F11',scale:1.5,format:'png'})).arrayBuffer()));
await (await SpreadsheetFile.exportXlsx(w)).save(dir+'盘库_差额倒序_前十费用分析.xlsx');


